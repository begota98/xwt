using System;
using System.Globalization;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class DatePickerBackend : WidgetBackend, IDatePickerBackend
	{
		static readonly string[] StyleFormats = new string[3];
		Gtk.Entry entry;
		DateTime currentValue = DateTime.Now;
		DateTime minValue = DateTime.MinValue;
		DateTime maxValue = DateTime.MaxValue;
		DatePickerStyle style;
		string format;
		bool updating;
		bool valueChangedEnabled;

		static DatePickerBackend()
		{
			StyleFormats[(int)DatePickerStyle.Date] = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
			var timeSeparator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
			StyleFormats[(int)DatePickerStyle.Time] = "HH" + timeSeparator + "mm" + timeSeparator + "ss";
			StyleFormats[(int)DatePickerStyle.DateTime] = StyleFormats[(int)DatePickerStyle.Date] + " " + StyleFormats[(int)DatePickerStyle.Time];
		}

		public DatePickerBackend()
		{
			entry = Gtk.Entry.New();
			Widget = entry;
			entry.OnChanged += HandleChanged;
			Style = DatePickerStyle.DateTime;
			UpdateText();
			Widget.Show();
		}

		protected new IDatePickerEventSink EventSink => (IDatePickerEventSink)base.EventSink;

		public DateTime DateTime {
			get { return currentValue; }
			set {
				currentValue = Clamp(value);
				UpdateText();
			}
		}

		public DateTime MinimumDateTime {
			get { return minValue; }
			set {
				minValue = value;
				if (currentValue < minValue)
					DateTime = minValue;
			}
		}

		public DateTime MaximumDateTime {
			get { return maxValue; }
			set {
				maxValue = value;
				if (currentValue > maxValue)
					DateTime = maxValue;
			}
		}

		public DatePickerStyle Style {
			get { return style; }
			set {
				style = value;
				format = StyleFormats[(int)style];
				if (entry != null)
					entry.WidthChars = format.Length;
				UpdateText();
			}
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is DatePickerEvent ev && ev == DatePickerEvent.ValueChanged)
				valueChangedEnabled = true;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is DatePickerEvent ev && ev == DatePickerEvent.ValueChanged)
				valueChangedEnabled = false;
		}

		void UpdateText()
		{
			if (entry == null || updating || string.IsNullOrEmpty(format))
				return;
			updating = true;
			entry.Text_ = currentValue.ToString(format, CultureInfo.CurrentCulture);
			updating = false;
		}

		void HandleChanged(object sender, EventArgs e)
		{
			if (updating || entry == null || string.IsNullOrEmpty(format))
				return;
			var text = entry.Text_ ?? string.Empty;
			if (!DateTime.TryParseExact(text, format, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
				return;
			parsed = Clamp(parsed);
			if (parsed == currentValue)
				return;
			currentValue = parsed;
			UpdateText();
			if (valueChangedEnabled)
				ApplicationContext.InvokeUserCode(EventSink.ValueChanged);
		}

		DateTime Clamp(DateTime value)
		{
			if (value < minValue)
				return minValue;
			if (value > maxValue)
				return maxValue;
			return value;
		}
	}
}
