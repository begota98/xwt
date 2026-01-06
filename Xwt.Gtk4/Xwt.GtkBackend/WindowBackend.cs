using System;
using System.Threading;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class WindowBackend : WindowFrameBackend, IWindowBackend
	{
		Gtk.Box mainBox;
		Gtk.Overlay rootOverlay;
		Gtk.MenuButton contextMenuAnchor;
		Gtk.Widget content;
		Gtk.Widget menuBar;
		IMenuBackend pendingMenu;
		Color? backgroundColor;
		Gtk.CssProvider backgroundProvider;
		string backgroundCssClass;
		static int backgroundClassId;

		public override void Initialize()
		{
			Window = Gtk.ApplicationWindow.New(GtkEngine.Application);
			GtkEngine.Application?.AddWindow(Window);
			mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
			mainBox.Hexpand = true;
			mainBox.Vexpand = true;
			rootOverlay = Gtk.Overlay.New();
			rootOverlay.Hexpand = true;
			rootOverlay.Vexpand = true;
			rootOverlay.SetChild(mainBox);
			Window.Child = rootOverlay;
			contextMenuAnchor = Gtk.MenuButton.New();
			contextMenuAnchor.HasFrame = false;
			contextMenuAnchor.Halign = Gtk.Align.Start;
			contextMenuAnchor.Valign = Gtk.Align.Start;
			contextMenuAnchor.Hexpand = false;
			contextMenuAnchor.Vexpand = false;
			contextMenuAnchor.Focusable = false;
			contextMenuAnchor.CanTarget = false;
			contextMenuAnchor.SetSizeRequest(1, 1);
			contextMenuAnchor.Opacity = 0;
			contextMenuAnchor.Show();
			rootOverlay.AddOverlay(contextMenuAnchor);
			MenuBackend.RegisterContextMenuAnchor(Window, contextMenuAnchor);
			ApplyBackground();
			if (pendingMenu != null)
				SetMainMenu(pendingMenu);
		}

		public void SetChild(IWidgetBackend child)
		{
			if (content != null)
				mainBox.Remove(content);
			content = ((IGtkWidgetBackend)child).Widget;
			content.Hexpand = true;
			content.Vexpand = true;
			WidgetBackend.ApplyChildPlacement(child);
			mainBox.Append(content);
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}

		public void SetMainMenu(IMenuBackend menu)
		{
			if (mainBox == null) {
				pendingMenu = menu;
				return;
			}

			if (menuBar != null) {
				mainBox.Remove(menuBar);
				menuBar = null;
			}

			if (menu is MenuBackend gtkMenu) {
				menuBar = gtkMenu.GetMenuBarWidget();
				menuBar.Hexpand = true;
				menuBar.Vexpand = false;
				menuBar.Show();
				mainBox.Prepend(menuBar);
				if (Window != null)
					gtkMenu.AttachActionGroup(Window);
			}

			pendingMenu = null;
		}

		public void SetPadding(double left, double top, double right, double bottom)
		{
			if (content == null)
				return;
			content.MarginStart = (int)left;
			content.MarginTop = (int)top;
			content.MarginEnd = (int)right;
			content.MarginBottom = (int)bottom;
		}

		public void GetMetrics(out Size minSize, out Size decorationSize)
		{
			if (menuBar != null) {
				menuBar.Measure(Gtk.Orientation.Horizontal, -1, out var minW, out var natW, out _, out _);
				menuBar.Measure(Gtk.Orientation.Vertical, natW, out var minH, out _, out _, out _);
				minSize = new Size(natW, 0);
				decorationSize = new Size(0, minH);
				return;
			}
			minSize = Size.Zero;
			decorationSize = Size.Zero;
		}

		public void SetMinSize(Size size)
		{
			if (Window == null)
				return;
			Window.SetSizeRequest((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
		}

		Color IWindowBackend.BackgroundColor {
			get { return backgroundColor ?? Colors.Transparent; }
			set {
				backgroundColor = value;
				ApplyBackground();
			}
		}

		void ApplyBackground()
		{
			if (Window == null || !backgroundColor.HasValue)
				return;

			if (backgroundProvider == null) {
				backgroundProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, backgroundProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(backgroundCssClass))
				backgroundCssClass = "xwt-window-bg-" + Interlocked.Increment(ref backgroundClassId).ToString();
			if (!Window.HasCssClass(backgroundCssClass))
				Window.AddCssClass(backgroundCssClass);

			var color = backgroundColor.Value;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + backgroundCssClass + " { background-color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			backgroundProvider.LoadFromString(css);
		}
	}
}
