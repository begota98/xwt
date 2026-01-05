using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class CheckBoxBackend : WidgetBackend, ICheckBoxBackend
	{
		Gtk.CheckButton checkButton;
		ICheckBoxEventSink checkBoxEventSink;
		bool allowMixed;
		bool internalActiveUpdate;
		bool toggleEventEnabled;

		public CheckBoxBackend()
		{
			checkButton = Gtk.CheckButton.New();
			Widget = checkButton;
			checkButton.Visible = true;
			checkButton.OnToggled += HandleToggled;
		}

		protected new Gtk.CheckButton Widget {
			get { return (Gtk.CheckButton)base.Widget; }
			set { base.Widget = value; }
		}

		protected new ICheckBoxEventSink EventSink => checkBoxEventSink;

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			checkBoxEventSink = (ICheckBoxEventSink)sink;
		}

		public bool AllowMixed {
			get { return allowMixed; }
			set { allowMixed = value; }
		}

		public CheckBoxState State {
			get {
				if (Widget.Inconsistent)
					return CheckBoxState.Mixed;
				return Widget.Active ? CheckBoxState.On : CheckBoxState.Off;
			}
			set {
				Widget.Inconsistent = value == CheckBoxState.Mixed;
				internalActiveUpdate = true;
				Widget.Active = value != CheckBoxState.Off;
				internalActiveUpdate = false;
			}
		}

		public void SetContent(string label, bool useMnemonic)
		{
			Widget.Label = label ?? string.Empty;
			Widget.UseUnderline = useMnemonic;
		}

		public void SetContent(IWidgetBackend widget)
		{
			Widget.Child = widget != null ? ((IGtkWidgetBackend)widget).Widget : null;
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is CheckBoxEvent ev) {
				switch (ev) {
					case CheckBoxEvent.Toggled:
						toggleEventEnabled = true;
						break;
					case CheckBoxEvent.Clicked:
						Widget.OnActivate += HandleClicked;
						break;
				}
			}
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is CheckBoxEvent ev) {
				switch (ev) {
					case CheckBoxEvent.Toggled:
						toggleEventEnabled = false;
						break;
					case CheckBoxEvent.Clicked:
						Widget.OnActivate -= HandleClicked;
						break;
				}
			}
		}

		void HandleToggled(object sender, EventArgs e)
		{
			if (internalActiveUpdate)
				return;

			if (allowMixed) {
				if (!Widget.Active) {
					if (Widget.Inconsistent) {
						Widget.Inconsistent = false;
					} else {
						Widget.Inconsistent = true;
						internalActiveUpdate = true;
						Widget.Active = true;
						internalActiveUpdate = false;
					}
				}
			} else if (Widget.Inconsistent) {
				Widget.Inconsistent = false;
				Widget.Active = false;
			}

			if (toggleEventEnabled && EventSink != null)
				ApplicationContext.InvokeUserCode(EventSink.OnToggled);
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (internalActiveUpdate || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnClicked);
		}
	}
}
