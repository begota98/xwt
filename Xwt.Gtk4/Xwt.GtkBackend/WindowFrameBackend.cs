using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class WindowFrameBackend : IWindowFrameBackend
	{
		Gtk.Window window;
		IWindowFrameEventSink eventSink;
		WindowFrame frontend;
		Rectangle bounds;
		double opacity = 1d;
		bool isFullscreen;
		bool pendingInitialSizeRequest;
		bool isClosed;
		GObject.SignalHandler<Gtk.Widget> shownHandler;
		GObject.SignalHandler<Gtk.Widget> hiddenHandler;
		GObject.SignalHandler<Gtk.Widget> destroyedHandler;
		GObject.ReturningSignalHandler<Gtk.Window, bool> closeRequestHandler;
		GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs> notifyHandler;

		public Gtk.Window Window {
			get { return window; }
			set {
				window = value;
				if (window != null && bounds.Width > 0 && bounds.Height > 0)
					ApplyInitialSize();
			}
		}

		object IWindowFrameBackend.Window => window;

		public IntPtr NativeHandle => IntPtr.Zero;

		void IBackend.InitializeBackend(object frontend, ApplicationContext context)
		{
			this.frontend = (WindowFrame)frontend;
			ApplicationContext = context;
		}

		public ApplicationContext ApplicationContext { get; private set; }

		public void Initialize(IWindowFrameEventSink eventSink)
		{
			this.eventSink = eventSink;
			Initialize();
		}

		public void EnableEvent(object eventId)
		{
			if (window == null || !(eventId is WindowFrameEvent ev))
				return;
			switch (ev) {
				case WindowFrameEvent.Shown:
					if (shownHandler == null)
						shownHandler = (sender, args) => ApplicationContext.InvokeUserCode(() => eventSink?.OnShown());
					window.OnShow += shownHandler;
					break;
				case WindowFrameEvent.Hidden:
					if (hiddenHandler == null)
						hiddenHandler = (sender, args) => ApplicationContext.InvokeUserCode(() => eventSink?.OnHidden());
					window.OnHide += hiddenHandler;
					break;
				case WindowFrameEvent.BoundsChanged:
					if (notifyHandler == null)
						notifyHandler = HandleNotify;
					window.OnNotify += notifyHandler;
					break;
				case WindowFrameEvent.CloseRequested:
				case WindowFrameEvent.Closed:
					if (closeRequestHandler == null)
						closeRequestHandler = HandleCloseRequest;
					window.OnCloseRequest += closeRequestHandler;
					if (destroyedHandler == null)
						destroyedHandler = HandleDestroyed;
					window.OnDestroy += destroyedHandler;
					break;
			}
		}

		public void DisableEvent(object eventId)
		{
			if (window == null || !(eventId is WindowFrameEvent ev))
				return;
			switch (ev) {
				case WindowFrameEvent.Shown:
					if (shownHandler != null)
						window.OnShow -= shownHandler;
					break;
				case WindowFrameEvent.Hidden:
					if (hiddenHandler != null)
						window.OnHide -= hiddenHandler;
					break;
				case WindowFrameEvent.BoundsChanged:
					if (notifyHandler != null)
						window.OnNotify -= notifyHandler;
					break;
				case WindowFrameEvent.CloseRequested:
				case WindowFrameEvent.Closed:
					if (closeRequestHandler != null)
						window.OnCloseRequest -= closeRequestHandler;
					if (destroyedHandler != null)
						window.OnDestroy -= destroyedHandler;
					break;
			}
		}

		public virtual void Initialize()
		{
		}

		public virtual void Dispose()
		{
			window?.Dispose();
			window = null;
		}

		public string Name {
			get { return window?.Name ?? string.Empty; }
			set {
				if (window != null)
					window.Name = value;
			}
		}

		public Rectangle Bounds {
			get { return bounds; }
			set {
				bounds = value;
				if (window != null)
					ApplyInitialSize();
			}
		}

		public void Move(double x, double y)
		{
			bounds = new Rectangle(x, y, bounds.Width, bounds.Height);
		}

		public virtual void SetSize(double width, double height)
		{
			bounds = new Rectangle(bounds.X, bounds.Y, width, height);
			if (window == null)
				return;
			ApplyInitialSize();
		}

		public bool Visible {
			get { return window?.Visible ?? false; }
			set {
				if (window != null) {
					window.Visible = value;
					if (value && pendingInitialSizeRequest) {
						window.SetSizeRequest(-1, -1);
						pendingInitialSizeRequest = false;
					}
				}
			}
		}

		public bool Sensitive {
			get { return window?.Sensitive ?? false; }
			set {
				if (window != null)
					window.Sensitive = value;
			}
		}

		public string Title {
			get { return window?.Title ?? string.Empty; }
			set {
				if (window != null)
					window.Title = value;
			}
		}

		public bool Decorated {
			get { return window?.Decorated ?? true; }
			set {
				if (window != null)
					window.Decorated = value;
			}
		}

		bool showInTaskbar = true;
		public bool ShowInTaskbar {
			get { return showInTaskbar; }
			set { showInTaskbar = value; }
		}

		public void SetTransientFor(IWindowFrameBackend window)
		{
			var parent = window?.Window as Gtk.Window;
			if (parent != null && this.window != null)
				this.window.TransientFor = parent;
		}

		public bool Resizable {
			get { return window?.Resizable ?? true; }
			set {
				if (window != null)
					window.Resizable = value;
			}
		}

		public double Opacity {
			get { return opacity; }
			set {
				opacity = value;
				if (window != null)
					window.Opacity = value;
			}
		}

		public bool HasFocus => window?.HasFocus ?? false;

		public void SetIcon(ImageDescription image)
		{
			if (window == null)
				return;
			var icon = image.Backend as GtkImage;
			if (icon != null && !string.IsNullOrEmpty(icon.IconName))
				window.IconName = icon.IconName;
			else
				window.IconName = null;
		}

		public void Present()
		{
			window?.Present();
		}

		public bool Close()
		{
			return PerformClose(false);
		}

		public bool FullScreen {
			get { return isFullscreen; }
			set {
				if (window == null)
					return;
				if (value) {
					window.Fullscreen();
					isFullscreen = true;
				} else {
					window.Unfullscreen();
					isFullscreen = false;
				}
			}
		}

		public object Screen => null;

		void ApplyInitialSize()
		{
			if (window == null)
				return;
			int width = (int)Math.Max(1, bounds.Width);
			int height = (int)Math.Max(1, bounds.Height);
			window.SetDefaultSize(width, height);
			if (!window.Visible) {
				window.SetSizeRequest(width, height);
				pendingInitialSizeRequest = true;
			}
		}

		bool PerformClose(bool userClose)
		{
			bool close = false;
			ApplicationContext.InvokeUserCode(() => {
				close = eventSink?.OnCloseRequested() ?? true;
			});
			if (close) {
				if (!userClose)
					window?.Hide();
				if (!isClosed) {
					isClosed = true;
						ApplicationContext.InvokeUserCode(() => eventSink?.OnClosed());
				}
			}
			return close;
		}

		bool HandleCloseRequest(Gtk.Window sender, EventArgs args)
		{
			return !PerformClose(true);
		}

		void HandleDestroyed(Gtk.Widget sender, EventArgs args)
		{
			if (isClosed)
				return;
			isClosed = true;
			ApplicationContext.InvokeUserCode(() => eventSink?.OnClosed());
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "default-width" && name != "default-height" && name != "width-request" && name != "height-request")
				return;
			var width = window.GetAllocatedWidth();
			var height = window.GetAllocatedHeight();
			if (width <= 0 || height <= 0)
				return;
			bounds = new Rectangle(bounds.X, bounds.Y, width, height);
			ApplicationContext.InvokeUserCode(() => eventSink?.OnBoundsChanged(bounds));
		}
	}
}
