using System;
using System.Threading;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class MenuButtonBackend : WidgetBackend, IMenuButtonBackend
	{
		readonly Gtk.MenuButton menuButton;
		IMenuButtonEventSink eventSink;
		bool isDefault;
		string labelText = string.Empty;
		bool useMnemonic;
		ImageDescription image;
		ContentPosition contentPosition;
		FormattedText formattedText;
		Gtk.Label labelWidget;
		Gtk.CssProvider labelColorProvider;
		string labelColorCssClass;
		static int labelColorClassId;
		Color? customLabelColor;
		GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs> rootNotifyHandler;

		public MenuButtonBackend()
		{
			menuButton = Gtk.MenuButton.New();
			menuButton.AlwaysShowArrow = true;
			Widget = menuButton;
			Widget.Show();
		}

		public Color LabelColor {
			get { return customLabelColor ?? GetThemeLabelColor(); }
			set {
				customLabelColor = value;
				ApplyLabelColor();
			}
		}

		public bool IsDefault {
			get { return isDefault; }
			set {
				isDefault = value;
				menuButton.ReceivesDefault = value;
				ApplyDefaultWidget();
			}
		}

		public void SetButtonStyle(ButtonStyle style)
		{
			switch (style) {
				case ButtonStyle.Flat:
					menuButton.HasFrame = true;
					menuButton.AddCssClass("flat");
					break;
				case ButtonStyle.Borderless:
					menuButton.HasFrame = false;
					menuButton.AddCssClass("flat");
					break;
				default:
					menuButton.HasFrame = true;
					if (menuButton.HasCssClass("flat"))
						menuButton.RemoveCssClass("flat");
					break;
			}
		}

		public void SetButtonType(ButtonType type)
		{
			menuButton.AlwaysShowArrow = type == ButtonType.DropDown;
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
			eventSink = (IMenuButtonEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is ButtonEvent be && be == ButtonEvent.Clicked)
				menuButton.OnActivate += HandleActivate;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is ButtonEvent be && be == ButtonEvent.Clicked)
				menuButton.OnActivate -= HandleActivate;
		}

		void HandleActivate(object sender, EventArgs e)
		{
			var menu = eventSink?.OnCreateMenu();
			if (menu is MenuBackend gtkMenu) {
				menuButton.MenuModel = gtkMenu.MenuModel;
				gtkMenu.AttachActionGroup(menuButton);
				menuButton.Popup();
			} else {
				menuButton.MenuModel = null;
			}
			ApplicationContext.InvokeUserCode(eventSink.OnClicked);
		}

		void ApplyDefaultWidget()
		{
			if (!isDefault) {
				RemoveRootNotify();
				return;
			}

			if (menuButton.Root is Gtk.Window window) {
				window.DefaultWidget = menuButton;
				RemoveRootNotify();
				return;
			}

			EnsureRootNotify();
		}

		void EnsureRootNotify()
		{
			if (rootNotifyHandler != null || menuButton == null)
				return;
			rootNotifyHandler = HandleRootNotify;
			menuButton.OnNotify += rootNotifyHandler;
		}

		void RemoveRootNotify()
		{
			if (rootNotifyHandler == null || menuButton == null)
				return;
			menuButton.OnNotify -= rootNotifyHandler;
			rootNotifyHandler = null;
		}

		void HandleRootNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null || args.Pspec.GetName() != "root")
				return;
			ApplyDefaultWidget();
		}

		void UpdateContent()
		{
			var text = formattedText?.Text ?? labelText ?? string.Empty;
			var hasText = !string.IsNullOrEmpty(text);
			GtkImage imageBackend = null;
			if (!image.IsNull)
				imageBackend = image.Backend as GtkImage;
			var hasImage = imageBackend != null && (imageBackend.Pixbuf != null || imageBackend.HasMultipleSizes);

			labelWidget = null;
			Gtk.Widget imageWidget = null;

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

			var content = ComposeContent(labelWidget, imageWidget, contentPosition);
			menuButton.Child = content;
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
				labelColorCssClass = "xwt-menu-button-label-" + Interlocked.Increment(ref labelColorClassId).ToString();
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

		Color GetThemeLabelColor()
		{
			var target = (Gtk.Widget)labelWidget ?? menuButton;
			if (target == null)
				return Colors.Black;
			var context = target.GetStyleContext();
			if (context == null)
				return Colors.Black;
			context.GetColor(out var color);
			return color.ToXwtValue();
		}

		public override void Dispose()
		{
			RemoveRootNotify();
			base.Dispose();
		}
	}
}
