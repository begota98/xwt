using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using GObject.Internal;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class GtkClipboardBackend : ClipboardBackend
	{
		static Gdk.Clipboard GetClipboard()
		{
			var display = Gdk.Display.GetDefault();
			return display?.GetClipboard();
		}

		public override void Clear()
		{
			var clipboard = GetClipboard();
			if (clipboard == null)
				return;
			clipboard.SetContent(null);
		}

		public override void SetData(TransferDataType type, Func<object> dataSource)
		{
			var clipboard = GetClipboard();
			if (clipboard == null || dataSource == null)
				return;

			object data = dataSource();

			if (type == TransferDataType.Text) {
				clipboard.SetText(data as string ?? string.Empty);
				return;
			}

			if (type == TransferDataType.Image) {
				var image = data as Image;
				if (image == null) {
					clipboard.SetContent(null);
					return;
				}
				var desc = image.ToImageDescription(ApplicationContext);
				var gtkImage = desc.Backend as GtkImage;
				if (gtkImage?.Pixbuf == null) {
					clipboard.SetContent(null);
					return;
				}
				var texture = Gdk.Texture.NewForPixbuf(gtkImage.Pixbuf);
				clipboard.SetTexture(texture);
				return;
			}

			if (data == null) {
				clipboard.SetContent(null);
				return;
			}

			string mimeType = GetMimeType(type);
			byte[] payload = BuildPayload(type, data);
			if (payload == null || payload.Length == 0) {
				clipboard.SetContent(null);
				return;
			}
			var bytes = GLib.Bytes.New(payload);
			clipboard.SetContent(Gdk.ContentProvider.NewForBytes(mimeType, bytes));
		}

		public override bool IsTypeAvailable(TransferDataType type)
		{
			var clipboard = GetClipboard();
			if (clipboard == null)
				return false;

			var formats = clipboard.GetFormats();
			if (formats == null)
				return false;

			if (type == TransferDataType.Text)
				return formats.ContainMimeType("text/plain");
			if (type == TransferDataType.Image)
				return formats.ContainMimeType("image/png") || formats.ContainMimeType("image/jpeg");

			return formats.ContainMimeType(GetMimeType(type));
		}

		public override object GetData(TransferDataType type)
		{
			var clipboard = GetClipboard();
			if (clipboard == null)
				return null;

			if (type == TransferDataType.Text) {
				try {
					return clipboard.ReadTextAsync().GetAwaiter().GetResult();
				} catch {
					return null;
				}
			}

			if (type == TransferDataType.Image) {
				var bytes = ReadClipboardBytes(clipboard, GetMimeTypes(type), out var mimeType);
				if (bytes == null || string.IsNullOrEmpty(mimeType))
					return null;
				if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
					return null;
				return LoadImageFromBytes(bytes);
			}

			var dataBytes = ReadClipboardBytes(clipboard, GetMimeTypes(type), out var resolvedMime);
			if (dataBytes == null || string.IsNullOrEmpty(resolvedMime))
				return null;

			if (resolvedMime.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
				return Encoding.UTF8.GetString(dataBytes);
			if (string.Equals(resolvedMime, "text/uri-list", StringComparison.OrdinalIgnoreCase))
				return ParseUris(Encoding.UTF8.GetString(dataBytes));
			if (string.Equals(resolvedMime, "text/html", StringComparison.OrdinalIgnoreCase))
				return Encoding.UTF8.GetString(dataBytes);
			if (string.Equals(resolvedMime, "text/rtf", StringComparison.OrdinalIgnoreCase))
				return Encoding.UTF8.GetString(dataBytes);

			var store = new TransferDataStore();
			store.AddValue(type, dataBytes);
			return ((ITransferData)store).GetValue(type);
		}

		public override IAsyncResult BeginGetData(TransferDataType type, AsyncCallback callback, object state)
		{
			object data = GetData(type);
			var result = new SimpleAsyncResult(state, data);
			callback?.Invoke(result);
			return result;
		}

		public override object EndGetData(IAsyncResult ares)
		{
			if (ares is SimpleAsyncResult result)
				return result.Data;
			return null;
		}

		class SimpleAsyncResult : IAsyncResult
		{
			readonly object asyncState;
			public SimpleAsyncResult(object asyncState, object data)
			{
				this.asyncState = asyncState;
				Data = data;
			}

			public object AsyncState => asyncState;
			public System.Threading.WaitHandle AsyncWaitHandle => null;
			public bool CompletedSynchronously => true;
			public bool IsCompleted => true;
			public object Data { get; }
		}

		static string GetMimeType(TransferDataType type)
		{
			if (type == TransferDataType.Html)
				return "text/html";
			if (type == TransferDataType.Rtf)
				return "text/rtf";
			if (type == TransferDataType.Uri)
				return "text/uri-list";
			if (type == TransferDataType.Image)
				return "image/png";
			return "text/plain";
		}

		static string[] GetMimeTypes(TransferDataType type)
		{
			if (type == TransferDataType.Text)
				return new[] { "text/plain" };
			if (type == TransferDataType.Uri)
				return new[] { "text/uri-list" };
			if (type == TransferDataType.Html)
				return new[] { "text/html" };
			if (type == TransferDataType.Rtf)
				return new[] { "text/rtf" };
			if (type == TransferDataType.Image)
				return new[] { "image/png", "image/jpeg" };
			return new[] { "application/" + type.Id };
		}

		static byte[] BuildPayload(TransferDataType type, object data)
		{
			if (type == TransferDataType.Uri) {
				var uris = data as System.Uri[] ?? (data is System.Uri uri ? new[] { uri } : null);
				if (uris == null || uris.Length == 0)
					return Array.Empty<byte>();
				var text = string.Join("\r\n", uris.Select(u => u.AbsoluteUri));
				return Encoding.UTF8.GetBytes(text);
			}
			if (type == TransferDataType.Html || type == TransferDataType.Rtf) {
				var text = data as string ?? data.ToString();
				return Encoding.UTF8.GetBytes(text ?? string.Empty);
			}
			if (data is byte[] bytes)
				return bytes;
			var serialized = TransferDataSource.SerializeValue(data);
			return serialized ?? Array.Empty<byte>();
		}

		static byte[] ReadClipboardBytes(Gdk.Clipboard clipboard, string[] mimeTypes, out string mimeType)
		{
			mimeType = null;
			if (clipboard == null || mimeTypes == null || mimeTypes.Length == 0)
				return null;

			var state = new ClipboardReadState {
				Clipboard = clipboard,
				Done = new ManualResetEvent(false)
			};
			state.MimeTypesHandle = GLib.Internal.Utf8StringArrayNullTerminatedOwnedHandle.Create(mimeTypes);
			state.Callback = state.HandleReady;
			var handle = GCHandle.Alloc(state);
			try {
				Gdk.Internal.Clipboard.ReadAsync(clipboard.Handle.DangerousGetHandle(), state.MimeTypesHandle, 0, IntPtr.Zero, state.Callback, GCHandle.ToIntPtr(handle));
				state.Done.WaitOne();
			} finally {
				if (handle.IsAllocated)
					handle.Free();
			}

			if (state.Error != null)
				return null;
			mimeType = state.MimeType;
			return state.Data;
		}

		Image LoadImageFromBytes(byte[] dataBytes)
		{
			try {
				using var stream = new MemoryStream(dataBytes);
				var handler = new GtkImageBackendHandler();
				var toolkit = ApplicationContext?.Toolkit ?? Toolkit.CurrentEngine;
				return toolkit.WrapImage(handler.LoadFromStream(stream));
			} catch {
				return null;
			}
		}

		static System.Uri[] ParseUris(string text)
		{
			if (string.IsNullOrEmpty(text))
				return Array.Empty<System.Uri>();
			var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var list = new List<System.Uri>();
			foreach (var line in lines) {
				var trimmed = line.Trim();
				if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
					continue;
				if (System.Uri.TryCreate(trimmed, System.UriKind.Absolute, out var uri))
					list.Add(uri);
			}
			return list.ToArray();
		}

		static byte[] ReadAllBytes(Gio.InputStream stream)
		{
			using var ms = new MemoryStream();
			while (true) {
				GLib.Bytes bytes;
				try {
					bytes = stream.ReadBytes(8192, null);
				} catch {
					break;
				}
				if (bytes == null)
					break;
				var size = bytes.GetSize();
				if (size == 0) {
					bytes.Unref();
					break;
				}
				var span = bytes.GetRegionSpan<byte>(UIntPtr.Zero, size);
				ms.Write(span);
				bytes.Unref();
			}
			return ms.ToArray();
		}

		sealed class ClipboardReadState
		{
			public Gdk.Clipboard Clipboard;
			public ManualResetEvent Done;
			public byte[] Data;
			public string MimeType;
			public Exception Error;
			public Gio.Internal.AsyncReadyCallback Callback;
			public GLib.Internal.Utf8StringArrayNullTerminatedOwnedHandle MimeTypesHandle;

			public void HandleReady(IntPtr sourceObject, IntPtr res, IntPtr data)
			{
				try {
					var asyncResult = new Gio.AsyncResultHelper(new ObjectHandle(res, false));
					if (Clipboard == null) {
						Error = new InvalidOperationException("Clipboard handle is missing.");
						return;
					}
					string outMime;
					using var stream = Clipboard.ReadFinish(asyncResult, out outMime);
					MimeType = outMime;
					if (stream != null)
						Data = ReadAllBytes(stream);
				} catch (Exception ex) {
					Error = ex;
				} finally {
					MimeTypesHandle.Dispose();
					Done.Set();
				}
			}
		}
	}
}
