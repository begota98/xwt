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
		bool rootPressActive;
		Gtk.Widget rootWidget;
		Gtk.GestureClick rootClickController;
		static int nextTraceId;
		readonly int traceId;
		static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("XWT_GTK4_CHECKBOX_TRACE") == "1";

		public CheckBoxBackend()
		{
			traceId = System.Threading.Interlocked.Increment(ref nextTraceId);
			checkButton = Gtk.CheckButton.New();
			Widget = checkButton;
			checkButton.CanTarget = false;
			checkButton.FocusOnClick = true;
			checkButton.CanFocus = true;
			checkButton.Visible = true;
			checkButton.OnToggled += HandleToggled;
			checkButton.OnMap += HandleMapped;
			checkButton.OnUnmap += HandleUnmapped;
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
				internalActiveUpdate = true;
				Widget.Inconsistent = value == CheckBoxState.Mixed;
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
					internalActiveUpdate = true;
					if (Widget.Inconsistent) {
						Widget.Inconsistent = false;
					} else {
						Widget.Inconsistent = true;
						Widget.Active = true;
					}
					internalActiveUpdate = false;
				}
			} else if (Widget.Inconsistent) {
				internalActiveUpdate = true;
				Widget.Inconsistent = false;
				Widget.Active = false;
				internalActiveUpdate = false;
			}

			if (toggleEventEnabled && EventSink != null)
				ApplicationContext.InvokeUserCode(EventSink.OnToggled);

			Trace($"toggled active={Widget.Active} inconsistent={Widget.Inconsistent} allowMixed={allowMixed}");
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (internalActiveUpdate || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnClicked);
		}

		void HandleRootPressed(Gtk.GestureClick sender, Gtk.GestureClick.PressedSignalArgs args)
		{
			if (internalActiveUpdate || !checkButton.Sensitive)
				return;
			if (!IsHitInCheckButton(args.X, args.Y))
				return;
			sender.SetState(Gtk.EventSequenceState.Claimed);
			rootPressActive = true;
			checkButton.GrabFocus();
			checkButton.SetStateFlags(Gtk.StateFlags.Active, false);
			Trace($"root-pressed n={args.NPress} active={Widget.Active} inconsistent={Widget.Inconsistent}");
		}

		void HandleRootReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if (!rootPressActive)
				return;
			rootPressActive = false;
			if (internalActiveUpdate || !checkButton.Sensitive)
				return;
			bool hit = IsHitInCheckButton(args.X, args.Y);
			if (hit) {
				sender.SetState(Gtk.EventSequenceState.Claimed);
				checkButton.Activate();
				Trace($"root-activate n={args.NPress} active={Widget.Active} inconsistent={Widget.Inconsistent}");
			}
			checkButton.UnsetStateFlags(Gtk.StateFlags.Active);
		}

		void HandleMapped(Gtk.Widget sender, EventArgs args)
		{
			var root = checkButton.GetRoot() as Gtk.Widget;
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

		bool IsHitInCheckButton(double x, double y)
		{
			if (rootWidget == null)
				return false;
			var picked = rootWidget.Pick(x, y, Gtk.PickFlags.Default | Gtk.PickFlags.NonTargetable);
			for (var w = picked; w != null; w = w.GetParent()) {
				if (ReferenceEquals(w, checkButton))
					return true;
			}
			return false;
		}

		void Trace(string message)
		{
			if (!TraceEnabled)
				return;
			Console.Error.WriteLine($"[Xwt.Gtk4] CheckBoxBackend#{traceId} {message}");
		}

	}
}
