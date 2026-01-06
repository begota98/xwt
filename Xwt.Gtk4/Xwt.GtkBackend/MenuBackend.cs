using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using Xwt.Backends;
using Gio;
using GLib;

namespace Xwt.GtkBackend
{
	internal interface IGtkMenuItemBackend
	{
		bool IsSeparator { get; }
		bool IsVisible { get; }
		Gio.MenuItem BuildMenuItem(string actionGroupName);
		void Attach(MenuBackend menu);
		void Detach(MenuBackend menu);
	}

	public class MenuBackend : IMenuBackend
	{
		static readonly bool UseApplicationActions = true;
		static readonly bool TraceMenuPopup = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_MENU_TRACE"), "1", StringComparison.Ordinal);
		const string ApplicationActionGroupName = "app";
		static readonly ConditionalWeakTable<Gtk.Window, Gtk.MenuButton> contextMenuAnchors = new ConditionalWeakTable<Gtk.Window, Gtk.MenuButton>();
		static int groupId;
		readonly List<IGtkMenuItemBackend> items = new List<IGtkMenuItemBackend>();
		readonly string actionGroupName = "xwtmenu" + System.Threading.Interlocked.Increment(ref groupId).ToString();
		Gio.Menu menuModel = Gio.Menu.New();
		Gtk.PopoverMenu popover;
		Gtk.PopoverMenuBar menuBar;
		readonly List<SimpleAction> actions = new List<SimpleAction>();
		Gio.SimpleActionGroup actionGroup;
		Gio.SimpleActionGroup sharedActionGroup;
		string sharedActionGroupName;
		ApplicationContext context;
		Pango.FontDescription customFont;
		Gtk.CssProvider fontProvider;
		string fontCssClass;
		static int fontClassId;

		public object Font {
			get {
				if (customFont != null)
					return customFont;
				Gtk.Widget fontWidget = (Gtk.Widget)menuBar ?? popover;
				if (fontWidget != null) {
					using var layout = fontWidget.CreatePangoLayout(string.Empty);
					var desc = layout.GetFontDescription();
					if (desc != null)
						return desc;
				}
				var fallback = Pango.FontDescription.New();
				fallback.SetFamily("Sans");
				fallback.SetSize((int)(10 * Pango.Constants.SCALE));
				return fallback;
			}
			set {
				customFont = value as Pango.FontDescription;
				ApplyFont();
			}
		}

		internal Gio.Menu MenuModel => menuModel;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
		}

		public void DisableEvent(object eventId)
		{
		}

		public void InsertItem(int index, IMenuItemBackend menuItem)
		{
			if (!(menuItem is IGtkMenuItemBackend gtkItem))
				return;
			if (index < 0 || index > items.Count)
				items.Add(gtkItem);
			else
				items.Insert(index, gtkItem);
			gtkItem.Attach(this);
			RebuildMenuModel();
		}

		public void RemoveItem(IMenuItemBackend menuItem)
		{
			if (!(menuItem is IGtkMenuItemBackend gtkItem))
				return;
			if (!items.Remove(gtkItem))
				return;
			gtkItem.Detach(this);
			RebuildMenuModel();
		}

		public void Popup()
		{
			EnsurePopover();
			if (popover == null)
				return;
			var window = GetActiveWindow();
			if (WidgetBackend.TryGetLastButtonPress(out var clickWidget, out var clickX, out var clickY)) {
				TraceWidget("menu popup last-click widget", clickWidget);
				if (TryPopupWithAnchor(window, clickWidget, clickX, clickY))
					return;
				Trace("menu popup last-click fallback to pointer position");
			}
			var display = Gdk.Display.GetDefault();
			var seat = display?.GetDefaultSeat();
			var pointer = seat?.GetPointer();
			Gtk.Widget anchorParent = window ?? GetRootWidget();
			double anchorX = 0;
			double anchorY = 0;
			if (pointer != null) {
				pointer.GetSurfaceAtPosition(out double x, out double y);
				anchorX = x;
				anchorY = y;
			}
			if (TryPopupWithAnchor(window, null, anchorX, anchorY))
				return;
			TraceWidget("menu popup anchor parent (pointer)", anchorParent);
			if (!EnsurePopoverParent(anchorParent)) {
				Trace("menu popup aborted: no valid parent");
				return;
			}
			popover.Position = Gtk.PositionType.Bottom;
			popover.PointingTo = new Gdk.Rectangle { X = (int)anchorX, Y = (int)anchorY, Width = 1, Height = 1 };
			TraceWidget("menu popup parent (pointer)", popover.Parent);
			popover.Popup();
		}

