using System;
using Xwt.Backends;
using Xwt.Drawing;
using Gio;
using GLib;

namespace Xwt.GtkBackend
{
	public class MenuItemBackend : IMenuItemBackend, IGtkMenuItemBackend
	{
		readonly string actionName;
		readonly SimpleAction action;
		IMenuItemEventSink eventSink;
		ApplicationContext context;
		MenuBackend menuHost;
		string label = string.Empty;
		string tooltip = string.Empty;
		bool useMnemonic = true;
		bool sensitive = true;
		bool visible = true;
		bool clickedEnabled;
		FormattedText formattedText;
		ImageDescription imageDesc = ImageDescription.Null;
		IMenuBackend submenu;

		public MenuItemBackend()
		{
			actionName = "item" + MenuActionIds.NextId().ToString();
			action = SimpleAction.New(actionName, null);
			action.Enabled = sensitive;
			action.OnActivate += HandleActivate;
		}

		public bool IsSeparator => false;

		public bool IsVisible => visible;

		public void Attach(MenuBackend menu)
		{
			menuHost = menu;
			menu?.RegisterAction(action);
		}

		public void Detach(MenuBackend menu)
		{
			menu?.UnregisterAction(actionName);
			if (menuHost == menu)
				menuHost = null;
		}

		public Gio.MenuItem BuildMenuItem(string actionGroupName)
		{
			var text = formattedText?.Text ?? label ?? string.Empty;
			string detailedAction = submenu == null ? actionGroupName + "." + actionName : null;
			var menuItem = Gio.MenuItem.New(text, detailedAction);
			if (submenu is MenuBackend gtkMenu)
				menuItem.SetLink("submenu", gtkMenu.MenuModel);
			ApplyCommonAttributes(menuItem);
			return menuItem;
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked)
				clickedEnabled = true;
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is MenuItemEvent ev && ev == MenuItemEvent.Clicked)
				clickedEnabled = false;
		}

		public void Initialize(IMenuItemEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void SetSubmenu(IMenuBackend menu)
		{
			submenu = menu;
			if (menuHost != null && submenu is MenuBackend gtkMenu)
				gtkMenu.ShareActionGroup(menuHost);
			NotifyMenuChanged();
		}

		public void SetImage(ImageDescription image)
		{
			imageDesc = image;
			NotifyMenuChanged();
		}

		public string Label {
			get { return label ?? string.Empty; }
			set {
				label = value ?? string.Empty;
				formattedText = null;
				NotifyMenuChanged();
			}
		}

		public string TooltipText {
			get { return tooltip ?? string.Empty; }
			set { tooltip = value ?? string.Empty; }
		}

		public bool UseMnemonic {
			get { return useMnemonic; }
			set {
				useMnemonic = value;
				NotifyMenuChanged();
			}
		}

		public bool Sensitive {
			get { return sensitive; }
			set {
				sensitive = value;
				action.Enabled = value;
				NotifyMenuChanged();
			}
		}

		public bool Visible {
			get { return visible; }
			set {
				visible = value;
				NotifyMenuChanged();
			}
		}

		public void SetFormattedText(FormattedText text)
		{
			formattedText = text;
			NotifyMenuChanged();
		}

		public void Dispose()
		{
			menuHost?.UnregisterAction(actionName);
			menuHost = null;
			action.OnActivate -= HandleActivate;
			action.Dispose();
		}

		void HandleActivate(SimpleAction sender, SimpleAction.ActivateSignalArgs args)
		{
			if (submenu != null || !clickedEnabled)
				return;
			if (context != null && eventSink != null)
				context.InvokeUserCode(eventSink.OnClicked);
			else
				eventSink?.OnClicked();
		}

		void ApplyCommonAttributes(Gio.MenuItem menuItem)
		{
			if (menuItem == null)
				return;

			menuItem.SetAttributeValue("use-underline", Variant.NewBoolean(useMnemonic));
			menuItem.SetAttributeValue("enabled", Variant.NewBoolean(sensitive));

			if (imageDesc.IsNull || imageDesc.Backend == null)
				return;
			if (imageDesc.Backend is GtkImage gtkImage) {
				var iconName = gtkImage.IconName;
				if (!string.IsNullOrEmpty(iconName)) {
					using var icon = Gio.ThemedIcon.NewWithDefaultFallbacks(iconName);
					menuItem.SetIcon(icon);
				}
			}
		}

		void NotifyMenuChanged()
		{
			menuHost?.Invalidate();
		}
	}
}
