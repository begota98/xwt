using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class CustomWidgetBackend : WidgetBackend, ICustomWidgetBackend
	{
		WidgetBackend childBackend;

		public CustomWidgetBackend()
		{
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			box.Hexpand = true;
			box.Vexpand = true;
			Widget = box;
			Widget.Show();
		}

		protected new Gtk.Box Widget {
			get { return (Gtk.Box)base.Widget; }
			set { base.Widget = value; }
		}

		public override Size GetPreferredSize(SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			if (childBackend == null)
				return base.GetPreferredSize(widthConstraint, heightConstraint);
			return childBackend.Frontend.Surface.GetPreferredSize(widthConstraint, heightConstraint, true);
		}

		public void SetContent(IWidgetBackend widget)
		{
			childBackend = widget as WidgetBackend;

			var newWidget = ((IGtkWidgetBackend)widget).Widget;
			var oldWidget = Widget.GetFirstChild();
			if (oldWidget == null) {
				Widget.Append(newWidget);
				return;
			}

			Widget.Remove(oldWidget);
			Widget.Append(newWidget);
			WidgetBackend.ApplyChildPlacement(widget);
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}
	}
}
