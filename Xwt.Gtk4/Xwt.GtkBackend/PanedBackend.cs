using System;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class PanedBackend : WidgetBackend, IPanedBackend
	{
		GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs> positionChangedHandler;
		IWidgetBackend startChildBackend;
		IWidgetBackend endChildBackend;

		protected new Gtk.Paned Widget {
			get { return (Gtk.Paned)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IPanedEventSink EventSink {
			get { return (IPanedEventSink)base.EventSink; }
		}

		public void Initialize(Orientation dir)
		{
			Widget = Gtk.Paned.New(dir == Orientation.Horizontal ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical);
			Widget.Hexpand = true;
			Widget.Vexpand = true;
			Widget.Visible = true;
		}

		public void SetPanel(int panel, IWidgetBackend widget, bool resize, bool shrink)
		{
			if (panel == 1)
				startChildBackend = widget;
			else
				endChildBackend = widget;

			var gtkWidget = widget != null ? ((IGtkWidgetBackend)widget).Widget : null;
			if (gtkWidget != null)
				gtkWidget.Visible = true;
			if (gtkWidget != null) {
				gtkWidget.Hexpand = true;
				gtkWidget.Vexpand = true;
			}
			if (widget != null)
				WidgetBackend.ApplyChildPlacement(widget);

			if (panel == 1) {
				Widget.StartChild = gtkWidget;
				Widget.ResizeStartChild = resize;
				Widget.ShrinkStartChild = shrink;
			} else {
				Widget.EndChild = gtkWidget;
				Widget.ResizeEndChild = resize;
				Widget.ShrinkEndChild = shrink;
			}
		}

		public void RemovePanel(int panel)
		{
			if (panel == 1)
				startChildBackend = null;
			else
				endChildBackend = null;
			if (panel == 1)
				Widget.StartChild = null;
			else
				Widget.EndChild = null;
		}

		public void UpdatePanel(int panel, bool resize, bool shrink)
		{
			if (panel == 1) {
				Widget.ResizeStartChild = resize;
				Widget.ShrinkStartChild = shrink;
			} else {
				Widget.ResizeEndChild = resize;
				Widget.ShrinkEndChild = shrink;
			}
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}

		public double Position {
			get { return Widget.Position; }
			set { Widget.Position = (int)value; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (Widget == null)
				return;
			if (!(eventId is PanedEvent) || (PanedEvent)eventId != PanedEvent.PositionChanged)
				return;
			if (positionChangedHandler == null) {
				positionChangedHandler = (sender, args) => {
					if (args?.Pspec == null || args.Pspec.GetName() != "position")
						return;
					ApplicationContext.InvokeUserCode(EventSink.OnPositionChanged);
				};
			}
			Widget.OnNotify += positionChangedHandler;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (Widget == null)
				return;
			if (!(eventId is PanedEvent) || (PanedEvent)eventId != PanedEvent.PositionChanged || positionChangedHandler == null)
				return;
			Widget.OnNotify -= positionChangedHandler;
		}

		public override Size GetPreferredSize(SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			var startSize = GetChildPreferredSize(startChildBackend);
			var endSize = GetChildPreferredSize(endChildBackend);

			if (Widget != null && Widget.GetOrientation() == Gtk.Orientation.Horizontal)
				return new Size(startSize.Width + endSize.Width, Math.Max(startSize.Height, endSize.Height));
			return new Size(Math.Max(startSize.Width, endSize.Width), startSize.Height + endSize.Height);
		}

		static Size GetChildPreferredSize(IWidgetBackend backend)
		{
			if (backend is WidgetBackend widgetBackend)
				return widgetBackend.GetPreferredSize(SizeConstraint.Unconstrained, SizeConstraint.Unconstrained);
			return Size.Zero;
		}
	}
}
