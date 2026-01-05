using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class DesignerSurfaceBackend : WidgetBackend, IDesignerSurfaceBackend
	{
		Gtk.Box box;

		public DesignerSurfaceBackend()
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

		public void Load(Widget w)
		{
			var widget = w != null ? ((IGtkWidgetBackend)Toolkit.GetBackend(w)).Widget : null;
			if (widget == null)
				return;
			if (widget.Parent != null)
				widget.Unparent();

			var old = Widget.GetFirstChild();
			if (old != null)
				Widget.Remove(old);
			Widget.Append(widget);
		}
	}
}
