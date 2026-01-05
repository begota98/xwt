using System;
using System.Collections.Generic;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ScrollAdjustmentBackend : IScrollAdjustmentBackend
	{
		static readonly bool logAdjustments = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_ADJUSTMENT_LOG"), "1", StringComparison.Ordinal);
		static readonly HashSet<IntPtr> loggedAdjustments = new HashSet<IntPtr>();
		Gtk.Adjustment adjustment;
		IScrollAdjustmentEventSink eventSink;
		ApplicationContext context;
		double lowerValue;
		bool adjustmentFixing;
		bool guardsAttached;

		public ScrollAdjustmentBackend()
		{
		}

		public ScrollAdjustmentBackend(Gtk.Adjustment adjustment)
		{
			this.adjustment = adjustment;
			AttachGuards(adjustment);
		}

		public Gtk.Adjustment Adjustment => adjustment;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
			if (adjustment == null)
				adjustment = Gtk.Adjustment.New(0, 0, 0, 0, 0, 0);
			AttachGuards();
		}

		public void Initialize(IScrollAdjustmentEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void EnableEvent(object eventId)
		{
			if ((ScrollAdjustmentEvent)eventId == ScrollAdjustmentEvent.ValueChanged)
				adjustment.OnValueChanged += HandleValueChanged;
		}

		public void DisableEvent(object eventId)
		{
			if ((ScrollAdjustmentEvent)eventId == ScrollAdjustmentEvent.ValueChanged)
				adjustment.OnValueChanged -= HandleValueChanged;
		}

		void HandleValueChanged(object sender, EventArgs e)
		{
			context?.InvokeUserCode(eventSink.OnValueChanged);
		}

		public void SetRange(double lowerValue, double upperValue, double pageSize, double pageIncrement, double stepIncrement, double value)
		{
			this.lowerValue = lowerValue;
			var span = Math.Max(0, upperValue - lowerValue);
			if (pageSize < 0)
				pageSize = 0;
			var safePageSize = Math.Min(pageSize, span);
			var upper = Math.Max(span, safePageSize);
			var normalized = value - lowerValue;
			var lower = 0d;
			var max = upper - safePageSize;
			if (max < lower)
				max = lower;
			if (normalized < lower)
				normalized = lower;
			if (normalized > max)
				normalized = max;
			adjustment.Configure(normalized, lower, upper, stepIncrement, pageIncrement, safePageSize);
			FixAdjustment(adjustment);
		}

		public double Value {
			get { return lowerValue + adjustment.Value; }
			set {
				var normalized = value - lowerValue;
				var lower = adjustment.Lower;
				var upper = adjustment.Upper - adjustment.PageSize;
				if (upper < lower)
					upper = lower;
				if (normalized < lower)
					normalized = lower;
				if (normalized > upper)
					normalized = upper;
				adjustment.Value = normalized;
			}
		}

		void AttachGuards()
		{
			if (adjustment == null || guardsAttached)
				return;
			AttachGuards(adjustment, this);
			guardsAttached = true;
		}

		internal static void AttachGuards(Gtk.Adjustment adjustment)
		{
			if (adjustment == null)
				return;
			adjustment.OnChanged += (sender, e) => FixAdjustment(adjustment);
			adjustment.OnNotify += (sender, args) => {
				if (args?.Pspec == null)
					return;
				var name = args.Pspec.GetName();
				if (name != "upper" && name != "page-size" && name != "lower")
					return;
				FixAdjustment(adjustment);
			};
		}

		static void AttachGuards(Gtk.Adjustment adjustment, ScrollAdjustmentBackend backend)
		{
			adjustment.OnChanged += backend.HandleAdjustmentChanged;
			adjustment.OnNotify += backend.HandleAdjustmentNotify;
		}

		void HandleAdjustmentChanged(Gtk.Adjustment sender, EventArgs e)
		{
			if (adjustmentFixing)
				return;
			adjustmentFixing = true;
			try {
				FixAdjustment(sender);
			} finally {
				adjustmentFixing = false;
			}
		}

		void HandleAdjustmentNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (adjustmentFixing || !(sender is Gtk.Adjustment adj))
				return;
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "upper" && name != "page-size" && name != "lower")
				return;
			adjustmentFixing = true;
			try {
				FixAdjustment(adj);
			} finally {
				adjustmentFixing = false;
			}
		}

		internal static void FixAdjustment(Gtk.Adjustment adjustment)
		{
			if (adjustment == null)
				return;
			double lower = adjustment.Lower;
			double upper = adjustment.Upper;
			double page = adjustment.PageSize;
			var handle = adjustment.Handle.DangerousGetHandle();

			if (double.IsNaN(lower) || double.IsNaN(upper) || double.IsNaN(page))
				return;

			if (logAdjustments && (upper < lower || lower + page > upper) && !loggedAdjustments.Contains(handle)) {
				loggedAdjustments.Add(handle);
				Console.WriteLine($"[Xwt.Gtk4] Adjustment violation: lower={lower} upper={upper} page={page} value={adjustment.Value}");
				Console.WriteLine(Environment.StackTrace);
			}

			if (page < 0)
				page = 0;
			if (upper < lower)
				upper = lower;
			if (lower + page > upper)
				upper = lower + page;

			var value = adjustment.Value;
			var max = upper - page;
			if (max < lower)
				max = lower;
			if (value < lower)
				value = lower;
			if (value > max)
				value = max;

			if (adjustment.Lower != lower || adjustment.Upper != upper || adjustment.PageSize != page || value != adjustment.Value) {
				adjustment.Configure(value, lower, upper, adjustment.StepIncrement, adjustment.PageIncrement, page);
			}
		}
	}
}
