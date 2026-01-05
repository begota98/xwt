using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SpinButtonBackend : WidgetBackend, ISpinButtonBackend
	{
		readonly Gtk.SpinButton spinButton;
		readonly Gtk.Adjustment adjustment;
		ISpinButtonEventSink eventSink;
		bool indeterminate;
		string indeterminateMessage = string.Empty;

		public SpinButtonBackend()
		{
			adjustment = Gtk.Adjustment.New(0, 0, 100, 1, 10, 0);
			ScrollAdjustmentBackend.AttachGuards(adjustment);
			spinButton = Gtk.SpinButton.New(adjustment, 1, 0);
			spinButton.Numeric = true;
			spinButton.Xalign = 1.0f;
			Widget = spinButton;
			Widget.Show();
		}

		public double ClimbRate {
			get { return spinButton.ClimbRate; }
			set { spinButton.ClimbRate = value; }
		}

		public int Digits {
			get { return (int)spinButton.Digits; }
			set { spinButton.Digits = (uint)Math.Max(0, value); }
		}

		public double Value {
			get { return spinButton.Value; }
			set {
				spinButton.Numeric = true;
				spinButton.Value = value;
			}
		}

		public bool Wrap {
			get { return spinButton.Wrap; }
			set { spinButton.Wrap = value; }
		}

		public double MinimumValue {
			get { return adjustment.Lower; }
			set { adjustment.Lower = value; }
		}

		public double MaximumValue {
			get { return adjustment.Upper; }
			set { adjustment.Upper = value; }
		}

		public double IncrementValue {
			get { return adjustment.StepIncrement; }
			set { adjustment.StepIncrement = value; }
		}

		public void SetButtonStyle(ButtonStyle style)
		{
			switch (style) {
				case ButtonStyle.Borderless:
				case ButtonStyle.Flat:
					spinButton.AddCssClass("flat");
					break;
				default:
					if (spinButton.HasCssClass("flat"))
						spinButton.RemoveCssClass("flat");
					break;
			}
		}

		public string IndeterminateMessage {
			get { return indeterminateMessage; }
			set {
				indeterminateMessage = value ?? string.Empty;
				if (indeterminate)
					SetSpinText(indeterminateMessage);
			}
		}

		public bool IsIndeterminate {
			get { return indeterminate; }
			set {
				indeterminate = value;
				spinButton.Numeric = !value;
				if (value)
					SetSpinText(indeterminateMessage);
				else
					spinButton.Value = MinimumValue;
			}
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ISpinButtonEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is SpinButtonEvent ev && ev == SpinButtonEvent.ValueChanged)
				spinButton.OnValueChanged += HandleValueChanged;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is SpinButtonEvent ev && ev == SpinButtonEvent.ValueChanged)
				spinButton.OnValueChanged -= HandleValueChanged;
		}

		void HandleValueChanged(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.ValueChanged);
		}

		void SetSpinText(string text)
		{
			if (text == null)
				text = string.Empty;
			spinButton.Text_ = text;
		}
	}
}
