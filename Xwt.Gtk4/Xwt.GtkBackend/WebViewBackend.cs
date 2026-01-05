using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class WebViewBackend : WidgetBackend, IWebViewBackend
	{
		Gtk.Box box;
		Gtk.Label label;
		string url;
		string html;
		string customCss;
		bool contextMenuEnabled = true;
		bool drawsBackground = true;
		bool scrollBarsEnabled = true;

		public WebViewBackend()
		{
			box = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
			label = Gtk.Label.New("WebView is not available in GTK4 backend.");
			label.Wrap = true;
			label.Halign = Gtk.Align.Start;
			label.Valign = Gtk.Align.Start;
			box.Append(label);
			Widget = box;
			Widget.Show();
			label.Show();
		}

		protected new IWebViewEventSink EventSink => (IWebViewEventSink)base.EventSink;

		public string Url {
			get { return url ?? string.Empty; }
			set {
				url = value ?? string.Empty;
				label.Label_ = url;
				RaiseLoadingEvents();
			}
		}

		public string Title => string.Empty;

		public double LoadProgress => 1.0;

		public bool CanGoBack => false;

		public void GoBack()
		{
		}

		public bool CanGoForward => false;

		public void GoForward()
		{
		}

		public void Reload()
		{
			RaiseLoadingEvents();
		}

		public void StopLoading()
		{
		}

		public void LoadHtml(string content, string base_uri)
		{
			html = content ?? string.Empty;
			label.Label_ = html;
			RaiseLoadingEvents();
		}

		public bool ContextMenuEnabled {
			get { return contextMenuEnabled; }
			set { contextMenuEnabled = value; }
		}

		public bool DrawsBackground {
			get { return drawsBackground; }
			set { drawsBackground = value; }
		}

		public bool ScrollBarsEnabled {
			get { return scrollBarsEnabled; }
			set { scrollBarsEnabled = value; }
		}

		public string CustomCss {
			get { return customCss ?? string.Empty; }
			set { customCss = value; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
		}

		void RaiseLoadingEvents()
		{
			ApplicationContext.InvokeUserCode(EventSink.OnLoading);
			ApplicationContext.InvokeUserCode(EventSink.OnLoaded);
		}
	}
}