		public void Popup(IWidgetBackend widget, double x, double y)
		{
			if (widget == null)
				return;
			EnsurePopover();
			if (popover == null)
				return;
			var parent = ((IGtkWidgetBackend)widget).Widget;
			var window = parent?.GetRoot() as Gtk.Window ?? GetActiveWindow();
			if (TryPopupWithAnchor(window, parent, x, y))
				return;
			TraceWidget("menu popup explicit parent", parent);
			if (!EnsurePopoverParent(parent)) {
				Trace("menu popup aborted: no valid parent for explicit widget");
				return;
			}
			popover.Position = Gtk.PositionType.Bottom;
			popover.PointingTo = new Gdk.Rectangle { X = (int)x, Y = (int)y, Width = 1, Height = 1 };
			TraceWidget("menu popup parent (explicit)", popover.Parent);
			popover.Popup();
		}

		internal void AttachActionGroup(Gtk.Widget widget)
		{
			if (widget == null)
				return;
			if (UseApplicationActions)
				return;
			EnsureActionGroup();
			widget.InsertActionGroup(GetActionGroupName(), GetActionGroup());
		}

		internal void RegisterAction(Gio.SimpleAction action)
		{
			if (action == null)
				return;
			if (UseApplicationActions && GtkEngine.Application != null) {
				GtkEngine.Application.AddAction(action);
				actions.Add(action);
				return;
			}
			EnsureActionGroup();
			actions.Add(action);
			GetActionGroup().AddAction(action);
		}

		internal void UnregisterAction(string actionName)
		{
			if (string.IsNullOrEmpty(actionName))
				return;
			if (UseApplicationActions && GtkEngine.Application != null) {
				GtkEngine.Application.RemoveAction(actionName);
				actions.RemoveAll(action => action.GetName() == actionName);
				return;
			}
			var group = GetActionGroup();
			if (group == null)
				return;
			group.RemoveAction(actionName);
			actions.RemoveAll(action => action.GetName() == actionName);
		}

		internal void Invalidate()
		{
			RebuildMenuModel();
		}

		internal void ShareActionGroup(MenuBackend owner)
		{
			if (owner == null)
				return;
			if (UseApplicationActions)
				return;
			owner.EnsureActionGroup();
			var newGroup = owner.GetActionGroup();
			var newName = owner.GetActionGroupName();
			if (newGroup == null || newGroup == sharedActionGroup)
				return;

			var oldGroup = GetActionGroup();
			var oldName = GetActionGroupName();
			if (oldGroup != null) {
				foreach (var action in actions)
					oldGroup.RemoveAction(action.GetName());
			}

			sharedActionGroup = newGroup;
			sharedActionGroupName = newName;
			foreach (var action in actions)
				sharedActionGroup.AddAction(action);

			if (menuBar != null) {
				if (!string.IsNullOrEmpty(oldName) && oldName != newName)
					menuBar.InsertActionGroup(oldName, null);
				menuBar.InsertActionGroup(newName, sharedActionGroup);
			}
			if (popover != null) {
				if (!string.IsNullOrEmpty(oldName) && oldName != newName)
					popover.InsertActionGroup(oldName, null);
				popover.InsertActionGroup(newName, sharedActionGroup);
			}
		}

		internal Gtk.Widget GetMenuBarWidget()
		{
			EnsureMenuBar();
			return menuBar;
		}

