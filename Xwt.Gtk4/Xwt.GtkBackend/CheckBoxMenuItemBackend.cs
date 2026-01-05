using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class CheckBoxMenuItemBackend : ICheckBoxMenuItemBackend, IGtkMenuItemBackend
	{
		readonly Gtk.CheckButton checkButton;
		readonly Gtk.Box contentBox;
		readonly Gtk.Label label;
		Gtk.Widget imageWidget;
		Gtk.Image submenuArrow;
		IMenuItemEventSink eventSink;
		ApplicationContext context;
		IMenuBackend submenu;
		bool clickedEnabled;
		bool useMnemonic;
		bool internalToggle;

		public CheckBoxMenuItemBackend()
		{
			checkButton = Gtk.CheckButton.New();
			checkButton.Halign = Gtk.Align.Fill;
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
			checkButton.SetChild(contentBox);
			checkButton.Show();
		}

		public Gtk.Widget Widget => checkButton;

		public bool Checked {
			get { return checkButton.Active; }
			set {
				internalToggle = true;
				checkButton.Active = value;
				internalToggle = false;
			}
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked && !clickedEnabled) {
				checkButton.OnToggled += HandleToggled;
				clickedEnabled = true;
			}
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked && clickedEnabled) {
				checkButton.OnToggled -= HandleToggled;
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

		public void SetImage(ImageDescription image)
		{
			if (image.IsNull || image.Backend == null) {
				if (imageWidget != null)
					imageWidget.Visible = false;
				return;
			}

			var gtkImage = (GtkImage)image.Backend;
			if (gtkImage == null || (gtkImage.Pixbuf == null && !gtkImage.HasMultipleSizes)) {
				if (imageWidget != null)
					imageWidget.Visible = false;
				return;
			}

			var newWidget = gtkImage.CreateWidget(context, image);
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
			get { return checkButton.TooltipText ?? string.Empty; }
			set { checkButton.TooltipText = value ?? string.Empty; }
		}

		public bool UseMnemonic {
			get { return useMnemonic; }
			set {
				useMnemonic = value;
				label.UseUnderline = value;
			}
		}

		public bool Sensitive {
			get { return checkButton.Sensitive; }
			set { checkButton.Sensitive = value; }
		}

		public bool Visible {
			get { return checkButton.Visible; }
			set { checkButton.Visible = value; }
		}

		public void SetFormattedText(FormattedText text)
		{
			FormattedTextUtil.ApplyFormattedText(label, text);
		}

		public void Dispose()
		{
			if (clickedEnabled)
				checkButton.OnToggled -= HandleToggled;
			checkButton?.Dispose();
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

		void HandleToggled(object sender, EventArgs e)
		{
			if (internalToggle)
				return;
			if (submenu != null) {
				if (submenu is MenuBackend gtkMenu) {
					gtkMenu.AttachTo(checkButton);
					gtkMenu.Popup(checkButton, 0, checkButton.GetAllocatedHeight());
				}
				return;
			}
			if (context != null && eventSink != null)
				context.InvokeUserCode(eventSink.OnClicked);
			else
				eventSink?.OnClicked();
			CloseParentPopover(checkButton);
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
