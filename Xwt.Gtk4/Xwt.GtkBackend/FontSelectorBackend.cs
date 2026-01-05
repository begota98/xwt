using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class FontSelectorBackend : WidgetBackend, IFontSelectorBackend
	{
		Gtk.FontChooserWidget chooser;

		public FontSelectorBackend()
		{
			chooser = Gtk.FontChooserWidget.New();
			Widget = chooser;
			Widget.Show();
		}

		protected new Gtk.FontChooserWidget Widget {
			get { return (Gtk.FontChooserWidget)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IFontSelectorEventSink EventSink => (IFontSelectorEventSink)base.EventSink;

		public Font SelectedFont {
			get {
				var name = Widget.Font ?? string.Empty;
				return Xwt.Drawing.Font.FromName(name);
			}
			set {
				Widget.Font = value.ToString();
			}
		}

		public string PreviewText {
			get { return Widget.PreviewText ?? string.Empty; }
			set { Widget.PreviewText = value ?? string.Empty; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is FontSelectorEvent ev && ev == FontSelectorEvent.FontChanged)
				Widget.OnNotify += HandleNotify;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is FontSelectorEvent ev && ev == FontSelectorEvent.FontChanged)
				Widget.OnNotify -= HandleNotify;
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "font" && name != "font-desc")
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnFontChanged);
		}
	}
}
