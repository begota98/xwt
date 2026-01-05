using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ColorSelectorBackend : WidgetBackend, IColorSelectorBackend
	{
		Gtk.ColorChooserWidget chooser;
		Xwt.Drawing.Color textColor;
		bool textColorSet;

		public ColorSelectorBackend()
		{
			chooser = Gtk.ColorChooserWidget.New();
			Widget = chooser;
			Widget.Show();
		}

		protected new Gtk.ColorChooserWidget Widget {
			get { return (Gtk.ColorChooserWidget)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IColorSelectorEventSink EventSink => (IColorSelectorEventSink)base.EventSink;

		public Xwt.Drawing.Color TextColor {
			get { return textColorSet ? textColor : Xwt.Drawing.Colors.Transparent; }
			set {
				textColor = value;
				textColorSet = true;
			}
		}

		public Xwt.Drawing.Color Color {
			get { return Widget.Rgba.ToXwtValue(); }
			set { Widget.Rgba = value.ToGtkValue(); }
		}

		public bool SupportsAlpha {
			get { return Widget.UseAlpha; }
			set { Widget.UseAlpha = value; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is ColorSelectorEvent ev && ev == ColorSelectorEvent.ColorChanged)
				Widget.OnNotify += HandleNotify;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is ColorSelectorEvent ev && ev == ColorSelectorEvent.ColorChanged)
				Widget.OnNotify -= HandleNotify;
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null || args.Pspec.GetName() != "rgba")
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnColorChanged);
		}
	}
}
