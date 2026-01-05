using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GdkPixbuf;
using GLib;
using Xwt.Backends;
using Xwt.Drawing;
using Xwt.CairoBackend;

namespace Xwt.GtkBackend
{
	public class GtkImageBackendHandler : ImageBackendHandler
	{
		static readonly Dictionary<string, string> stockIconNames = new Dictionary<string, string> {
			{ StockIconId.Error, "dialog-error" },
			{ StockIconId.Warning, "dialog-warning" },
			{ StockIconId.Information, "dialog-information" },
			{ StockIconId.Question, "dialog-question" },
			{ StockIconId.ZoomIn, "zoom-in" },
			{ StockIconId.ZoomOut, "zoom-out" },
			{ StockIconId.ZoomFit, "zoom-fit-best" },
			{ StockIconId.Zoom100, "zoom-original" },
			{ StockIconId.OrientationPortrait, "orientation-portrait" },
			{ StockIconId.OrientationLandscape, "orientation-landscape" },
			{ StockIconId.Add, "list-add" },
			{ StockIconId.Remove, "list-remove" }
		};

		public override object LoadFromStream(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			using var ms = new MemoryStream();
			stream.CopyTo(ms);
			using var bytes = Bytes.New(ms.ToArray());
			var loader = PixbufLoader.New();
			loader.WriteBytes(bytes);
			loader.Close();
			var pixbuf = loader.GetPixbuf();
			if (pixbuf == null)
				throw new InvalidOperationException("Failed to load image.");
			return new GtkImage(pixbuf);
		}

		public override void SaveToStream(object backend, Stream stream, ImageFileType fileType)
		{
			if (!(backend is GtkImage image) || image.Pixbuf == null)
				throw new InvalidOperationException("No image data.");

			using var output = Gio.MemoryOutputStream.NewResizable();
			if (!image.Pixbuf.SaveToStreamv(output, GetFileType(fileType), null, null, null))
				throw new InvalidOperationException("Failed to save image.");
			output.Close(null);
			var bytes = output.StealAsBytes();
			try {
				var span = bytes.GetRegionSpan<byte>(UIntPtr.Zero, bytes.GetSize());
				stream.Write(span);
			} finally {
				bytes.Unref();
			}
		}

		public override object CreateMultiResolutionImage(IEnumerable<object> images)
		{
			if (images == null)
				throw new ArgumentNullException(nameof(images));

			GtkImage best = null;
			double bestArea = -1;
			foreach (var image in images) {
				if (!(image is GtkImage gtkImage))
					continue;
				var size = gtkImage.DefaultSize;
				var area = size.Width * size.Height;
				if (best == null || area > bestArea) {
					best = gtkImage;
					bestArea = area;
				}
			}

			if (best == null)
				return new GtkImage((Pixbuf)null);

			foreach (var image in images) {
				if (image is GtkImage gtkImage && !ReferenceEquals(gtkImage, best))
					gtkImage.Dispose();
			}

			return best;
		}

		public override object CreateMultiSizeIcon(IEnumerable<object> images)
		{
			return CreateMultiResolutionImage(images);
		}

		public override object CreateCustomDrawn(ImageDrawCallback drawCallback)
		{
			if (drawCallback == null)
				throw new ArgumentNullException(nameof(drawCallback));
			return new GtkImage(drawCallback);
		}

		public override Image GetStockIcon(string id)
		{
			if (id == null)
				throw new ArgumentNullException(nameof(id));

			if (!stockIconNames.TryGetValue(id, out var iconName))
				iconName = id;

			var image = TryLoadThemeIcon(iconName, 16, 1) ?? (iconName != id ? TryLoadThemeIcon(id, 16, 1) : null);
			return ApplicationContext.Toolkit.WrapImage(image ?? new GtkImage((Pixbuf)null));
		}

		public override bool IsBitmap(object handle)
		{
			return handle is GtkImage image && !image.HasMultipleSizes;
		}

		public override object ConvertToBitmap(ImageDescription idesc, double scaleFactor, ImageFormat format)
		{
			if (idesc.Backend is GtkImage image && image.Pixbuf != null)
				return idesc.Backend;
			return idesc.Backend;
		}

		public override bool HasMultipleSizes(object handle)
		{
			return handle is GtkImage image && image.HasMultipleSizes;
		}

		public override Size GetSize(object handle)
		{
			var img = (GtkImage)handle;
			return img.DefaultSize;
		}

		public override Size GetSize(string file)
		{
			var pixbuf = Pixbuf.NewFromFile(file);
			var size = new Size(pixbuf.Width, pixbuf.Height);
			pixbuf.Dispose();
			return size;
		}

		public override object CopyBitmap(object handle)
		{
			var img = (GtkImage)handle;
			return img.Pixbuf != null ? new GtkImage(img.Pixbuf.Copy()) : new GtkImage((Pixbuf)null);
		}

		public override void CopyBitmapArea(object srcHandle, int srcX, int srcY, int width, int height, object destHandle, int destX, int destY)
		{
			var src = ((GtkImage)srcHandle).Pixbuf;
			var dest = ((GtkImage)destHandle).Pixbuf;
			if (src == null || dest == null)
				return;
			src.CopyArea(srcX, srcY, width, height, dest, destX, destY);
		}

		public override object CropBitmap(object handle, int srcX, int srcY, int width, int height)
		{
			var pixbuf = ((GtkImage)handle).Pixbuf;
			if (pixbuf == null)
				return new GtkImage((Pixbuf)null);
			var cropped = pixbuf.NewSubpixbuf(srcX, srcY, width, height);
			return new GtkImage(cropped);
		}

