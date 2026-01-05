using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ColorPickerBackend : WidgetBackend, IColorPickerBackend
	{
		Gtk.ColorButton button;

		public ColorPickerBackend()
		{
			button = Gtk.ColorButton.New();
			Widget = button;
			Widget.Show();
		}

		protected new Gtk.ColorButton Widget {
			get { return (Gtk.ColorButton)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IColorPickerEventSink EventSink => (IColorPickerEventSink)base.EventSink;

		public Xwt.Drawing.Color Color {
			get { return Widget.Rgba.ToXwtValue(); }
			set { Widget.Rgba = value.ToGtkValue(); }
		}

		public bool SupportsAlpha {
			get { return Widget.UseAlpha; }
			set { Widget.UseAlpha = value; }
		}

		public string Title {
			get { return Widget.Title ?? string.Empty; }
			set { Widget.Title = value ?? string.Empty; }
		}

		public void SetButtonStyle(ButtonStyle style)
		{
			Widget.RemoveCssClass("flat");
			switch (style) {
				case ButtonStyle.Flat:
				case ButtonStyle.Borderless:
					Widget.AddCssClass("flat");
					break;
			}
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is ColorPickerEvent ev && ev == ColorPickerEvent.ColorChanged)
				Widget.OnColorSet += HandleColorSet;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is ColorPickerEvent ev && ev == ColorPickerEvent.ColorChanged)
				Widget.OnColorSet -= HandleColorSet;
		}

		void HandleColorSet(Gtk.ColorButton sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(EventSink.OnColorChanged);
		}
	}
}
