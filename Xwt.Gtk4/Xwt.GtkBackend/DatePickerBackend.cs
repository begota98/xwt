using System;
using System.Globalization;
using System.Linq;
using Xwt;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class DatePickerBackend : WidgetBackend, IDatePickerBackend
	{
		static readonly string[] StyleFormats = new string[3];
		static readonly uint keyLeft = Gdk.Functions.KeyvalFromName("Left");
		static readonly uint keyRight = Gdk.Functions.KeyvalFromName("Right");
		static readonly uint keyUp = Gdk.Functions.KeyvalFromName("Up");
		static readonly uint keyDown = Gdk.Functions.KeyvalFromName("Down");
		static readonly uint keyKpLeft = Gdk.Functions.KeyvalFromName("KP_Left");
		static readonly uint keyKpRight = Gdk.Functions.KeyvalFromName("KP_Right");
		static readonly uint keyKpUp = Gdk.Functions.KeyvalFromName("KP_Up");
		static readonly uint keyKpDown = Gdk.Functions.KeyvalFromName("KP_Down");
		Gtk.Entry entry;
		DateTime currentValue = DateTime.Now;
		DateTime minValue = DateTime.MinValue;
		DateTime maxValue = DateTime.MaxValue;
		DatePickerStyle style;
		string format;
		bool updating;
		bool valueChangedEnabled;
		readonly System.Collections.Generic.Dictionary<DateTimeComponent, int> componentPosition = new System.Collections.Generic.Dictionary<DateTimeComponent, int>();
		readonly System.Collections.Generic.Dictionary<DateTimeComponent, int> componentLength = new System.Collections.Generic.Dictionary<DateTimeComponent, int>();
		System.Collections.Generic.List<DateTimeComponent> componentsSorted = new System.Collections.Generic.List<DateTimeComponent>();
		DateTimeComponent selectedComponent = DateTimeComponent.None;
		int currentDigitInsert;
		Gtk.EventControllerKey keyController;
		Gtk.GestureClick clickController;
		Gtk.EventControllerFocus focusController;

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
			InstallControllers();
			Style = DatePickerStyle.DateTime;
			UpdateText();
			Widget.Show();
		}

		protected new IDatePickerEventSink EventSink => (IDatePickerEventSink)base.EventSink;

		public DateTime DateTime {
			get { return currentValue; }
			set {
				SetDateTimeInternal(value, valueChangedEnabled);
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
				UpdateComponentMap();
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
			SelectComponent(selectedComponent);
			updating = false;
		}

		void HandleChanged(object sender, EventArgs e)
		{
			if (updating || entry == null || string.IsNullOrEmpty(format))
				return;
			var text = entry.Text_ ?? string.Empty;
			if (!DateTime.TryParseExact(text, format, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
				return;
			SetDateTimeInternal(parsed, true);
		}

		DateTime Clamp(DateTime value)
		{
			if (value < minValue)
				return minValue;
			if (value > maxValue)
				return maxValue;
			return value;
		}

		void SetDateTimeInternal(DateTime value, bool notify)
		{
			value = Clamp(value);
			if (value == currentValue)
				return;
			currentValue = value;
			UpdateText();
			if (notify && valueChangedEnabled)
				ApplicationContext.InvokeUserCode(EventSink.ValueChanged);
		}

		void InstallControllers()
		{
			keyController = Gtk.EventControllerKey.New();
			keyController.OnKeyPressed += HandleKeyPressed;
			keyController.OnKeyReleased += HandleKeyReleased;
			entry.AddController(keyController);

			clickController = Gtk.GestureClick.New();
			clickController.SetButton(0);
			clickController.OnReleased += HandleClickReleased;
			entry.AddController(clickController);

			focusController = Gtk.EventControllerFocus.New();
			focusController.OnEnter += HandleFocusEnter;
			focusController.OnLeave += HandleFocusLeave;
			entry.AddController(focusController);
		}

		void UpdateComponentMap()
		{
			componentPosition.Clear();
			componentLength.Clear();

			if (style == DatePickerStyle.DateTime || style == DatePickerStyle.Date) {
				componentPosition[DateTimeComponent.Day] = format.IndexOf('d');
				componentPosition[DateTimeComponent.Month] = format.IndexOf('M');
				componentPosition[DateTimeComponent.Year] = format.IndexOf('y');
				componentLength[DateTimeComponent.Day] = 2;
				componentLength[DateTimeComponent.Month] = 2;
				componentLength[DateTimeComponent.Year] = 4;
			}

			if (style == DatePickerStyle.DateTime || style == DatePickerStyle.Time) {
				componentPosition[DateTimeComponent.Hour] = format.IndexOfAny(new[] { 'H', 'h' });
				componentPosition[DateTimeComponent.Minute] = format.IndexOf('m');
				componentPosition[DateTimeComponent.Second] = format.IndexOf('s');
				componentLength[DateTimeComponent.Hour] = 2;
				componentLength[DateTimeComponent.Minute] = 2;
				componentLength[DateTimeComponent.Second] = 2;
			}

			componentsSorted = componentPosition
				.OrderBy(k => k.Value)
				.Select(k => k.Key)
				.ToList();
		}

		bool HandleKeyPressed(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyPressedSignalArgs args)
		{
			var keyval = args.Keyval;
			if (keyval == keyLeft || keyval == keyKpLeft) {
				SelectPrevComponent();
				return true;
			}
			if (keyval == keyRight || keyval == keyKpRight) {
				SelectNextComponent();
				return true;
			}
			if (keyval == keyUp || keyval == keyKpUp) {
				AdjustComponent(1);
				return true;
			}
			if (keyval == keyDown || keyval == keyKpDown) {
				AdjustComponent(-1);
				return true;
			}

			var modifiers = args.State.ToXwtValue();
			if (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Alt))
				return false;

			var unicode = Gdk.Functions.KeyvalToUnicode(args.Keyval);
			if (unicode == 0)
				return false;
			var ch = (char)unicode;
			if (ch == '\t')
				return false;

			return true;
		}

		void HandleKeyReleased(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyReleasedSignalArgs args)
		{
			var modifiers = args.State.ToXwtValue();
			if (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Alt))
				return;

			var unicode = Gdk.Functions.KeyvalToUnicode(args.Keyval);
			if (unicode == 0)
				return;
			var ch = (char)unicode;

			if (char.IsDigit(ch)) {
				EnsureSelection();
				if (selectedComponent != DateTimeComponent.None)
					ApplyDigit(ch);
				return;
			}

			if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSeparator(ch)) {
				if (ch != '\t')
					SelectNextComponent();
			}
		}

		void HandleClickReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if ((PointerButton)sender.GetCurrentButton() != PointerButton.Left)
				return;
			SelectComponentAtPosition(entry.CursorPosition);
		}

		void HandleFocusEnter(Gtk.EventControllerFocus sender, EventArgs args)
		{
			EnsureSelection();
			SelectComponent(selectedComponent);
		}

		void HandleFocusLeave(Gtk.EventControllerFocus sender, EventArgs args)
		{
			SelectComponent(DateTimeComponent.None);
		}

		void EnsureSelection()
		{
			if (selectedComponent == DateTimeComponent.None && componentsSorted.Count > 0)
				selectedComponent = componentsSorted[componentsSorted.Count - 1];
		}

		void AdjustComponent(int delta)
		{
			EnsureSelection();
			if (selectedComponent == DateTimeComponent.None)
				return;
			SetDateTimeInternal(currentValue.AddComponent(selectedComponent, delta), true);
		}

		void ApplyDigit(char digit)
		{
			if (selectedComponent == DateTimeComponent.None)
				return;

			try {
				int value = currentValue.GetComponent(selectedComponent);
				value = AddDigitToValue(value, digit);
				var updated = currentValue.SetComponent(selectedComponent, value);
				SetDateTimeInternal(updated, true);

				if (currentDigitInsert < componentLength[selectedComponent] - 1)
					currentDigitInsert++;
				else
					currentDigitInsert = 0;
			} catch (ArgumentOutOfRangeException) {
				if (digit != '0' && currentDigitInsert != 0) {
					currentDigitInsert = 0;
					ApplyDigit(digit);
				}
			}
		}

		int AddDigitToValue(int baseValue, char newValue)
		{
			if (currentDigitInsert == 0 || currentDigitInsert > componentLength[selectedComponent])
				return int.Parse(newValue.ToString());
			return int.Parse(baseValue.ToString() + newValue);
		}

		void SelectComponentAtPosition(int characterIndex)
		{
			foreach (var entry in componentPosition) {
				if (characterIndex >= entry.Value && characterIndex <= entry.Value + componentLength[entry.Key]) {
					SelectComponent(entry.Key);
					return;
				}
			}
			SelectComponent(DateTimeComponent.None);
		}

		void SelectComponent(DateTimeComponent component)
		{
			int startPos;
			int endPos;
			if (componentPosition.TryGetValue(component, out var start) && componentLength.TryGetValue(component, out var length)) {
				startPos = start;
				endPos = start + length;
			} else {
				startPos = entry.CursorPosition;
				endPos = entry.CursorPosition;
			}

			entry.SelectRegion(startPos, endPos);
			if (selectedComponent != component) {
				selectedComponent = component;
				currentDigitInsert = 0;
			}
		}

		void SelectNextComponent()
		{
			if (componentsSorted.Count == 0)
				return;
			if (selectedComponent == DateTimeComponent.None ||
			    componentsSorted.IndexOf(selectedComponent) == componentsSorted.Count - 1)
				selectedComponent = componentsSorted[0];
			else
				selectedComponent = componentsSorted[componentsSorted.IndexOf(selectedComponent) + 1];

			SelectComponent(selectedComponent);
		}

		void SelectPrevComponent()
		{
			if (componentsSorted.Count == 0)
				return;
			if (selectedComponent == DateTimeComponent.None ||
			    componentsSorted.IndexOf(selectedComponent) == 0)
				selectedComponent = componentsSorted[componentsSorted.Count - 1];
			else
				selectedComponent = componentsSorted[componentsSorted.IndexOf(selectedComponent) - 1];

			SelectComponent(selectedComponent);
		}
	}

	enum DateTimeComponent
	{
		None = 0,
		Month,
		Day,
		Year,
		Hour,
		Minute,
		Second
	}

	static class DateTimeExtensions
	{
		public static DateTime AddComponent(this DateTime dateTime, DateTimeComponent component, int value)
		{
			try {
				switch (component) {
					case DateTimeComponent.Second:
						return dateTime.AddSeconds(value);
					case DateTimeComponent.Minute:
						return dateTime.AddMinutes(value);
					case DateTimeComponent.Hour:
						return dateTime.AddHours(value);
					case DateTimeComponent.Day:
						return dateTime.AddDays(value);
					case DateTimeComponent.Month:
						return dateTime.AddMonths(value);
					case DateTimeComponent.Year:
						return dateTime.AddYears(value);
					default:
						return dateTime.AddSeconds(value);
				}
			} catch (ArgumentOutOfRangeException) {
				return dateTime;
			}
		}

		public static int GetComponent(this DateTime dateTime, DateTimeComponent component)
		{
			switch (component) {
				case DateTimeComponent.Second:
					return dateTime.Second;
				case DateTimeComponent.Minute:
					return dateTime.Minute;
				case DateTimeComponent.Hour:
					return dateTime.Hour;
				case DateTimeComponent.Day:
					return dateTime.Day;
				case DateTimeComponent.Month:
					return dateTime.Month;
				case DateTimeComponent.Year:
					return dateTime.Year;
				default:
					return 0;
			}
		}

		public static DateTime SetComponent(this DateTime source, DateTimeComponent component, int newValue)
		{
			int year = source.Year;
			int month = source.Month;
			int day = source.Day;
			int hour = source.Hour;
			int minute = source.Minute;
			int second = source.Second;
			switch (component) {
				case DateTimeComponent.Year:
					return new DateTime(newValue, month, day, hour, minute, second);
				case DateTimeComponent.Month:
					return new DateTime(year, newValue, day, hour, minute, second);
				case DateTimeComponent.Day:
					return new DateTime(year, month, newValue, hour, minute, second);
				case DateTimeComponent.Hour:
					return new DateTime(year, month, day, newValue, minute, second);
				case DateTimeComponent.Minute:
					return new DateTime(year, month, day, hour, newValue, second);
				case DateTimeComponent.Second:
					return new DateTime(year, month, day, hour, minute, newValue);
				default:
					return source;
			}
		}
	}
}