		public override void SetBitmapPixel(object handle, int x, int y, Color color)
		{
			var pixbuf = ((GtkImage)handle).Pixbuf;
			if (pixbuf == null)
				return;
			if (x < 0 || y < 0 || x >= pixbuf.Width || y >= pixbuf.Height)
				return;

			var channels = pixbuf.NChannels;
			var offset = y * pixbuf.Rowstride + x * channels;
			var pixel = new byte[channels];
			pixel[0] = (byte)(color.Red * 255);
			pixel[1] = (byte)(color.Green * 255);
			pixel[2] = (byte)(color.Blue * 255);
			if (channels > 3)
				pixel[3] = (byte)(color.Alpha * 255);
			Marshal.Copy(pixel, 0, IntPtr.Add(pixbuf.Pixels, offset), channels);
		}

		public override Color GetBitmapPixel(object handle, int x, int y)
		{
			var pixbuf = ((GtkImage)handle).Pixbuf;
			if (pixbuf == null || x < 0 || y < 0 || x >= pixbuf.Width || y >= pixbuf.Height)
				return Color.FromBytes(0, 0, 0, 0);

			var channels = pixbuf.NChannels;
			var offset = y * pixbuf.Rowstride + x * channels;
			var pixel = new byte[channels];
			Marshal.Copy(IntPtr.Add(pixbuf.Pixels, offset), pixel, 0, channels);
			return channels > 3
				? Color.FromBytes(pixel[0], pixel[1], pixel[2], pixel[3])
				: Color.FromBytes(pixel[0], pixel[1], pixel[2]);
		}

		public override void Dispose(object backend)
		{
			(backend as GtkImage)?.Dispose();
		}

		static string GetFileType(ImageFileType type)
		{
			return type switch {
				ImageFileType.Bmp => "bmp",
				ImageFileType.Jpeg => "jpeg",
				ImageFileType.Png => "png",
				_ => throw new NotSupportedException()
			};
		}

		static GtkImage TryLoadThemeIcon(string iconName, int size, int scale)
		{
			var display = Gdk.Display.GetDefault();
			if (display == null)
				return null;
			var theme = Gtk.IconTheme.GetForDisplay(display);
			if (theme == null || !theme.HasIcon(iconName))
				return null;
			var paintable = theme.LookupIcon(iconName, null, size, scale, Gtk.TextDirection.Ltr, (Gtk.IconLookupFlags)0);
			if (paintable == null)
				return null;
			var file = paintable.GetFile();
			var path = file?.GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;
			return new GtkImage(Pixbuf.NewFromFile(path), iconName);
		}
	}

	public class GtkImage : IDisposable
	{
		readonly ImageDrawCallback drawCallback;
		readonly string iconName;
		public Pixbuf Pixbuf { get; }

		public GtkImage(Pixbuf pixbuf, string iconName = null)
		{
			Pixbuf = pixbuf;
			this.iconName = iconName;
		}

		public GtkImage(ImageDrawCallback drawCallback)
		{
			this.drawCallback = drawCallback;
		}

		public string IconName => iconName;

		public Size DefaultSize {
			get {
				if (Pixbuf == null)
					return Size.Zero;
				return new Size(Pixbuf.Width, Pixbuf.Height);
			}
		}

		public bool HasMultipleSizes => drawCallback != null;

		public void Draw(ApplicationContext context, Cairo.Context cr, double scaleFactor, double x, double y, ImageDescription idesc)
		{
			if (drawCallback != null) {
				var backend = new CairoContextBackend(scaleFactor) {
					Context = cr
				};
				var bounds = new Rectangle(x, y, idesc.Size.Width, idesc.Size.Height);
				if (context != null)
					context.InvokeUserCode(() => drawCallback(backend, bounds, idesc, context.Toolkit));
				else
					drawCallback(backend, bounds, idesc, Toolkit.CurrentEngine);
				return;
			}

			if (Pixbuf == null)
				return;
			cr.Save();
			cr.Translate(x, y);
			double sx = idesc.Size.Width / Pixbuf.Width;
			double sy = idesc.Size.Height / Pixbuf.Height;
			cr.Scale(sx, sy);
			Gdk.Functions.CairoSetSourcePixbuf(cr, Pixbuf, 0, 0);
			if (idesc.Alpha >= 1)
				cr.Paint();
			else
				cr.PaintWithAlpha(idesc.Alpha);
			cr.Restore();
		}

		public Gtk.Widget CreateWidget(ApplicationContext context, ImageDescription idesc)
		{
			if (Pixbuf != null) {
				var image = Gtk.Image.NewFromPaintable(Gdk.Texture.NewForPixbuf(Pixbuf));
				image.Show();
				return image;
			}

			if (drawCallback == null)
				return Gtk.Image.New();

			var area = Gtk.DrawingArea.New();
			area.SetDrawFunc((widget, cr, width, height) => {
				var desc = idesc;
				if (desc.Size.Width <= 0 || desc.Size.Height <= 0)
					desc.Size = new Size(width, height);
				Draw(context, cr, widget.ScaleFactor, 0, 0, desc);
			});
			if (idesc.Size.Width > 0 && idesc.Size.Height > 0)
				area.SetSizeRequest((int)Math.Ceiling(idesc.Size.Width), (int)Math.Ceiling(idesc.Size.Height));
			area.Show();
			return area;
		}

		public void Dispose()
		{
			Pixbuf?.Dispose();
		}
	}
}
