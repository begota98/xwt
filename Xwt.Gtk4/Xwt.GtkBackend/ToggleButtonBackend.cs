using System;
using System.Threading;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class ToggleButtonBackend : WidgetBackend, IToggleButtonBackend
	{
		readonly Gtk.ToggleButton button;
		IToggleButtonEventSink eventSink;
		bool isDefault;
		ImageDescription image;
		ContentPosition contentPosition;
		ButtonType buttonType;
		string labelText = string.Empty;
		bool useMnemonic;
		FormattedText formattedText;
		Gtk.Label labelWidget;
		Gtk.Widget imageWidget;
		Gtk.Image disclosureWidget;
		Gtk.CssProvider labelColorProvider;
		string labelColorCssClass;
		static int labelColorClassId;
		Color? customLabelColor;

		public ToggleButtonBackend()
		{
			button = Gtk.ToggleButton.New();
			Widget = button;
			Widget.Show();
		}

		public Color LabelColor {
			get { return customLabelColor ?? Colors.Black; }
			set {
				customLabelColor = value;
				ApplyLabelColor();
			}
		}

		public bool IsDefault {
			get { return isDefault; }
			set {
				isDefault = value;
				button.ReceivesDefault = value;
				ApplyDefaultWidget();
			}
		}

		public bool Active {
			get { return button.Active; }
			set { button.Active = value; }
		}

		public void SetButtonStyle(ButtonStyle style)
		{
			switch (style) {
				case ButtonStyle.Flat:
					button.HasFrame = true;
					button.AddCssClass("flat");
					break;
				case ButtonStyle.Borderless:
					button.HasFrame = false;
					button.AddCssClass("flat");
					break;
				default:
					button.HasFrame = true;
					if (button.HasCssClass("flat"))
						button.RemoveCssClass("flat");
					break;
			}
		}

		public void SetButtonType(ButtonType type)
		{
			buttonType = type;
			UpdateContent();
		}

		public void SetContent(string label, bool useMnemonic, ImageDescription image, ContentPosition position)
		{
			labelText = label ?? string.Empty;
			this.useMnemonic = useMnemonic;
			this.image = image;
			contentPosition = position;
			formattedText = null;
			UpdateContent();
		}

		public void SetFormattedText(FormattedText text)
		{
			formattedText = text;
			UpdateContent();
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (IToggleButtonEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is ButtonEvent be && be == ButtonEvent.Clicked)
				button.OnClicked += HandleClicked;
			if (eventId is ToggleButtonEvent te && te == ToggleButtonEvent.Toggled)
				button.OnToggled += HandleToggled;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is ButtonEvent be && be == ButtonEvent.Clicked)
				button.OnClicked -= HandleClicked;
			if (eventId is ToggleButtonEvent te && te == ToggleButtonEvent.Toggled)
				button.OnToggled -= HandleToggled;
		}

		void HandleClicked(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnClicked);
		}

		void HandleToggled(object sender, EventArgs e)
		{
			UpdateDisclosureIndicator();
			ApplicationContext.InvokeUserCode(eventSink.OnToggled);
		}

		void ApplyDefaultWidget()
		{
			if (!isDefault)
				return;
			if (button.Root is Gtk.Window window)
				window.DefaultWidget = button;
		}

		void UpdateContent()
		{
			Gtk.Widget newContent;
			if (buttonType == ButtonType.Disclosure) {
				disclosureWidget = CreateDisclosureWidget(button.Active);
				newContent = disclosureWidget;
			} else {
				var text = formattedText?.Text ?? labelText ?? string.Empty;
				var hasText = !string.IsNullOrEmpty(text);
				GtkImage imageBackend = null;
				if (!image.IsNull)
					imageBackend = image.Backend as GtkImage;
				var hasImage = imageBackend != null && (imageBackend.Pixbuf != null || imageBackend.HasMultipleSizes);

				if (!hasText && !hasImage && buttonType == ButtonType.Help)
					text = "?";

				labelWidget = null;
				imageWidget = null;
				disclosureWidget = null;

				if (hasText) {
					labelWidget = Gtk.Label.New(text);
					labelWidget.UseUnderline = formattedText == null && useMnemonic;
					labelWidget.Show();
					if (formattedText != null)
						FormattedTextUtil.ApplyFormattedText(labelWidget, formattedText);
				}

				if (hasImage) {
					imageWidget = imageBackend.CreateWidget(ApplicationContext, image);
				}

				newContent = ComposeContent(labelWidget, imageWidget, contentPosition);

				if (buttonType == ButtonType.DropDown)
					newContent = AddDropDownIndicator(newContent);
			}

			button.SetChild(newContent);
			ApplyLabelColor();
		}

		Gtk.Widget ComposeContent(Gtk.Label label, Gtk.Widget image, ContentPosition position)
		{
			if (label != null && image == null)
				return label;
			if (label == null && image != null)
				return image;
			if (label == null && image == null)
				return Gtk.Label.New(string.Empty);

			var isHorizontal = position == ContentPosition.Left || position == ContentPosition.Right;
			var box = Gtk.Box.New(isHorizontal ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical, 6);
			if (position == ContentPosition.Left || position == ContentPosition.Top) {
				box.Append(image);
				box.Append(label);
			} else {
				box.Append(label);
				box.Append(image);
			}
			box.Show();
			return box;
		}

		Gtk.Widget AddDropDownIndicator(Gtk.Widget mainContent)
		{
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
			var arrow = Gtk.Image.NewFromIconName("pan-down-symbolic");
			arrow.Show();
			if (mainContent != null)
				box.Append(mainContent);
			box.Append(arrow);
			box.Show();
			return box;
		}

		Gtk.Image CreateDisclosureWidget(bool expanded)
		{
			var iconName = expanded ? "pan-up-symbolic" : "pan-down-symbolic";
			var arrow = Gtk.Image.NewFromIconName(iconName);
			arrow.Show();
			return arrow;
		}

		void UpdateDisclosureIndicator()
		{
			if (buttonType != ButtonType.Disclosure || disclosureWidget == null)
				return;
			disclosureWidget.IconName = button.Active ? "pan-up-symbolic" : "pan-down-symbolic";
		}

		void ApplyLabelColor()
		{
			if (!customLabelColor.HasValue || labelWidget == null)
				return;

			if (labelColorProvider == null) {
				labelColorProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, labelColorProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(labelColorCssClass))
				labelColorCssClass = "xwt-toggle-button-label-" + Interlocked.Increment(ref labelColorClassId).ToString();
			if (!labelWidget.HasCssClass(labelColorCssClass))
				labelWidget.AddCssClass(labelColorCssClass);

			var color = customLabelColor.Value;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + labelColorCssClass + " { color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			labelColorProvider.LoadFromString(css);
		}
	}
}
