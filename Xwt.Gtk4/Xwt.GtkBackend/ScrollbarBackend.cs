using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ScrollbarBackend : WidgetBackend, IScrollbarBackend
	{
		Gtk.Scrollbar scrollbar;
		Gtk.Adjustment adjustment;

		public void Initialize(Orientation dir)
		{
			var orientation = dir == Orientation.Horizontal ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical;
			adjustment = Gtk.Adjustment.New(0, 0, 100, 1, 10, 0);
			ScrollAdjustmentBackend.AttachGuards(adjustment);
			scrollbar = Gtk.Scrollbar.New(orientation, adjustment);
			Widget = scrollbar;
			Widget.Show();
		}

		public IScrollAdjustmentBackend CreateAdjustment()
		{
			return new ScrollAdjustmentBackend(adjustment);
		}
	}
}