		internal Gtk.Widget GetAccessibleWidget()
		{
			return menuBar ?? (Gtk.Widget)popover;
		}

		void EnsureMenuBar()
		{
			if (menuBar == null) {
				menuBar = Gtk.PopoverMenuBar.NewFromModel(menuModel);
				menuBar.Hexpand = true;
				menuBar.Vexpand = false;
				AttachActionGroup(menuBar);
				ApplyFontTo(menuBar);
				menuBar.Show();
				return;
			}

			menuBar.MenuModel = menuModel;
		}

		void EnsurePopover()
		{
			if (popover != null) {
				popover.MenuModel = menuModel;
				return;
			}

			popover = Gtk.PopoverMenu.NewFromModel(menuModel);
			popover.HasArrow = false;
			AttachActionGroup(popover);
			ApplyFontTo(popover);
		}

		void EnsureActionGroup()
		{
			if (UseApplicationActions)
				return;
			if (sharedActionGroup != null)
				return;
			if (actionGroup != null)
				return;
			actionGroup = Gio.SimpleActionGroup.New();
			if (menuBar != null)
				menuBar.InsertActionGroup(GetActionGroupName(), actionGroup);
			if (popover != null)
				popover.InsertActionGroup(GetActionGroupName(), actionGroup);
		}

		bool EnsurePopoverParent(Gtk.Widget parent)
		{
			if (popover == null)
				return false;
			if (parent == null)
				parent = GetRootWidget();
			if (parent == null)
				return false;
			var root = parent.GetRoot();
			if (root == null) {
				TraceWidget("menu popup parent missing root", parent);
				parent = GetRootWidget();
				if (parent == null)
					return false;
			}
			if (popover.Parent != null && !ReferenceEquals(popover.Parent, parent))
				popover.Unparent();
			if (popover.Parent == null)
				popover.SetParent(parent);
			popover.Show();
			if (popover.Parent == null) {
				Trace("menu popup parent still null after SetParent");
				return false;
			}
			if (popover.Parent.GetRoot() == null) {
				TraceWidget("menu popup parent root still null after SetParent", popover.Parent);
				return false;
			}
			return true;
		}

		Gtk.Window GetActiveWindow()
		{
			var activeWindow = GtkEngine.Application?.ActiveWindow;
			if (activeWindow != null)
				return activeWindow;
			var windows = GtkEngine.Application?.GetWindows();
			if (windows != null && GLib.List.Length(windows) > 0) {
				var handle = GLib.List.NthData(windows, 0);
				if (handle != IntPtr.Zero)
					return (Gtk.Window)GObject.Internal.InstanceWrapper.WrapHandle<Gtk.Window>(handle, false);
			}
			return null;
		}

		Gtk.Widget GetRootWidget()
		{
			var window = GetActiveWindow();
			if (window != null)
				return window;
			return null;
		}

		internal static void RegisterContextMenuAnchor(Gtk.Window window, Gtk.MenuButton anchor)
		{
			if (window == null || anchor == null)
				return;
			lock (contextMenuAnchors)
				contextMenuAnchors.Remove(window);
			contextMenuAnchors.Add(window, anchor);
		}

		static Gtk.MenuButton GetContextMenuAnchor(Gtk.Window window)
		{
			if (window == null)
				return null;
			if (contextMenuAnchors.TryGetValue(window, out var anchor))
				return anchor;
			return null;
		}

