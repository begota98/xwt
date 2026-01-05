using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class MenuItemBackend : IMenuItemBackend, IGtkMenuItemBackend
	{
		readonly Gtk.Button button;
		readonly Gtk.Box contentBox;
		readonly Gtk.Label label;
		Gtk.Widget imageWidget;
		Gtk.Image submenuArrow;
		IMenuItemEventSink eventSink;
		ApplicationContext context;
		IMenuBackend submenu;
		bool useMnemonic;
		bool clickedEnabled;

		public MenuItemBackend()
		{
			button = Gtk.Button.New();
			button.HasFrame = false;
			button.Halign = Gtk.Align.Fill;
			contentBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
			label = Gtk.Label.New(string.Empty);
			label.Hexpand = true;
			label.Halign = Gtk.Align.Start;
			label.Xalign = 0f;
			imageWidget = Gtk.Image.New();
			submenuArrow = Gtk.Image.NewFromIconName("pan-end-symbolic");
			submenuArrow.Visible = false;
			imageWidget.Visible = false;
			label.Visible = true;
			contentBox.Append(imageWidget);
			contentBox.Append(label);
			contentBox.Append(submenuArrow);
			button.SetChild(contentBox);
			button.Show();
		}

		public Gtk.Widget Widget => button;

		internal Gtk.Widget MenuItem => button;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked && !clickedEnabled) {
				button.OnClicked += HandleClicked;
				clickedEnabled = true;
			}
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked && clickedEnabled) {
				button.OnClicked -= HandleClicked;
				clickedEnabled = false;
			}
		}

		public void Initialize(IMenuItemEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void SetSubmenu(IMenuBackend menu)
		{
			submenu = menu;
			submenuArrow.Visible = submenu != null;
		}

		public void SetImage(ImageDescription imageDesc)
		{
			if (imageDesc.IsNull || imageDesc.Backend == null) {
				if (imageWidget != null)
					imageWidget.Visible = false;
				return;
			}

			var gtkImage = (GtkImage)imageDesc.Backend;
			if (gtkImage == null || (gtkImage.Pixbuf == null && !gtkImage.HasMultipleSizes)) {
				if (imageWidget != null)
					imageWidget.Visible = false;
				return;
			}

			var newWidget = gtkImage.CreateWidget(context, imageDesc);
			ReplaceImageWidget(newWidget);
			imageWidget.Visible = true;
		}

		public string Label {
			get { return label.Label_ ?? string.Empty; }
			set {
				label.UseMarkup = false;
				label.Label_ = value ?? string.Empty;
				label.SetAttributes(null);
			}
		}

		public string TooltipText {
			get { return button.TooltipText ?? string.Empty; }
			set { button.TooltipText = value ?? string.Empty; }
		}

		public bool UseMnemonic {
			get { return useMnemonic; }
			set {
				useMnemonic = value;
				label.UseUnderline = value;
			}
		}

		public bool Sensitive {
			get { return button.Sensitive; }
			set { button.Sensitive = value; }
		}

		public bool Visible {
			get { return button.Visible; }
			set { button.Visible = value; }
		}

		public void SetFormattedText(FormattedText text)
		{
			FormattedTextUtil.ApplyFormattedText(label, text);
		}

		public void Dispose()
		{
			if (clickedEnabled)
				button.OnClicked -= HandleClicked;
			button?.Dispose();
		}

		void ReplaceImageWidget(Gtk.Widget newWidget)
		{
			if (newWidget == null)
				return;
			if (imageWidget != null)
				contentBox.Remove(imageWidget);
			imageWidget = newWidget;
			contentBox.Prepend(imageWidget);
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (submenu != null) {
				if (submenu is MenuBackend gtkMenu) {
					gtkMenu.AttachTo(button);
					gtkMenu.Popup(button, 0, button.GetAllocatedHeight());
				}
				return;
			}
			if (context != null && eventSink != null)
				context.InvokeUserCode(eventSink.OnClicked);
			else
				eventSink?.OnClicked();
			CloseParentPopover(button);
		}

		static void CloseParentPopover(Gtk.Widget widget)
		{
			for (var parent = widget?.Parent; parent != null; parent = parent.Parent) {
				if (parent is Gtk.Popover popover) {
					popover.Popdown();
					return;
				}
			}
		}
	}
}
