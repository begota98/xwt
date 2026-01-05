using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

			var text = data?.ToString();
			if (string.IsNullOrEmpty(text)) {
				clipboard.SetContent(null);
				return;
			}

			string mimeType = GetMimeType(type);
			var bytes = GLib.Bytes.New(Encoding.UTF8.GetBytes(text));
			var provider = Gdk.ContentProvider.NewForBytes(mimeType, bytes);
			clipboard.SetContent(provider);
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
				var texture = ReadTexture(clipboard);
				if (texture == null)
					return null;
				var bytes = texture.SaveToPngBytes();
				try {
					var span = bytes.GetRegionSpan<byte>(UIntPtr.Zero, bytes.GetSize());
					using var ms = new MemoryStream(span.ToArray());
					var handler = new GtkImageBackendHandler();
					return ApplicationContext.Toolkit.WrapImage(handler.LoadFromStream(ms));
				} finally {
					bytes.Unref();
				}
			}

			try {
				return clipboard.ReadTextAsync().GetAwaiter().GetResult();
			} catch {
				return null;
			}
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
			return "text/plain";
		}

		static Gdk.Texture ReadTexture(Gdk.Clipboard clipboard)
		{
			var method = clipboard.GetType().GetMethod("ReadTextureAsync");
			if (method == null)
				return null;
			var task = method.Invoke(clipboard, Array.Empty<object>()) as Task;
			if (task == null)
				return null;
			try {
				task.Wait();
			} catch {
				return null;
			}
			var resultProp = task.GetType().GetProperty("Result");
			return resultProp?.GetValue(task) as Gdk.Texture;
		}
	}
}