		bool TryPopupWithAnchor(Gtk.Window window, Gtk.Widget clickWidget, double x, double y)
		{
			var anchor = GetContextMenuAnchor(window);
			if (anchor == null)
				return false;
			var anchorParent = anchor.Parent;
			if (anchorParent == null) {
				Trace("menu popup anchor has no parent");
				return false;
			}
			if (popover.Parent != null && !ReferenceEquals(popover.Parent, anchor))
				popover.Unparent();
			if (!ReferenceEquals(anchor.Popover, popover))
				anchor.Popover = popover;
			double anchorX = x;
			double anchorY = y;
			if (clickWidget != null) {
				try {
					clickWidget.TranslateCoordinates(anchorParent, x, y, out anchorX, out anchorY);
				} catch {
					Trace("menu popup failed to translate click coordinates to anchor parent");
				}
			}
			anchor.MarginStart = (int)Math.Round(anchorX);
			anchor.MarginTop = (int)Math.Round(anchorY);
			TraceWidget("menu popup anchor widget", anchor);
			popover.Position = Gtk.PositionType.Bottom;
			popover.PointingTo = new Gdk.Rectangle { X = 0, Y = 0, Width = 1, Height = 1 };
			TraceWidget("menu popup parent (anchor)", popover.Parent);
			anchor.Popup();
			return true;
		}

		void Trace(string message)
		{
			if (!TraceMenuPopup)
				return;
			Console.WriteLine($"[Xwt.Gtk4][Menu] {message}");
		}

		void TraceWidget(string label, Gtk.Widget widget)
		{
			if (!TraceMenuPopup)
				return;
			if (widget == null) {
				Trace(label + ": <null>");
				return;
			}
			var root = widget.GetRoot() as Gtk.Widget;
			var native = widget.GetNative();
			var handle = widget.Handle.DangerousGetHandle();
			var parent = widget.GetParent();
			Trace(label + ": type=" + widget.GetType().Name +
				" handle=0x" + handle.ToString("x") +
				" root=" + (root == null ? "null" : root.GetType().Name) +
				" native=" + (native == null ? "null" : native.GetType().Name) +
				" visible=" + widget.Visible +
				" parent=" + (parent == null ? "null" : parent.GetType().Name));
		}

		void RebuildMenuModel()
		{
			var root = Gio.Menu.New();
			var section = Gio.Menu.New();
			foreach (var item in items) {
				if (item == null || !item.IsVisible)
					continue;
				if (item.IsSeparator) {
					if (section.GetNItems() > 0) {
						root.AppendSection(null, section);
						section = Gio.Menu.New();
					}
					continue;
				}
				var menuItem = item.BuildMenuItem(GetActionGroupName());
				if (menuItem != null)
					section.AppendItem(menuItem);
			}
			if (section.GetNItems() > 0)
				root.AppendSection(null, section);

			menuModel = root;
			if (menuBar != null)
				menuBar.MenuModel = menuModel;
			if (popover != null)
				popover.MenuModel = menuModel;
		}

		void ApplyFont()
		{
			if (customFont == null)
				return;

			if (fontProvider == null) {
				fontProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, fontProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(fontCssClass))
				fontCssClass = "xwt-menu-font-" + Interlocked.Increment(ref fontClassId).ToString();

			var family = customFont.GetFamily() ?? "Sans";
			var sizePx = customFont.GetSize() / (double)Pango.Constants.SCALE;
			var weight = customFont.GetWeight();
			var style = customFont.GetStyle();
			string styleCss = style == Pango.Style.Italic ? "italic" : style == Pango.Style.Oblique ? "oblique" : "normal";
			string css = "." + fontCssClass + " { font-family: " + family + "; font-size: " + sizePx.ToString("0.###") + "px; font-style: " + styleCss + "; font-weight: " + (int)weight + "; }";
			fontProvider.LoadFromString(css);

			ApplyFontTo(menuBar);
			ApplyFontTo(popover);
		}

		void ApplyFontTo(Gtk.Widget widget)
		{
			if (widget == null || string.IsNullOrEmpty(fontCssClass))
				return;
			if (!widget.HasCssClass(fontCssClass))
				widget.AddCssClass(fontCssClass);
		}

		SimpleActionGroup GetActionGroup()
		{
			return sharedActionGroup ?? actionGroup;
		}

		string GetActionGroupName()
		{
			if (UseApplicationActions)
				return ApplicationActionGroupName;
			return sharedActionGroupName ?? actionGroupName;
		}
	}
}
