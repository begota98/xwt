using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class DialogBackend : WindowFrameBackend, IDialogBackend
	{
		Gtk.Box mainBox;
		Gtk.Box contentBox;
		Gtk.Box buttonBox;
		Gtk.Widget content;
		Gtk.Widget menuBar;
		IMenuBackend pendingMenu;
		DialogButton[] dialogButtons;
		Gtk.Button[] buttons;
		DialogButton defaultButton;
		GLib.MainLoop loop;
		IDialogEventSink dialogEventSink;
		Color? backgroundColor;
		Gtk.CssProvider backgroundProvider;
		string backgroundCssClass;
		static int backgroundClassId;

		public override void Initialize()
		{
			Window = Gtk.Window.New();
			Window.Modal = true;
			GtkEngine.Application?.AddWindow(Window);

			mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
			mainBox.Hexpand = true;
			mainBox.Vexpand = true;
			contentBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
			contentBox.Hexpand = true;
			contentBox.Vexpand = true;
			buttonBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
			buttonBox.Halign = Gtk.Align.End;
			buttonBox.Show();
			contentBox.Show();
			mainBox.Show();

			mainBox.Append(contentBox);
			mainBox.Append(buttonBox);
			Window.Child = mainBox;
			Window.OnCloseRequest += HandleCloseRequest;
			ApplyBackground();
			if (pendingMenu != null)
				SetMainMenu(pendingMenu);
		}

		void IWindowFrameBackend.Initialize(IWindowFrameEventSink eventSink)
		{
			base.Initialize(eventSink);
			dialogEventSink = (IDialogEventSink)eventSink;
		}

		public Color BackgroundColor {
			get { return backgroundColor ?? Colors.Transparent; }
			set {
				backgroundColor = value;
				ApplyBackground();
			}
		}

		public void SetChild(IWidgetBackend child)
		{
			if (content != null)
				contentBox.Remove(content);
			content = child != null ? ((IGtkWidgetBackend)child).Widget : null;
			if (content != null) {
				content.Hexpand = true;
				content.Vexpand = true;
				WidgetBackend.ApplyChildPlacement(child);
				contentBox.Append(content);
			}
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
			minSize = Size.Zero;
			decorationSize = Size.Zero;
			if (buttonBox == null)
				return;

			buttonBox.Measure(Gtk.Orientation.Horizontal, -1, out int minW, out int natW, out _, out _);
			buttonBox.Measure(Gtk.Orientation.Vertical, -1, out int minH, out int natH, out _, out _);
			minSize = new Size(Math.Max(minW, natW), Math.Max(minH, natH));
			decorationSize = new Size(0, Math.Max(minH, natH));
		}

		public void SetMinSize(Size size)
		{
			if (Window == null)
				return;
			Window.SetSizeRequest((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
		}

		public DialogButton DefaultButton {
			get { return defaultButton; }
			set {
				defaultButton = value;
				SetButtons(dialogButtons ?? Enumerable.Empty<DialogButton>());
			}
		}

		public void SetButtons(IEnumerable<DialogButton> newButtons)
		{
			if (buttons != null) {
				foreach (var b in buttons)
					buttonBox.Remove(b);
			}

			dialogButtons = newButtons?.ToArray() ?? Array.Empty<DialogButton>();
			buttons = new Gtk.Button[dialogButtons.Length];

			for (int i = 0; i < dialogButtons.Length; i++) {
				var db = dialogButtons[i];
				var button = Gtk.Button.New();
				button.Label = db.Label ?? string.Empty;
				button.Sensitive = db.Sensitive;
				button.Visible = db.Visible;
				button.OnClicked += HandleButtonClicked;
				buttonBox.Append(button);
				button.Show();
				if (db == defaultButton) {
					button.ReceivesDefault = true;
					button.GrabFocus();
				}
				buttons[i] = button;
			}
		}

		public void UpdateButton(DialogButton btn)
		{
			if (dialogButtons == null || buttons == null)
				return;
			int i = Array.IndexOf(dialogButtons, btn);
			if (i < 0 || i >= buttons.Length)
				return;
			var button = buttons[i];
			button.Label = btn.Label ?? string.Empty;
			button.Sensitive = btn.Sensitive;
			button.Visible = btn.Visible;
		}

		void HandleButtonClicked(object sender, EventArgs e)
		{
			int i = Array.IndexOf(buttons, (Gtk.Button)sender);
			if (i < 0)
				return;
			ApplicationContext.InvokeUserCode(() => dialogEventSink.OnDialogButtonClicked(dialogButtons[i]));
		}

		public void RunLoop(IWindowFrameBackend parent)
		{
			if (Window == null)
				return;
			if (parent is WindowFrameBackend frame && frame.Window != null)
				Window.TransientFor = frame.Window;
			Window.Present();
			loop = GLib.MainLoop.New(null, false);
			loop.Run();
			loop = null;
		}

		public void EndLoop()
		{
			Window.Hide();
			loop?.Quit();
		}

		bool HandleCloseRequest(Gtk.Window sender, EventArgs args)
		{
			EndLoop();
			return true;
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
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
				backgroundCssClass = "xwt-dialog-bg-" + Interlocked.Increment(ref backgroundClassId).ToString();
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
