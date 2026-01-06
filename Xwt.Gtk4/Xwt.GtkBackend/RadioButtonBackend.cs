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
		bool rootPressActive;
		Gtk.Widget rootWidget;
		Gtk.GestureClick rootClickController;
		static int nextTraceId;
		readonly int traceId;
		static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("XWT_GTK4_RADIO_TRACE") == "1";

		public RadioButtonBackend()
		{
			traceId = System.Threading.Interlocked.Increment(ref nextTraceId);
			radioButton = Gtk.CheckButton.New();
			Widget = radioButton;
			radioButton.Visible = true;
			radioButton.OnToggled += HandleToggled;
			radioButton.OnMap += HandleMapped;
			radioButton.OnUnmap += HandleUnmapped;
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
			Trace($"toggled active={Widget.Active}");
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (internalActiveUpdate || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnClicked);
		}

		void HandleRootPressed(Gtk.GestureClick sender, Gtk.GestureClick.PressedSignalArgs args)
		{
			if (internalActiveUpdate || !radioButton.Sensitive)
				return;
			if (!IsHitInRadioButton(args.X, args.Y))
				return;
			sender.SetState(Gtk.EventSequenceState.Claimed);
			rootPressActive = true;
			radioButton.GrabFocus();
			radioButton.SetStateFlags(Gtk.StateFlags.Active, false);
			Trace($"root-pressed n={args.NPress} active={Widget.Active}");
		}

		void HandleRootReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if (!rootPressActive)
				return;
			rootPressActive = false;
			if (internalActiveUpdate || !radioButton.Sensitive)
				return;
			bool hit = IsHitInRadioButton(args.X, args.Y);
			if (hit) {
				sender.SetState(Gtk.EventSequenceState.Claimed);
				radioButton.Activate();
				Trace($"root-activate n={args.NPress} active={Widget.Active}");
			}
			radioButton.UnsetStateFlags(Gtk.StateFlags.Active);
		}

		void HandleMapped(Gtk.Widget sender, EventArgs args)
		{
			var root = radioButton.GetRoot() as Gtk.Widget;
			if (root == null || ReferenceEquals(root, rootWidget))
				return;
			DetachRootController();
			rootWidget = root;
			rootClickController = Gtk.GestureClick.New();
			rootClickController.SetButton(1);
			rootClickController.SetExclusive(false);
			rootClickController.PropagationPhase = Gtk.PropagationPhase.Capture;
			rootClickController.OnPressed += HandleRootPressed;
			rootClickController.OnReleased += HandleRootReleased;
			rootWidget.AddController(rootClickController);
			Trace("root controller attached");
		}

		void HandleUnmapped(Gtk.Widget sender, EventArgs args)
		{
			DetachRootController();
		}

		void DetachRootController()
		{
			if (rootWidget == null || rootClickController == null)
				return;
			rootClickController.OnPressed -= HandleRootPressed;
			rootClickController.OnReleased -= HandleRootReleased;
			rootWidget.RemoveController(rootClickController);
			rootClickController.Dispose();
			rootClickController = null;
			rootWidget = null;
		}

		bool IsHitInRadioButton(double x, double y)
		{
			if (rootWidget == null)
				return false;
			var picked = rootWidget.Pick(x, y, Gtk.PickFlags.Default | Gtk.PickFlags.NonTargetable);
			for (var w = picked; w != null; w = w.GetParent()) {
				if (ReferenceEquals(w, radioButton))
					return true;
			}
			return false;
		}

		void Trace(string message)
		{
			if (!TraceEnabled)
				return;
			Console.Error.WriteLine($"[Xwt.Gtk4] RadioButtonBackend#{traceId} {message}");
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
