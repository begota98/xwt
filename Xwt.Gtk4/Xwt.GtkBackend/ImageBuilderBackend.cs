using System;
using GdkPixbuf;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	class GtkImageBuilderData
	{
		public int Width;
		public int Height;
		public ImageFormat Format;
	}

	public class GtkImageBuilderBackendHandler : ImageBuilderBackendHandler
	{
		public override object CreateImageBuilder(int width, int height, ImageFormat format)
		{
			return new GtkImageBuilderData {
				Width = width,
				Height = height,
				Format = format
			};
		}

		public override object CreateContext(object backend)
		{
			return backend;
		}

		public override object CreateImage(object backend)
		{
			var data = (GtkImageBuilderData)backend;
			bool hasAlpha = data.Format == ImageFormat.ARGB32;
			var pixbuf = Pixbuf.New(Colorspace.Rgb, hasAlpha, 8, data.Width, data.Height);
			return new GtkImage(pixbuf);
		}

		public override void Dispose(object backend)
		{
			if (backend is IDisposable disposable)
				disposable.Dispose();
		}
	}
}
