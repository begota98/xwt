using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class CalendarBackend : WidgetBackend, ICalendarBackend
	{
		Gtk.Calendar calendar;
		ICalendarEventSink eventSink;
		DateTime minimumDate;
		DateTime maximumDate;

		public CalendarBackend()
		{
			calendar = Gtk.Calendar.New();
			Widget = calendar;
			calendar.OnDaySelected += HandleDaySelected;
			Widget.Show();
		}

		protected new Gtk.Calendar Widget {
			get { return (Gtk.Calendar)base.Widget; }
			set { base.Widget = value; }
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ICalendarEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is CalendarEvent ev && ev == CalendarEvent.ValueChanged)
				calendar.OnDaySelected += HandleValueChanged;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is CalendarEvent ev && ev == CalendarEvent.ValueChanged)
				calendar.OnDaySelected -= HandleValueChanged;
		}

		public DateTime Date {
			get { return ToSystemDate(calendar.Date); }
			set { calendar.Date = ToGDate(value); }
		}

		public DateTime MinimumDate {
			get { return minimumDate; }
			set {
				if (Date < value)
					Date = value;
				minimumDate = value;
			}
		}

		public DateTime MaximumDate {
			get { return maximumDate; }
			set {
				if (Date > value)
					Date = value;
				maximumDate = value;
			}
		}

		void HandleDaySelected(Gtk.Calendar sender, EventArgs e)
		{
			if (Date < MinimumDate)
				Date = MinimumDate;
			if (Date > MaximumDate)
				Date = MaximumDate;
		}

		void HandleValueChanged(Gtk.Calendar sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnValueChanged);
		}

		static DateTime ToSystemDate(GLib.DateTime value)
		{
			if (value == null)
				return DateTime.MinValue;
			var unixSeconds = value.ToUnix();
			return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.Date;
		}

		static GLib.DateTime ToGDate(DateTime value)
		{
			var dt = value.Date;
			var unixSeconds = new DateTimeOffset(dt).ToUnixTimeSeconds();
			return GLib.DateTime.NewFromUnixLocal(unixSeconds);
		}
	}
}
