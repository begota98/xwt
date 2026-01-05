using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class CheckBoxBackend : WidgetBackend, ICheckBoxBackend
	{
		Gtk.CheckButton checkButton;
		readonly System.Collections.Generic.List<Gtk.GestureClick> clickControllers = new System.Collections.Generic.List<Gtk.GestureClick>();
		readonly System.Collections.Generic.HashSet<IntPtr> clickControllerTargets = new System.Collections.Generic.HashSet<IntPtr>();
		ICheckBoxEventSink checkBoxEventSink;
		bool allowMixed;
		bool internalActiveUpdate;
		bool toggleEventEnabled;
		bool clickBaselineActive;
		bool clickBaselineInconsistent;
		bool clickSequenceActive;
		bool toggledDuringClick;
		uint clickSequenceId;
		Gtk.Widget rootWidget;
		Gtk.GestureClick rootClickController;
		static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("XWT_GTK4_CHECKBOX_TRACE") == "1";

		public CheckBoxBackend()
		{
			checkButton = Gtk.CheckButton.New();
			Widget = checkButton;
			checkButton.CanTarget = true;
			checkButton.FocusOnClick = true;
			checkButton.CanFocus = true;
			checkButton.Visible = true;
			checkButton.OnToggled += HandleToggled;
			AttachClickController(checkButton);
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				AttachChildControllers(checkButton);
				return false;
			}));
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

			if (clickSequenceActive)
				toggledDuringClick = true;

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

			Trace($"toggled active={Widget.Active} inconsistent={Widget.Inconsistent} allowMixed={allowMixed} clickActive={clickSequenceActive}");
		}

		void HandleClicked(object sender, EventArgs e)
		{
			if (internalActiveUpdate || EventSink == null)
				return;
			ApplicationContext.InvokeUserCode(EventSink.OnClicked);
		}

		void HandleClickPressed(Gtk.GestureClick sender, Gtk.GestureClick.PressedSignalArgs args)
		{
			BeginClickSequence($"pressed n={args.NPress}");
		}

		void HandleClickReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			EndClickSequence($"released n={args.NPress}");
		}

		void HandleRootPressed(Gtk.GestureClick sender, Gtk.GestureClick.PressedSignalArgs args)
		{
			if (!IsHitInCheckButton(args.X, args.Y))
				return;
			BeginClickSequence($"root-pressed n={args.NPress}");
		}

		void HandleRootReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if (!IsHitInCheckButton(args.X, args.Y))
				return;
			EndClickSequence($"root-released n={args.NPress}");
		}

		void BeginClickSequence(string context)
		{
			if (internalActiveUpdate)
				return;
			if (clickSequenceActive) {
				Trace($"{context} skipped: click sequence active");
				return;
			}
			clickBaselineActive = Widget.Active;
			clickBaselineInconsistent = Widget.Inconsistent;
			clickSequenceActive = true;
			toggledDuringClick = false;
			clickSequenceId++;
			Trace($"{context} active={Widget.Active} inconsistent={Widget.Inconsistent}");
		}

		void EndClickSequence(string context)
		{
			if (internalActiveUpdate || !clickSequenceActive)
				return;
			var sequenceId = clickSequenceId;
			Trace($"{context} active={Widget.Active} inconsistent={Widget.Inconsistent}");
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				if (!clickSequenceActive || clickSequenceId != sequenceId)
					return false;
				if (toggledDuringClick || Widget.Active != clickBaselineActive || Widget.Inconsistent != clickBaselineInconsistent) {
					clickSequenceActive = false;
					Trace("idle: toggle already applied");
					return false;
				}
				Widget.Active = !clickBaselineActive;
				Trace($"fallback-toggle active={Widget.Active} inconsistent={Widget.Inconsistent}");
				clickSequenceActive = false;
				return false;
			}));
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
			var picked = rootWidget.Pick(x, y, Gtk.PickFlags.Default);
			for (var w = picked; w != null; w = w.GetParent()) {
				if (ReferenceEquals(w, checkButton))
					return true;
			}
			return false;
		}

		void AttachChildControllers(Gtk.Widget parent)
		{
			var child = parent.GetFirstChild();
			while (child != null) {
				AttachClickController(child);
				AttachChildControllers(child);
				child = child.GetNextSibling();
			}
		}

		void AttachClickController(Gtk.Widget target)
		{
			if (target == null)
				return;
			var handle = target.Handle.DangerousGetHandle();
			if (!clickControllerTargets.Add(handle))
				return;
			var controller = Gtk.GestureClick.New();
			controller.SetButton(1);
			controller.SetExclusive(false);
			controller.PropagationPhase = Gtk.PropagationPhase.Target;
			controller.OnPressed += HandleClickPressed;
			controller.OnReleased += HandleClickReleased;
			target.AddController(controller);
			clickControllers.Add(controller);
		}

		static void Trace(string message)
		{
			if (!TraceEnabled)
				return;
			Console.Error.WriteLine($"[Xwt.Gtk4] CheckBoxBackend {message}");
		}

	}
}
