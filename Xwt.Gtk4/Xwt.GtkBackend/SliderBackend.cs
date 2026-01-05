using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SliderBackend : WidgetBackend, ISliderBackend
	{
		Gtk.Scale scale;
		Gtk.Adjustment adjustment;
		ISliderEventSink eventSink;
		bool snapToTicks;
		bool onValueChangedEnabled;

		public void Initialize(Orientation dir)
		{
			var orientation = dir == Orientation.Horizontal ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical;
			adjustment = Gtk.Adjustment.New(0, 0, 100, 1, 10, 0);
			ScrollAdjustmentBackend.AttachGuards(adjustment);
			scale = Gtk.Scale.New(orientation, adjustment);
			if (dir == Orientation.Vertical)
				scale.Inverted = true;
			scale.DrawValue = false;
			scale.OnValueChanged += HandleValueChanged;
			Widget = scale;
			Widget.Show();
		}

		public double Value {
			get { return adjustment.Value; }
			set { adjustment.Value = value; }
		}

		public double MinimumValue {
			get { return adjustment.Lower; }
			set {
				scale.SetRange(value, Math.Max(value + 1, MaximumValue));
				UpdateMarks();
			}
		}

		public double MaximumValue {
			get { return adjustment.Upper; }
			set {
				scale.SetRange(Math.Min(value - 1, MinimumValue), value);
				UpdateMarks();
			}
		}

		public double StepIncrement {
			get { return adjustment.StepIncrement; }
			set {
				adjustment.StepIncrement = value;
				adjustment.PageIncrement = value;
				if (Math.Abs(value) >= 1.0 || Math.Abs(value) < double.Epsilon) {
					scale.Digits = 0;
				} else {
					scale.Digits = Math.Abs((int)Math.Floor(Math.Log10(Math.Abs(value))));
					if (scale.Digits > 5)
						scale.Digits = 5;
				}
				UpdateMarks();
			}
		}

		public bool SnapToTicks {
			get { return snapToTicks; }
			set {
				snapToTicks = value;
				UpdateMarks();
			}
		}

		public double SliderPosition => Value;

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ISliderEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is SliderEvent ev && ev == SliderEvent.ValueChanged)
				onValueChangedEnabled = true;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is SliderEvent ev && ev == SliderEvent.ValueChanged)
				onValueChangedEnabled = false;
		}

		void HandleValueChanged(object sender, EventArgs e)
		{
			if (SnapToTicks && Math.Abs(StepIncrement) > double.Epsilon) {
				var offset = Math.Abs(Value) % StepIncrement;
				if (Math.Abs(offset) > double.Epsilon) {
					if (offset > StepIncrement / 2) {
						if (Value >= 0)
							Value += -offset + StepIncrement;
						else
							Value += offset - StepIncrement;
					} else {
						if (Value >= 0)
							Value -= offset;
						else
							Value += offset;
					}
				}
			}
			if (onValueChangedEnabled)
				ApplicationContext.InvokeUserCode(eventSink.ValueChanged);
		}

		void UpdateMarks()
		{
			scale.ClearMarks();
			if (!SnapToTicks || Math.Abs(StepIncrement) < double.Epsilon)
				return;
			if (MinimumValue >= 0) {
				var ticksCount = (int)((MaximumValue - MinimumValue) / StepIncrement) + 1;
				for (int i = 0; i < ticksCount; i++)
					scale.AddMark(MinimumValue + (i * StepIncrement), Gtk.PositionType.Bottom, null);
			} else if (MaximumValue <= 0) {
				var ticksCount = (int)((MaximumValue - MinimumValue) / StepIncrement) + 1;
				for (int i = 0; i < ticksCount; i++)
					scale.AddMark(-(i * StepIncrement), Gtk.PositionType.Bottom, null);
			} else if (MinimumValue < 0) {
				var ticksCount = (int)(MaximumValue / StepIncrement) + 1;
				for (int i = 0; i < ticksCount; i++)
					scale.AddMark(i * StepIncrement, Gtk.PositionType.Bottom, null);
				var ticksCountN = (int)(Math.Abs(MinimumValue) / StepIncrement) + 1;
				for (int i = 1; i < ticksCountN; i++)
					scale.AddMark(-(i * StepIncrement), Gtk.PositionType.Bottom, null);
			}
		}
	}
}
