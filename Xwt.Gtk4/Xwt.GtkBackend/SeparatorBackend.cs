using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SeparatorBackend : WidgetBackend, ISeparatorBackend
	{
		public void Initialize(Orientation dir)
		{
			var orientation = dir == Orientation.Horizontal ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical;
			Widget = Gtk.Separator.New(orientation);
			Widget.Show();
		}
	}
}
