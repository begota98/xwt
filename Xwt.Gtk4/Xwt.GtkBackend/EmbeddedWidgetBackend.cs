using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class EmbeddedWidgetBackend : WidgetBackend, IEmbeddedWidgetBackend
	{
		Gtk.Box box;

		public EmbeddedWidgetBackend()
		{
			box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			box.Hexpand = true;
			box.Vexpand = true;
			Widget = box;
			Widget.Show();
		}

		protected new Gtk.Box Widget {
			get { return (Gtk.Box)base.Widget; }
			set { base.Widget = value; }
		}

		public void SetContent(object nativeWidget, bool reparent)
		{
			if (!(nativeWidget is Gtk.Widget widget))
				return;

			if (reparent && widget.Parent != null)
				widget.Unparent();

			var old = Widget.GetFirstChild();
			if (old != null)
				Widget.Remove(old);
			Widget.Append(widget);
		}
	}
}
