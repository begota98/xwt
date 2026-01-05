using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class ImageViewBackend : WidgetBackend, IImageViewBackend
	{
		readonly Gtk.DrawingArea drawingArea;
		ImageDescription image;

		public ImageViewBackend()
		{
			drawingArea = Gtk.DrawingArea.New();
			drawingArea.SetDrawFunc(HandleDraw);
			Widget = drawingArea;
			Widget.Show();
		}

		public void SetImage(ImageDescription image)
		{
			this.image = image;
			if (!image.IsNull && image.Size.Width > 0 && image.Size.Height > 0)
				drawingArea.SetSizeRequest((int)Math.Ceiling(image.Size.Width), (int)Math.Ceiling(image.Size.Height));
			drawingArea.QueueResize();
			drawingArea.QueueDraw();
		}

		void HandleDraw(Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
		{
			if (image.IsNull || image.Backend == null)
				return;
			if (!(image.Backend is GtkImage gtkImage))
				return;
			var desc = image;
			if (desc.Size.Width <= 0 || desc.Size.Height <= 0) {
				if (gtkImage.Pixbuf != null)
					desc.Size = new Size(gtkImage.Pixbuf.Width, gtkImage.Pixbuf.Height);
				else
					desc.Size = new Size(width, height);
			}
			var x = Math.Max(0, (width - desc.Size.Width) / 2);
			var y = Math.Max(0, (height - desc.Size.Height) / 2);
			gtkImage.Draw(ApplicationContext, cr, area.ScaleFactor, x, y, desc);
		}
	}
}
