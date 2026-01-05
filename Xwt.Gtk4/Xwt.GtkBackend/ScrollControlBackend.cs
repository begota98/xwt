using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ScrollControlBackend : IScrollControlBackend
	{
		Gtk.Adjustment adjustment;
		IScrollControlEventSink eventSink;
		ApplicationContext context;

		public ScrollControlBackend()
		{
		}

		public ScrollControlBackend(Gtk.Adjustment adjustment)
		{
			this.adjustment = adjustment;
			ScrollAdjustmentBackend.AttachGuards(adjustment);
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void Initialize(IScrollControlEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void EnableEvent(object eventId)
		{
			var ev = (ScrollAdjustmentEvent)eventId;
			if (ev == ScrollAdjustmentEvent.ValueChanged && adjustment != null)
				adjustment.OnValueChanged += HandleValueChanged;
		}

		public void DisableEvent(object eventId)
		{
			var ev = (ScrollAdjustmentEvent)eventId;
			if (ev == ScrollAdjustmentEvent.ValueChanged && adjustment != null)
				adjustment.OnValueChanged -= HandleValueChanged;
		}

		void HandleValueChanged(object sender, EventArgs e)
		{
			if (context == null || eventSink == null)
				return;
			context.InvokeUserCode(eventSink.OnValueChanged);
		}

		public double Value {
			get { return adjustment?.Value ?? 0d; }
			set {
				if (adjustment == null)
					return;
				var lower = adjustment.Lower;
				var upper = adjustment.Upper - adjustment.PageSize;
				if (upper < lower)
					upper = lower;
				if (value < lower)
					value = lower;
				if (value > upper)
					value = upper;
				adjustment.Value = value;
			}
		}

		public double LowerValue => adjustment?.Lower ?? 0d;

		public double UpperValue => adjustment?.Upper ?? 0d;

		public double PageSize => adjustment?.PageSize ?? 0d;

		public double PageIncrement => adjustment?.PageIncrement ?? 0d;

		public double StepIncrement => adjustment?.StepIncrement ?? 0d;
	}
}
