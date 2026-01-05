using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class RadioButtonBackend : WidgetBackend, IRadioButtonBackend
	{
		Gtk.CheckButton radioButton;
		IRadioButtonEventSink radioEventSink;
		object group;
		bool internalActiveUpdate;
		bool toggleEventEnabled;

		public RadioButtonBackend()
		{
			radioButton = Gtk.CheckButton.New();
			Widget = radioButton;
			radioButton.Visible = true;
			radioButton.OnToggled += HandleToggled;
		}

		protected new Gtk.CheckButton Widget {
			get { return (Gtk.CheckButton)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IRadioButtonEventSink EventSink => radioEventSink;

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			radioEventSink = (IRadioButtonEventSink)sink;
		}

		public void SetContent(string label)
		{
			Widget.Label = label ?? string.Empty;
		}

		public void SetContent(IWidgetBackend widget)
		{
			Widget.Child = widget != null ? ((IGtkWidgetBackend)widget).Widget : null;
		}

		public object Group {
			get {
				return group ?? (object)Widget;
			}
			set {
				group = value;
				var groupButton = ExtractGroupButton(value);
				if (groupButton != null)
					Widget.Group = groupButton;
			}
		}

		public bool Active {
			get { return Widget.Active; }
			set {
				internalActiveUpdate = true;
				Widget.Active = value;
				internalActiveUpdate = false;
			}
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is RadioButtonEvent ev) {
				switch (ev) {
					case RadioButtonEvent.ActiveChanged:
						toggleEventEnabled = true;
						break;
					case RadioButtonEvent.Clicked:
						Widget.OnActivate += HandleClicked;
						break;
				}
			}
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is RadioButtonEvent ev) {
				switch (ev) {
					case RadioButtonEvent.ActiveChanged:
						toggleEventEnabled = false;
						break;
					case RadioButtonEvent.Clicked:
						Widget.OnActivate -= HandleClicked;
						break;
				}
			}
		}

		void HandleToggled(object sender, EventArgs e)
		{
			if (internalActiveUpdate || !toggleEventEnabled || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnToggled);
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (internalActiveUpdate || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnClicked);
		}

		static Gtk.CheckButton ExtractGroupButton(object value)
		{
			if (value is Gtk.CheckButton checkButton)
				return checkButton;
			if (value is RadioButtonBackend radioBackend)
				return radioBackend.Widget;
			return null;
		}
	}
}
