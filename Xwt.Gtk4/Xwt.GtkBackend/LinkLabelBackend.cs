using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class LinkLabelBackend : WidgetBackend, ILinkLabelBackend
	{
		Gtk.Label label;
		FormattedText formatted;
		ILinkLabelEventSink eventSink;
		Uri uri;
		string text = string.Empty;
		Gtk.CssProvider textColorProvider;
		string textColorCssClass;
		static int textColorClassId;
		Color? customTextColor;
		bool navigateEnabled;

		public LinkLabelBackend()
		{
			label = Gtk.Label.New(string.Empty);
			label.Selectable = false;
			label.Xalign = 0f;
			label.Yalign = 0.5f;
			label.Halign = Gtk.Align.Start;
			label.Valign = Gtk.Align.Center;
			label.OnActivateLink += HandleActivateLink;
			Widget = label;
			Widget.Show();
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ILinkLabelEventSink)sink;
		}

		public string Text {
			get { return text; }
			set {
				formatted = null;
				text = value ?? string.Empty;
				UpdateLabel();
			}
		}

		public bool Selectable {
			get { return label.Selectable; }
			set { label.Selectable = value; }
		}

		public Color TextColor {
			get { return customTextColor ?? Colors.Black; }
			set {
				customTextColor = value;
				ApplyTextColor();
			}
		}

		public Alignment TextAlignment {
			get {
				double xalign = label.Xalign;
				if (xalign <= 0.01)
					return Alignment.Start;
				if (xalign >= 0.99)
					return Alignment.End;
				return Alignment.Center;
			}
			set {
				switch (value) {
					case Alignment.Start:
						label.Xalign = 0f;
						break;
					case Alignment.End:
						label.Xalign = 1f;
						break;
					default:
						label.Xalign = 0.5f;
						break;
				}
			}
		}

		public EllipsizeMode Ellipsize {
			get { return (EllipsizeMode)(int)label.Ellipsize; }
			set { label.Ellipsize = (Pango.EllipsizeMode)(int)value; }
		}

		public WrapMode Wrap {
			get { return (WrapMode)(int)label.WrapMode; }
			set { label.WrapMode = (Pango.WrapMode)(int)value; }
		}

		public void SetFormattedText(FormattedText text)
		{
			formatted = text;
			UpdateLabel();
		}

		public Uri Uri {
			get { return uri; }
			set {
				uri = value;
				UpdateLabel();
			}
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is LinkLabelEvent ev && ev == LinkLabelEvent.NavigateToUrl)
				navigateEnabled = true;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is LinkLabelEvent ev && ev == LinkLabelEvent.NavigateToUrl)
				navigateEnabled = false;
		}

		void UpdateLabel()
		{
			if (formatted != null) {
				FormattedTextUtil.ApplyFormattedText(label, formatted);
				return;
			}

			if (uri != null) {
				label.SetAttributes(null);
				label.UseMarkup = true;
				label.Label_ = $"<a href=\"{EscapeMarkup(uri.ToString())}\">{EscapeMarkup(text)}</a>";
				return;
			}

			label.SetAttributes(null);
			label.UseMarkup = false;
			label.Label_ = text ?? string.Empty;
		}

		bool HandleActivateLink(Gtk.Label sender, Gtk.Label.ActivateLinkSignalArgs args)
		{
			if (!navigateEnabled || eventSink == null || uri == null)
				return false;
			ApplicationContext.InvokeUserCode(() => eventSink.OnNavigateToUrl(uri));
			return true;
		}

		void ApplyTextColor()
		{
			if (!customTextColor.HasValue)
				return;

			if (textColorProvider == null) {
				textColorProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, textColorProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(textColorCssClass))
				textColorCssClass = "xwt-linklabel-text-" + System.Threading.Interlocked.Increment(ref textColorClassId).ToString();
			if (!label.HasCssClass(textColorCssClass))
				label.AddCssClass(textColorCssClass);

			var color = customTextColor.Value;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + textColorCssClass + " { color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			textColorProvider.LoadFromString(css);
		}

		static string EscapeMarkup(string value)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;
			return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
		}
	}
}
