using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class RadioButtonBackend : WidgetBackend, IRadioButtonBackend
	{
		Gtk.CheckButton radioButton;
		readonly System.Collections.Generic.List<Gtk.GestureClick> clickControllers = new System.Collections.Generic.List<Gtk.GestureClick>();
		readonly System.Collections.Generic.HashSet<IntPtr> clickControllerTargets = new System.Collections.Generic.HashSet<IntPtr>();
		IRadioButtonEventSink radioEventSink;
		object group;
		bool internalActiveUpdate;
		bool toggleEventEnabled;
		bool clickBaselineActive;
		bool clickSequenceActive;
		bool toggledDuringClick;
		uint clickSequenceId;
		Gtk.Widget rootWidget;
		Gtk.GestureClick rootClickController;
		static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("XWT_GTK4_RADIO_TRACE") == "1";

		public RadioButtonBackend()
		{
			radioButton = Gtk.CheckButton.New();
			Widget = radioButton;
			radioButton.Visible = true;
			radioButton.OnToggled += HandleToggled;
			AttachClickController(radioButton);
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				AttachChildControllers(radioButton);
				return false;
			}));
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
			if (clickSequenceActive)
				toggledDuringClick = true;
			ApplicationContext.InvokeUserCode(EventSink.OnToggled);
			Trace($"toggled active={Widget.Active} clickActive={clickSequenceActive}");
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
			if (!IsHitInRadioButton(args.X, args.Y))
				return;
			BeginClickSequence($"root-pressed n={args.NPress}");
		}

		void HandleRootReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if (!IsHitInRadioButton(args.X, args.Y))
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
			clickSequenceActive = true;
			toggledDuringClick = false;
			clickSequenceId++;
			Trace($"{context} active={Widget.Active}");
		}

		void EndClickSequence(string context)
		{
			if (internalActiveUpdate || !clickSequenceActive)
				return;
			var sequenceId = clickSequenceId;
			Trace($"{context} active={Widget.Active}");
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				if (!clickSequenceActive || clickSequenceId != sequenceId)
					return false;
				if (toggledDuringClick || Widget.Active != clickBaselineActive) {
					clickSequenceActive = false;
					Trace("idle: toggle already applied");
					return false;
				}
				Widget.Active = true;
				Trace($"fallback-activate active={Widget.Active}");
				clickSequenceActive = false;
				return false;
			}));
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
			var picked = rootWidget.Pick(x, y, Gtk.PickFlags.Default);
			for (var w = picked; w != null; w = w.GetParent()) {
				if (ReferenceEquals(w, radioButton))
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
			Console.Error.WriteLine($"[Xwt.Gtk4] RadioButtonBackend {message}");
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
