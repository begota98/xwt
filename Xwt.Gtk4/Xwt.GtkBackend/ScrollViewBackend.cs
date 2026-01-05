using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ScrollViewBackend : WidgetBackend, IScrollViewBackend
	{
		static readonly bool logScrolledWindow = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_SCROLLED_LOG"), "1", StringComparison.Ordinal);
		static readonly bool logAdjustmentTrace = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_ADJUSTMENT_TRACE"), "1", StringComparison.Ordinal);
		readonly Gtk.ScrolledWindow scrolledWindow;
		Gtk.Widget content;
		Gtk.Viewport viewport;
		IScrollViewEventSink eventSink;
		bool adjustmentFixing;
		Gtk.Adjustment attachedHAdjustment;
		Gtk.Adjustment attachedVAdjustment;

		public ScrollViewBackend()
		{
			scrolledWindow = Gtk.ScrolledWindow.New();
			scrolledWindow.Hexpand = true;
			scrolledWindow.Vexpand = true;
			scrolledWindow.Show();
			Widget = scrolledWindow;
			if (logScrolledWindow)
				Console.WriteLine($"[Xwt.Gtk4] ScrollViewBackend scrolled window created: 0x{scrolledWindow.Handle.DangerousGetHandle():x}");
			scrolledWindow.OnNotify += HandleScrolledWindowNotify;
			AttachAdjustmentGuards();
		}

		public void SetChild(IWidgetBackend child)
		{
			if (viewport != null) {
				viewport.SetChild(null);
				viewport = null;
			}
			if (content != null)
				scrolledWindow.SetChild(null);
			content = child != null ? ((IGtkWidgetBackend)child).Widget : null;
			if (content != null) {
				content.Hexpand = true;
				content.Vexpand = true;
				WidgetBackend.ApplyChildPlacement(child);
			}
			if (content is Gtk.Scrollable scrollable) {
				scrollable.HscrollPolicy = Gtk.ScrollablePolicy.Natural;
				scrollable.VscrollPolicy = Gtk.ScrollablePolicy.Natural;
				scrolledWindow.SetChild(content);
			} else if (content != null) {
				viewport = Gtk.Viewport.New(null, null);
				viewport.HscrollPolicy = Gtk.ScrollablePolicy.Natural;
				viewport.VscrollPolicy = Gtk.ScrollablePolicy.Natural;
				viewport.Hexpand = true;
				viewport.Vexpand = true;
				viewport.SetChild(content);
				viewport.Show();
				scrolledWindow.SetChild(viewport);
			} else {
				scrolledWindow.SetChild(null);
			}
			AttachCustomScrolling(child);
			AttachAdjustmentGuards();
		}

		public bool BorderVisible {
			get { return scrolledWindow.HasFrame; }
			set { scrolledWindow.HasFrame = value; }
		}

		public Rectangle VisibleRect {
			get {
				var hadj = scrolledWindow.Hadjustment;
				var vadj = scrolledWindow.Vadjustment;
				return new Rectangle(hadj.Value, vadj.Value, hadj.PageSize, vadj.PageSize);
			}
		}

		public void SetChildSize(Size size)
		{
			// GTK4 should size the scrollable child based on measurement; forcing a size
			// request here can yield invalid adjustments when the viewport is larger.
		}

		public ScrollPolicy VerticalScrollPolicy {
			get { return ToXwtPolicy(scrolledWindow.VscrollbarPolicy); }
			set { scrolledWindow.VscrollbarPolicy = ToGtkPolicy(value); }
		}

		public ScrollPolicy HorizontalScrollPolicy {
			get { return ToXwtPolicy(scrolledWindow.HscrollbarPolicy); }
			set { scrolledWindow.HscrollbarPolicy = ToGtkPolicy(value); }
		}

		public IScrollControlBackend CreateVerticalScrollControl()
		{
			return new ScrollControlBackend(scrolledWindow.Vadjustment);
		}

		public IScrollControlBackend CreateHorizontalScrollControl()
		{
			return new ScrollControlBackend(scrolledWindow.Hadjustment);
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (IScrollViewEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is ScrollViewEvent ev && ev == ScrollViewEvent.VisibleRectChanged) {
				scrolledWindow.Hadjustment.OnValueChanged += HandleVisibleRectChanged;
				scrolledWindow.Vadjustment.OnValueChanged += HandleVisibleRectChanged;
			}
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is ScrollViewEvent ev && ev == ScrollViewEvent.VisibleRectChanged) {
				scrolledWindow.Hadjustment.OnValueChanged -= HandleVisibleRectChanged;
				scrolledWindow.Vadjustment.OnValueChanged -= HandleVisibleRectChanged;
			}
		}

		void HandleVisibleRectChanged(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnVisibleRectChanged);
		}

		void AttachAdjustmentGuards()
		{
			var hadj = scrolledWindow.Hadjustment;
			var vadj = scrolledWindow.Vadjustment;
			if (hadj != null && hadj != attachedHAdjustment) {
				if (attachedHAdjustment != null) {
					attachedHAdjustment.OnChanged -= HandleAdjustmentChanged;
					attachedHAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				hadj.OnChanged += HandleAdjustmentChanged;
				hadj.OnNotify += HandleAdjustmentNotify;
				attachedHAdjustment = hadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] ScrollViewBackend attach hadj=0x{hadj.Handle.DangerousGetHandle():x} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x}");
			}
			if (vadj != null && vadj != attachedVAdjustment) {
				if (attachedVAdjustment != null) {
					attachedVAdjustment.OnChanged -= HandleAdjustmentChanged;
					attachedVAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				vadj.OnChanged += HandleAdjustmentChanged;
				vadj.OnNotify += HandleAdjustmentNotify;
				attachedVAdjustment = vadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] ScrollViewBackend attach vadj=0x{vadj.Handle.DangerousGetHandle():x} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x}");
			}
		}

		void HandleScrolledWindowNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name == "hadjustment" || name == "vadjustment")
				AttachAdjustmentGuards();
		}

		void HandleAdjustmentChanged(Gtk.Adjustment sender, EventArgs e)
		{
			if (adjustmentFixing)
				return;
			if (logAdjustmentTrace)
				LogAdjustment("ScrollViewBackend", sender);
			adjustmentFixing = true;
			try {
				ScrollAdjustmentBackend.FixAdjustment(sender);
			} finally {
				adjustmentFixing = false;
			}
		}

		void HandleAdjustmentNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (adjustmentFixing || !(sender is Gtk.Adjustment adjustment))
				return;
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "upper" && name != "page-size" && name != "lower")
				return;
			if (logAdjustmentTrace)
				LogAdjustment($"ScrollViewBackend notify:{name}", adjustment);
			adjustmentFixing = true;
			try {
				ScrollAdjustmentBackend.FixAdjustment(adjustment);
			} finally {
				adjustmentFixing = false;
			}
		}

		void LogAdjustment(string source, Gtk.Adjustment adjustment)
		{
			if (adjustment == null)
				return;
			int swW = scrolledWindow.GetAllocatedWidth();
			int swH = scrolledWindow.GetAllocatedHeight();
			int childW = content?.GetAllocatedWidth() ?? 0;
			int childH = content?.GetAllocatedHeight() ?? 0;
			Console.WriteLine($"[Xwt.Gtk4] {source} adj=0x{adjustment.Handle.DangerousGetHandle():x} lower={adjustment.Lower} upper={adjustment.Upper} page={adjustment.PageSize} value={adjustment.Value} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x} swSize={swW}x{swH} childSize={childW}x{childH}");
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}

		void AttachCustomScrolling(IWidgetBackend child)
		{
			if (child == null)
				return;
			var wb = child as WidgetBackend;
			var sink = wb != null ? wb.GetEventSink() : null;
			if (wb == null || sink == null)
				return;
			if (!sink.SupportsCustomScrolling())
				return;

			var hadj = new ScrollAdjustmentBackend(scrolledWindow.Hadjustment);
			var vadj = new ScrollAdjustmentBackend(scrolledWindow.Vadjustment);
			sink.SetScrollAdjustments(hadj, vadj);
		}

		static Gtk.PolicyType ToGtkPolicy(ScrollPolicy policy)
		{
			switch (policy) {
				case ScrollPolicy.Always:
					return Gtk.PolicyType.Always;
				case ScrollPolicy.Never:
					return Gtk.PolicyType.Never;
				default:
					return Gtk.PolicyType.Automatic;
			}
		}

		static ScrollPolicy ToXwtPolicy(Gtk.PolicyType policy)
		{
			switch (policy) {
				case Gtk.PolicyType.Always:
					return ScrollPolicy.Always;
				case Gtk.PolicyType.Never:
					return ScrollPolicy.Never;
				default:
					return ScrollPolicy.Automatic;
			}
		}
	}
}
