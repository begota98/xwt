using System;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class PopoverBackend : IPopoverBackend
	{
		Gtk.Popover popover;
		IPopoverEventSink sink;
		Color backgroundColor;
		ApplicationContext context;
		bool closedEnabled;
		Gtk.CssProvider backgroundProvider;
		string backgroundCssClass;
		static int backgroundClassId;

		public Color BackgroundColor {
			get { return backgroundColor; }
			set {
				backgroundColor = value;
				ApplyBackground();
			}
		}

		public void Initialize(IPopoverEventSink sink)
		{
			this.sink = sink;
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is PopoverEvent ev && ev == PopoverEvent.Closed)
				closedEnabled = true;
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is PopoverEvent ev && ev == PopoverEvent.Closed)
				closedEnabled = false;
		}

		public void Show(Popover.Position arrowPosition, Widget referenceWidget, Rectangle positionRect, Widget child)
		{
			if (popover == null) {
				popover = Gtk.Popover.New();
				popover.HasArrow = true;
				popover.OnClosed += HandleClosed;
			}
			ApplyBackground();

			var parent = referenceWidget != null ? ((IGtkWidgetBackend)Toolkit.GetBackend(referenceWidget)).Widget : null;
			if (parent != null)
				popover.SetParent(parent);

			var childWidget = child != null ? ((IGtkWidgetBackend)Toolkit.GetBackend(child)).Widget : null;
			if (childWidget != null) {
				if (childWidget.Parent != null)
					childWidget.Unparent();
				popover.SetChild(childWidget);
			}

			popover.SetPosition(ToGtkPosition(arrowPosition));
			popover.SetPointingTo(new Gdk.Rectangle {
				X = (int)positionRect.X,
				Y = (int)positionRect.Y,
				Width = (int)positionRect.Width,
				Height = (int)positionRect.Height
			});

			popover.Popup();
		}

		public void Hide()
		{
			popover?.Popdown();
		}

		public void Dispose()
		{
			if (popover != null) {
				popover.OnClosed -= HandleClosed;
				popover.Unparent();
				popover.Dispose();
				popover = null;
			}
		}

		void HandleClosed(Gtk.Popover sender, EventArgs e)
		{
			if (!closedEnabled)
				return;
			sink?.OnClosed();
		}

		void ApplyBackground()
		{
			if (popover == null)
				return;

			if (backgroundProvider == null) {
				backgroundProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, backgroundProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(backgroundCssClass))
				backgroundCssClass = "xwt-popover-bg-" + System.Threading.Interlocked.Increment(ref backgroundClassId).ToString();
			if (!popover.HasCssClass(backgroundCssClass))
				popover.AddCssClass(backgroundCssClass);

			var r = (int)(backgroundColor.Red * 255);
			var g = (int)(backgroundColor.Green * 255);
			var b = (int)(backgroundColor.Blue * 255);
			var a = backgroundColor.Alpha.ToString("0.###");
			var css = "." + backgroundCssClass + " { background-color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			backgroundProvider.LoadFromString(css);
		}

		static Gtk.PositionType ToGtkPosition(Popover.Position position)
		{
			switch (position) {
				case Popover.Position.Bottom:
					return Gtk.PositionType.Bottom;
				default:
					return Gtk.PositionType.Top;
			}
		}
	}
}
