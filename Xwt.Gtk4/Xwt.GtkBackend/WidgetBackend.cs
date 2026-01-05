using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gio;
using GObject;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class WidgetBackend : IWidgetBackend, IGtkWidgetBackend
	{
		Gtk.Widget widget;
		Widget frontend;
		IWidgetEventSink eventSink;
		Pango.FontDescription customFont;
		Gtk.CssProvider fontProvider;
		string fontCssClass;
		static int fontClassId;
		Color? customBackgroundColor;
		Gtk.CssProvider backgroundProvider;
		string backgroundCssClass;
		static int backgroundClassId;
		WidgetEvent enabledEvents;
		Gtk.EventControllerKey keyController;
		Gtk.EventControllerFocus focusController;
		Gtk.EventControllerMotion motionController;
		Gtk.GestureClick clickController;
		Gtk.EventControllerScroll scrollController;
		Gtk.DropTargetAsync dropTarget;
		Gtk.DragSource dragSource;
		Gtk.IMContext imContext;
		uint boundsTickId;
		int lastAllocatedWidth;
		int lastAllocatedHeight;
		TransferDataType[] dragSourceTypes = Array.Empty<TransferDataType>();
		TransferDataType[] dragTargetTypes = Array.Empty<TransferDataType>();
		Gdk.DragAction dragSourceActions;
		Gdk.DragAction dragTargetActions;
		TransferDataSource currentDragData;
		DragStartData currentDragStartData;
		static readonly WidgetEvent dragDropEvents = WidgetEvent.DragOverCheck | WidgetEvent.DragOver | WidgetEvent.DragDropCheck | WidgetEvent.DragDrop | WidgetEvent.DragLeave;

		public Gtk.Widget Widget {
			get { return widget; }
			protected set { widget = value; }
		}

		Gtk.Widget IGtkWidgetBackend.Widget => widget;

		public Widget Frontend => frontend;

		public ApplicationContext ApplicationContext { get; private set; }

		public virtual object NativeWidget => widget;

		void IBackend.InitializeBackend(object frontend, ApplicationContext context)
		{
			this.frontend = (Widget)frontend;
			ApplicationContext = context;
		}

		public virtual void Initialize(IWidgetEventSink sink)
		{
			eventSink = sink;
		}

		public virtual void Dispose()
		{
			widget?.Dispose();
			widget = null;
		}

		public virtual bool Visible {
			get { return widget?.Visible ?? false; }
			set {
				if (widget != null)
					widget.Visible = value;
			}
		}

		public virtual bool Sensitive {
			get { return widget?.Sensitive ?? false; }
			set {
				if (widget != null)
					widget.Sensitive = value;
			}
		}

		public virtual string Name {
			get { return widget?.Name ?? string.Empty; }
			set {
				if (widget != null)
					widget.Name = value;
			}
		}

		public virtual string TooltipText {
			get { return widget?.TooltipText ?? string.Empty; }
			set {
				if (widget != null)
					widget.TooltipText = value;
			}
		}

		public virtual bool CanGetFocus {
			get { return widget?.CanFocus ?? false; }
			set {
				if (widget != null)
					widget.CanFocus = value;
			}
		}

		public virtual bool HasFocus => widget?.HasFocus ?? false;

		public virtual double Opacity {
			get { return widget?.Opacity ?? 1d; }
			set {
				if (widget != null)
					widget.Opacity = value;
			}
		}

		public virtual Size Size {
			get {
				if (widget == null)
					return Size.Zero;
				return new Size(widget.GetAllocatedWidth(), widget.GetAllocatedHeight());
			}
		}

		public virtual Point ConvertToParentCoordinates(Point widgetCoordinates)
		{
			if (widget == null)
				return widgetCoordinates;
			double x = 0;
			double y = 0;
			var parent = widget.GetParent();
			if (parent != null)
				widget.TranslateCoordinates(parent, 0, 0, out x, out y);
			return new Point(x + widgetCoordinates.X, y + widgetCoordinates.Y);
		}

		public virtual Point ConvertToWindowCoordinates(Point widgetCoordinates)
		{
			if (widget == null)
				return widgetCoordinates;
			double x = 0;
			double y = 0;
			var root = widget.GetRoot() as Gtk.Widget;
			if (root != null)
				widget.TranslateCoordinates(root, 0, 0, out x, out y);
			return new Point(x + widgetCoordinates.X, y + widgetCoordinates.Y);
		}

		public virtual Point ConvertToScreenCoordinates(Point widgetCoordinates)
		{
			return ConvertToWindowCoordinates(widgetCoordinates);
		}

		public virtual void SetMinSize(double width, double height)
		{
			if (widget == null)
				return;
			widget.SetSizeRequest((int)width, (int)height);
		}

		public virtual void SetSizeRequest(double width, double height)
		{
			if (widget == null)
				return;
			widget.SetSizeRequest((int)width, (int)height);
		}

		public virtual void SetFocus()
		{
			widget?.GrabFocus();
		}

		public virtual void SetCursor(CursorType cursor)
		{
			if (widget == null)
				return;
			var name = GetCursorName(cursor);
			if (string.IsNullOrEmpty(name)) {
				widget.SetCursor(null);
				return;
			}
			widget.SetCursorFromName(name);
		}

		public virtual void UpdateLayout()
		{
			widget?.QueueResize();
		}

		public virtual Size GetPreferredSize(SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			if (widget == null)
				return Size.Zero;
			int minW, natW, minH, natH;
			int heightForWidth = heightConstraint.IsConstrained ? (int)heightConstraint.AvailableSize : -1;
			widget.Measure(Gtk.Orientation.Horizontal, heightForWidth, out minW, out natW, out _, out _);

			int widthForHeight = widthConstraint.IsConstrained ? (int)widthConstraint.AvailableSize : -1;
			if (widthForHeight != -1 && widthForHeight < minW)
				widthForHeight = minW;
			widget.Measure(Gtk.Orientation.Vertical, widthForHeight, out minH, out natH, out _, out _);

			if ((enabledEvents & WidgetEvent.PreferredSizeCheck) != 0 && eventSink != null) {
				var wc = widthConstraint;
				var hc = heightConstraint;
				ApplicationContext.InvokeUserCode(() => {
					var size = eventSink.GetPreferredSize(wc, hc);
					minW = (int)size.Width;
					minH = (int)size.Height;
				});
			}

			if (widget.WidthRequest > minW)
				minW = widget.WidthRequest;
			if (widget.HeightRequest > minH)
				minH = widget.HeightRequest;
			if (frontend != null) {
				if (frontend.MinWidth > 0 && frontend.MinWidth > minW)
					minW = (int)frontend.MinWidth;
				if (frontend.MinHeight > 0 && frontend.MinHeight > minH)
					minH = (int)frontend.MinHeight;
			}

			return new Size(minW, minH);
		}

		public virtual void DragStart(DragStartData data)
		{
			if (widget == null || data == null || data.Data == null)
				return;

			var provider = BuildContentProvider(data.Data, data.Data.DataTypes);
			if (provider == null)
				return;

			var surface = widget.GetNative()?.GetSurface();
			var device = Gdk.Display.GetDefault()?.GetDefaultSeat()?.GetPointer();
			if (surface == null || device == null)
				return;

			var actions = data.DragAction == DragDropAction.Default ? DragDropAction.All : data.DragAction;
			var drag = Gdk.Drag.Begin(surface, device, provider, ConvertDragAction(actions), data.HotX, data.HotY);
			if (drag == null)
				return;

			currentDragStartData = data;
			currentDragData = data.Data;
			drag.OnDndFinished += HandleManualDragFinished;
			drag.OnCancel += HandleManualDragCancel;
		}

		public virtual void SetDragSource(TransferDataType[] types, DragDropAction dragAction)
		{
			dragSourceTypes = types ?? Array.Empty<TransferDataType>();
			dragSourceActions = ConvertDragAction(dragAction == DragDropAction.Default ? DragDropAction.All : dragAction);

			if (widget == null)
				return;

			if (dragSourceTypes.Length == 0) {
				if (dragSource != null) {
					widget.RemoveController(dragSource);
					dragSource.Dispose();
					dragSource = null;
				}
				return;
			}

			if (dragSource == null) {
				dragSource = Gtk.DragSource.New();
				dragSource.OnPrepare += HandleDragSourcePrepare;
				dragSource.OnDragBegin += HandleDragSourceBegin;
				dragSource.OnDragEnd += HandleDragSourceEnd;
				dragSource.OnDragCancel += HandleDragSourceCancel;
				widget.AddController(dragSource);
			}
			dragSource.Actions = dragSourceActions;
		}

		public virtual void SetDragTarget(TransferDataType[] types, DragDropAction dragAction)
		{
			dragTargetTypes = types ?? Array.Empty<TransferDataType>();
			dragTargetActions = ConvertDragAction(dragAction == DragDropAction.Default ? DragDropAction.All : dragAction);

			if (widget == null)
				return;

			if (dragTargetTypes.Length == 0) {
				if (dropTarget != null) {
					widget.RemoveController(dropTarget);
					dropTarget.Dispose();
					dropTarget = null;
				}
				return;
			}

			EnsureDropTarget();
			if (dropTarget != null) {
				dropTarget.SetActions(dragTargetActions);
				dropTarget.SetFormats(Gdk.ContentFormats.New(GetMimeTypes(dragTargetTypes)));
			}
		}

		public virtual object Font {
			get {
				if (customFont != null)
					return customFont;
				if (widget != null) {
					using var layout = widget.CreatePangoLayout(string.Empty);
					var desc = layout.GetFontDescription();
					if (desc != null)
						return desc;
				}
				var fallback = Pango.FontDescription.New();
				fallback.SetFamily("Sans");
				fallback.SetSize((int)(10 * Pango.Constants.SCALE));
				return fallback;
			}
			set {
				customFont = value as Pango.FontDescription;
				ApplyFont();
			}
		}

		public virtual Color BackgroundColor {
			get { return customBackgroundColor ?? Colors.Transparent; }
			set {
				customBackgroundColor = value;
				ApplyBackgroundColor();
			}
		}

		public virtual bool UsingCustomBackgroundColor => customBackgroundColor.HasValue;

		public virtual void EnableEvent(object eventId)
		{
			if (eventId is WidgetEvent ev) {
				enabledEvents |= ev;

				if ((ev & (WidgetEvent.KeyPressed | WidgetEvent.KeyReleased | WidgetEvent.TextInput)) != 0)
					EnsureKeyController();
				if ((ev & (WidgetEvent.MouseEntered | WidgetEvent.MouseExited | WidgetEvent.MouseMoved)) != 0)
					EnsureMotionController();
				if ((ev & (WidgetEvent.ButtonPressed | WidgetEvent.ButtonReleased)) != 0)
					EnsureClickController();
				if ((ev & WidgetEvent.MouseScrolled) != 0)
					EnsureScrollController();
				if ((ev & (WidgetEvent.GotFocus | WidgetEvent.LostFocus | WidgetEvent.TextInput)) != 0)
					EnsureFocusController();
				if ((ev & WidgetEvent.BoundsChanged) != 0)
					EnableBoundsTracking();
				if ((ev & dragDropEvents) != 0)
					EnsureDropTarget();
				if ((ev & WidgetEvent.TextInput) != 0)
					EnsureTextInput();
			}
		}

		public virtual void DisableEvent(object eventId)
		{
			if (eventId is WidgetEvent ev) {
				enabledEvents &= ~ev;
				if ((enabledEvents & WidgetEvent.BoundsChanged) == 0)
					DisableBoundsTracking();
				if ((enabledEvents & WidgetEvent.TextInput) == 0)
					DisableTextInput();
			}
		}

		protected IWidgetEventSink EventSink => eventSink;

		internal IWidgetEventSink GetEventSink()
		{
			return eventSink;
		}

		internal bool IsEventEnabled(WidgetEvent ev)
		{
			return (enabledEvents & ev) != 0;
		}

		internal static void ApplyChildPlacement(IWidgetBackend childBackend)
		{
			if (!(childBackend is WidgetBackend backend))
				return;
			if (backend.Widget == null || backend.Frontend == null)
				return;

			var h = backend.Frontend.HorizontalPlacement;
			var v = backend.Frontend.VerticalPlacement;
			backend.Widget.Halign = ToGtkAlign(h);
			backend.Widget.Valign = ToGtkAlign(v);
			backend.Widget.Hexpand = h == WidgetPlacement.Fill;
			backend.Widget.Vexpand = v == WidgetPlacement.Fill;
		}

		static string GetCursorName(CursorType cursor)
		{
			if (cursor == null)
				return null;
			if (ReferenceEquals(cursor, CursorType.Arrow))
				return "default";
			if (ReferenceEquals(cursor, CursorType.Crosshair))
				return "crosshair";
			if (ReferenceEquals(cursor, CursorType.Hand) || ReferenceEquals(cursor, CursorType.Hand2) || ReferenceEquals(cursor, CursorType.DragCopy))
				return "pointer";
			if (ReferenceEquals(cursor, CursorType.IBeam))
				return "text";
			if (ReferenceEquals(cursor, CursorType.NotAllowed))
				return "not-allowed";
			if (ReferenceEquals(cursor, CursorType.ResizeLeftRight))
				return "ew-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeUpDown))
				return "ns-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeNE) || ReferenceEquals(cursor, CursorType.ResizeSW))
				return "nesw-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeNW) || ReferenceEquals(cursor, CursorType.ResizeSE))
				return "nwse-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeLeft))
				return "w-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeRight))
				return "e-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeUp))
				return "n-resize";
			if (ReferenceEquals(cursor, CursorType.ResizeDown))
				return "s-resize";
			return null;
		}

		static Gtk.Align ToGtkAlign(WidgetPlacement placement)
		{
			switch (placement) {
				case WidgetPlacement.Center:
					return Gtk.Align.Center;
				case WidgetPlacement.End:
					return Gtk.Align.End;
				case WidgetPlacement.Fill:
					return Gtk.Align.Fill;
				default:
					return Gtk.Align.Start;
			}
		}

		void EnsureKeyController()
		{
			if (keyController != null || widget == null)
				return;
			keyController = Gtk.EventControllerKey.New();
			keyController.OnKeyPressed += HandleKeyPressed;
			keyController.OnKeyReleased += HandleKeyReleased;
			widget.AddController(keyController);
		}

		void EnsureFocusController()
		{
			if (focusController != null || widget == null)
				return;
			focusController = Gtk.EventControllerFocus.New();
			focusController.OnEnter += HandleFocusEnter;
			focusController.OnLeave += HandleFocusLeave;
			widget.AddController(focusController);
		}

		void EnsureMotionController()
		{
			if (motionController != null || widget == null)
				return;
			motionController = Gtk.EventControllerMotion.New();
			motionController.OnEnter += HandleMouseEnter;
			motionController.OnLeave += HandleMouseLeave;
			motionController.OnMotion += HandleMouseMotion;
			widget.AddController(motionController);
		}

		void EnsureClickController()
		{
			if (clickController != null || widget == null)
				return;
			clickController = Gtk.GestureClick.New();
			clickController.SetButton(0);
			clickController.OnPressed += HandleButtonPressed;
			clickController.OnReleased += HandleButtonReleased;
			widget.AddController(clickController);
		}

		void EnsureScrollController()
		{
			if (scrollController != null || widget == null)
				return;
			scrollController = Gtk.EventControllerScroll.New(Gtk.EventControllerScrollFlags.BothAxes);
			scrollController.OnScroll += HandleScroll;
			widget.AddController(scrollController);
		}

		void EnsureDropTarget()
		{
			if (dropTarget != null || widget == null)
				return;
			dropTarget = Gtk.DropTargetAsync.New(null, (Gdk.DragAction)0);
			dropTarget.OnAccept += HandleDropAccept;
			dropTarget.OnDragEnter += HandleDropEnter;
			dropTarget.OnDragMotion += HandleDropMotion;
			dropTarget.OnDragLeave += HandleDropLeave;
			dropTarget.OnDrop += HandleDrop;
			widget.AddController(dropTarget);
		}

		void EnsureTextInput()
		{
			if (imContext != null)
				return;
			imContext = Gtk.IMMulticontext.New();
			imContext.OnCommit += HandleImCommit;
		}

		void DisableTextInput()
		{
			if (imContext == null)
				return;
			imContext.OnCommit -= HandleImCommit;
			imContext.Dispose();
			imContext = null;
		}

		void EnableBoundsTracking()
		{
			if (boundsTickId != 0 || widget == null)
				return;
			lastAllocatedWidth = widget.GetAllocatedWidth();
			lastAllocatedHeight = widget.GetAllocatedHeight();
			boundsTickId = widget.AddTickCallback(HandleBoundsTick);
		}

		void DisableBoundsTracking()
		{
			if (boundsTickId == 0 || widget == null)
				return;
			widget.RemoveTickCallback(boundsTickId);
			boundsTickId = 0;
		}

		bool HandleBoundsTick(Gtk.Widget tickWidget, Gdk.FrameClock frameClock)
		{
			if ((enabledEvents & WidgetEvent.BoundsChanged) == 0)
				return true;
			var width = tickWidget.GetAllocatedWidth();
			var height = tickWidget.GetAllocatedHeight();
			if (width == lastAllocatedWidth && height == lastAllocatedHeight)
				return true;
			lastAllocatedWidth = width;
			lastAllocatedHeight = height;
			ApplicationContext?.InvokeUserCode(eventSink.OnBoundsChanged);
			return true;
		}

		bool HandleKeyPressed(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyPressedSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.TextInput) != 0 && imContext != null) {
				var currentEvent = sender.GetCurrentEvent();
				if (currentEvent != null)
					imContext.FilterKeypress(currentEvent);
			}

			if ((enabledEvents & WidgetEvent.KeyPressed) == 0 || eventSink == null)
				return false;

			var key = (Key)args.Keyval;
			var modifiers = args.State.ToXwtValue();
			var timestamp = (long)sender.GetCurrentEventTime();
			var kargs = new KeyEventArgs(key, (int)args.Keycode, modifiers, false, timestamp);
			ApplicationContext.InvokeUserCode(() => eventSink.OnKeyPressed(kargs));
			return kargs.Handled;
		}

		void HandleKeyReleased(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyReleasedSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.TextInput) != 0 && imContext != null) {
				var currentEvent = sender.GetCurrentEvent();
				if (currentEvent != null)
					imContext.FilterKeypress(currentEvent);
			}

			if ((enabledEvents & WidgetEvent.KeyReleased) == 0 || eventSink == null)
				return;

			var key = (Key)args.Keyval;
			var modifiers = args.State.ToXwtValue();
			var timestamp = (long)sender.GetCurrentEventTime();
			var kargs = new KeyEventArgs(key, (int)args.Keycode, modifiers, false, timestamp);
			ApplicationContext.InvokeUserCode(() => eventSink.OnKeyReleased(kargs));
		}

		void HandleImCommit(Gtk.IMContext sender, Gtk.IMContext.CommitSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.TextInput) == 0 || eventSink == null)
				return;
			if (string.IsNullOrEmpty(args.Str))
				return;
			var targs = new TextInputEventArgs(args.Str);
			ApplicationContext.InvokeUserCode(() => eventSink.OnTextInput(targs));
		}

		void HandleFocusEnter(Gtk.EventControllerFocus sender, EventArgs args)
		{
			imContext?.FocusIn();
			if ((enabledEvents & WidgetEvent.GotFocus) == 0 || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnGotFocus);
		}

		void HandleFocusLeave(Gtk.EventControllerFocus sender, EventArgs args)
		{
			imContext?.FocusOut();
			if ((enabledEvents & WidgetEvent.LostFocus) == 0 || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnLostFocus);
		}

		void HandleMouseEnter(Gtk.EventControllerMotion sender, Gtk.EventControllerMotion.EnterSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.MouseEntered) == 0 || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnMouseEntered);
		}

		void HandleMouseLeave(Gtk.EventControllerMotion sender, EventArgs args)
		{
			if ((enabledEvents & WidgetEvent.MouseExited) == 0 || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnMouseExited);
		}

		void HandleMouseMotion(Gtk.EventControllerMotion sender, Gtk.EventControllerMotion.MotionSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.MouseMoved) == 0 || eventSink == null)
				return;
			var timestamp = (long)sender.GetCurrentEventTime();
			var margs = new MouseMovedEventArgs(timestamp, args.X, args.Y);
			ApplicationContext.InvokeUserCode(() => eventSink.OnMouseMoved(margs));
		}

		void HandleButtonPressed(Gtk.GestureClick sender, Gtk.GestureClick.PressedSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.ButtonPressed) == 0 || eventSink == null)
				return;
			var button = (PointerButton)sender.GetCurrentButton();
			var bargs = new ButtonEventArgs {
				Button = button,
				MultiplePress = args.NPress,
				X = args.X,
				Y = args.Y,
				IsContextMenuTrigger = button == PointerButton.Right
			};
			ApplicationContext.InvokeUserCode(() => eventSink.OnButtonPressed(bargs));
		}

		void HandleButtonReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.ButtonReleased) == 0 || eventSink == null)
				return;
			var button = (PointerButton)sender.GetCurrentButton();
			var bargs = new ButtonEventArgs {
				Button = button,
				MultiplePress = args.NPress,
				X = args.X,
				Y = args.Y,
				IsContextMenuTrigger = button == PointerButton.Right
			};
			ApplicationContext.InvokeUserCode(() => eventSink.OnButtonReleased(bargs));
		}

		bool HandleScroll(Gtk.EventControllerScroll sender, Gtk.EventControllerScroll.ScrollSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.MouseScrolled) == 0 || eventSink == null)
				return false;
			if (args.Dx == 0 && args.Dy == 0)
				return false;

			double x = 0;
			double y = 0;
			var currentEvent = sender.GetCurrentEvent();
			if (currentEvent != null)
				currentEvent.GetPosition(out x, out y);

			ScrollDirection direction;
			if (Math.Abs(args.Dy) >= Math.Abs(args.Dx))
				direction = args.Dy < 0 ? ScrollDirection.Up : ScrollDirection.Down;
			else
				direction = args.Dx < 0 ? ScrollDirection.Left : ScrollDirection.Right;

			var timestamp = (long)sender.GetCurrentEventTime();
			var sargs = new MouseScrolledEventArgs(timestamp, x, y, direction);
			ApplicationContext.InvokeUserCode(() => eventSink.OnMouseScrolled(sargs));
			return sargs.Handled;
		}

		Gdk.DragAction HandleDropEnter(Gtk.DropTargetAsync sender, Gtk.DropTargetAsync.DragEnterSignalArgs args)
		{
			return HandleDropMotionInternal(args.Drop, args.X, args.Y);
		}

		Gdk.DragAction HandleDropMotion(Gtk.DropTargetAsync sender, Gtk.DropTargetAsync.DragMotionSignalArgs args)
		{
			return HandleDropMotionInternal(args.Drop, args.X, args.Y);
		}

		void HandleDropLeave(Gtk.DropTargetAsync sender, Gtk.DropTargetAsync.DragLeaveSignalArgs args)
		{
			if ((enabledEvents & WidgetEvent.DragLeave) == 0 || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(() => eventSink.OnDragLeave(EventArgs.Empty));
		}

		bool HandleDropAccept(Gtk.DropTargetAsync sender, Gtk.DropTargetAsync.AcceptSignalArgs args)
		{
			if (dragTargetTypes.Length == 0)
				return false;
			var types = FilterTransferTypes(GetTransferTypes(args.Drop?.Formats));
			return types.Length > 0;
		}

		bool HandleDrop(Gtk.DropTargetAsync sender, Gtk.DropTargetAsync.DropSignalArgs args)
		{
			if (eventSink == null)
				return false;

			var position = new Point(args.X, args.Y);
			var action = GetDropAction(args.Drop);
			DragDropResult result;

			if ((enabledEvents & WidgetEvent.DragDropCheck) != 0) {
				var types = FilterTransferTypes(GetTransferTypes(args.Drop?.Formats));
				var checkArgs = new DragCheckEventArgs(position, types, action);
				ApplicationContext.InvokeUserCode(() => eventSink.OnDragDropCheck(checkArgs));
				result = checkArgs.Result;
				if ((enabledEvents & WidgetEvent.DragDrop) == 0 && result == DragDropResult.None)
					result = DragDropResult.Canceled;
			} else {
				if ((enabledEvents & WidgetEvent.DragDrop) != 0)
					result = DragDropResult.None;
				else
					result = DragDropResult.Canceled;
			}

			if (result == DragDropResult.Canceled) {
				args.Drop?.Finish(Gdk.DragAction.None);
				return true;
			}

			if (result == DragDropResult.Success) {
				args.Drop?.Finish(ConvertDragAction(action));
				return true;
			}

			BeginDropRead(args.Drop, position, false, action);
			return true;
		}

		Gdk.DragAction HandleDropMotionInternal(Gdk.Drop drop, double x, double y)
		{
			if (eventSink == null || drop == null)
				return Gdk.DragAction.None;

			var position = new Point(x, y);
			var availableAction = ResolveAllowedAction(DragDropAction.Default, drop.Actions);

			DragDropAction allowed;
			if ((enabledEvents & WidgetEvent.DragOverCheck) != 0) {
				var types = FilterTransferTypes(GetTransferTypes(drop.Formats));
				var checkArgs = new DragOverCheckEventArgs(position, types, availableAction);
				ApplicationContext.InvokeUserCode(() => eventSink.OnDragOverCheck(checkArgs));
				allowed = checkArgs.AllowedAction;
				if ((enabledEvents & WidgetEvent.DragOver) == 0 && allowed == DragDropAction.Default)
					allowed = DragDropAction.None;
			} else if ((enabledEvents & WidgetEvent.DragOver) != 0) {
				allowed = DragDropAction.Default;
			} else {
				allowed = ConvertDragAction(dragTargetActions);
			}

			if (allowed == DragDropAction.None)
				return Gdk.DragAction.None;

			if (allowed == DragDropAction.Default && (enabledEvents & WidgetEvent.DragOver) != 0)
				BeginDropRead(drop, position, true, availableAction);

			var resolved = ResolveAllowedAction(allowed, drop.Actions);
			return ConvertDragAction(resolved);
		}

		void HandleDragSourceBegin(Gtk.DragSource sender, Gtk.DragSource.DragBeginSignalArgs args)
		{
			if (currentDragStartData?.ImageBackend == null)
				return;
			if (!TryGetDragTexture(currentDragStartData.ImageBackend, out var texture))
				return;
			sender.SetIcon(texture, (int)currentDragStartData.HotX, (int)currentDragStartData.HotY);
		}

		Gdk.ContentProvider HandleDragSourcePrepare(Gtk.DragSource sender, Gtk.DragSource.PrepareSignalArgs args)
		{
			if (eventSink == null || dragSourceTypes.Length == 0 || dragSourceActions == (Gdk.DragAction)0)
				return null;

			DragStartData sdata = null;
			ApplicationContext.InvokeUserCode(() => sdata = eventSink.OnDragStarted());
			if (sdata == null || sdata.Data == null)
				return null;

			currentDragStartData = sdata;
			currentDragData = sdata.Data;
			if (sdata.DragAction != DragDropAction.Default && sdata.DragAction != DragDropAction.None)
				sender.Actions = ConvertDragAction(sdata.DragAction);
			else
				sender.Actions = dragSourceActions;

			return BuildContentProvider(sdata.Data, dragSourceTypes);
		}

		void HandleDragSourceEnd(Gtk.DragSource sender, Gtk.DragSource.DragEndSignalArgs args)
		{
			NotifyDragFinished(args.DeleteData);
		}

		bool HandleDragSourceCancel(Gtk.DragSource sender, Gtk.DragSource.DragCancelSignalArgs args)
		{
			NotifyDragFinished(false);
			return false;
		}

		void HandleManualDragFinished(Gdk.Drag sender, EventArgs args)
		{
			var delete = sender.SelectedAction == Gdk.DragAction.Move;
			NotifyDragFinished(delete);
		}

		void HandleManualDragCancel(Gdk.Drag sender, Gdk.Drag.CancelSignalArgs args)
		{
			NotifyDragFinished(false);
		}

		void NotifyDragFinished(bool deleteSource)
		{
			if (currentDragData == null && currentDragStartData == null)
				return;

			currentDragData = null;
			currentDragStartData = null;

			if (eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(() => eventSink.OnDragFinished(new DragFinishedEventArgs(deleteSource)));
		}

		DragDropAction GetDropAction(Gdk.Drop drop)
		{
			if (drop == null)
				return DragDropAction.None;
			var selected = drop.Drag?.SelectedAction ?? Gdk.DragAction.None;
			var action = ConvertDragAction(selected);
			if (action == DragDropAction.None)
				action = DragDropAction.Default;
			return ResolveAllowedAction(action, drop.Actions);
		}

		DragDropAction ResolveAllowedAction(DragDropAction allowed, Gdk.DragAction availableActions)
		{
			if (allowed != DragDropAction.Default)
				return allowed;
			var available = ConvertDragAction(availableActions);
			if ((available & DragDropAction.Move) != 0)
				return DragDropAction.Move;
			if ((available & DragDropAction.Copy) != 0)
				return DragDropAction.Copy;
			if ((available & DragDropAction.Link) != 0)
				return DragDropAction.Link;
			return DragDropAction.None;
		}

		TransferDataType[] GetTransferTypes(Gdk.ContentFormats formats)
		{
			if (formats == null)
				return Array.Empty<TransferDataType>();
			var list = new List<TransferDataType>();
			var mimes = formats.GetMimeTypes(out _);
			if (mimes == null)
				return Array.Empty<TransferDataType>();
			foreach (var mime in mimes) {
				if (TryGetTransferType(mime, out var type))
					list.Add(type);
			}
			return list.ToArray();
		}

		TransferDataType[] FilterTransferTypes(TransferDataType[] dropTypes)
		{
			if (dropTypes == null || dropTypes.Length == 0)
				return Array.Empty<TransferDataType>();
			if (dragTargetTypes == null || dragTargetTypes.Length == 0)
				return dropTypes;
			var set = new HashSet<TransferDataType>(dragTargetTypes);
			return dropTypes.Where(set.Contains).ToArray();
		}

		bool TryGetTransferType(string mimeType, out TransferDataType type)
		{
			type = null;
			if (string.IsNullOrEmpty(mimeType))
				return false;
			if (mimeType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.Text;
				return true;
			}
			if (string.Equals(mimeType, "text/uri-list", StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.Uri;
				return true;
			}
			if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.Html;
				return true;
			}
			if (string.Equals(mimeType, "text/rtf", StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.Rtf;
				return true;
			}
			if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.Image;
				return true;
			}
			const string appPrefix = "application/";
			if (mimeType.StartsWith(appPrefix, StringComparison.OrdinalIgnoreCase)) {
				type = TransferDataType.FromId(mimeType.Substring(appPrefix.Length));
				return true;
			}
			return false;
		}

		string[] GetMimeTypes(TransferDataType[] types)
		{
			if (types == null || types.Length == 0)
				return Array.Empty<string>();
			var list = new List<string>();
			foreach (var type in types) {
				if (type == null)
					continue;
				if (type == TransferDataType.Text)
					list.Add("text/plain");
				else if (type == TransferDataType.Uri)
					list.Add("text/uri-list");
				else if (type == TransferDataType.Html)
					list.Add("text/html");
				else if (type == TransferDataType.Rtf)
					list.Add("text/rtf");
				else if (type == TransferDataType.Image) {
					list.Add("image/png");
					list.Add("image/jpeg");
				} else {
					list.Add("application/" + type.Id);
				}
			}
			return list.Distinct().ToArray();
		}

		Gdk.ContentProvider BuildContentProvider(TransferDataSource data, TransferDataType[] types)
		{
			if (data == null)
				return null;
			if (types == null || types.Length == 0)
				types = data.DataTypes;

			var providers = new List<Gdk.ContentProvider>();
			foreach (var type in types) {
				var provider = BuildContentProviderForType(data, type);
				if (provider != null)
					providers.Add(provider);
			}

			if (providers.Count == 0)
				return null;
			if (providers.Count == 1)
				return providers[0];

			var handles = providers.Select(p => p.Handle.DangerousGetHandle()).ToArray();
			var union = Gdk.Internal.ContentProvider.NewUnion(handles, (UIntPtr)handles.Length);
			return new Gdk.ContentProvider(new Gdk.Internal.ContentProviderHandle(union, true));
		}

		Gdk.ContentProvider BuildContentProviderForType(TransferDataSource data, TransferDataType type)
		{
			if (type == null)
				return null;
			var value = data.GetValue(type);
			if (value == null)
				return null;

			if (type == TransferDataType.Text) {
				var text = value as string ?? value.ToString();
				if (text == null)
					return null;
				var bytes = GLib.Bytes.New(Encoding.UTF8.GetBytes(text));
				return Gdk.ContentProvider.NewForBytes("text/plain", bytes);
			}

			if (type == TransferDataType.Uri) {
				var uris = value as System.Uri[] ?? (value is System.Uri uri ? new[] { uri } : null);
				if (uris == null || uris.Length == 0)
					return null;
				var text = string.Join("\r\n", uris.Select(u => u.AbsoluteUri));
				var bytes = GLib.Bytes.New(Encoding.UTF8.GetBytes(text));
				return Gdk.ContentProvider.NewForBytes("text/uri-list", bytes);
			}

			if (type == TransferDataType.Html || type == TransferDataType.Rtf) {
				var text = value as string ?? value.ToString();
				if (text == null)
					return null;
				var mime = type == TransferDataType.Html ? "text/html" : "text/rtf";
				var bytes = GLib.Bytes.New(Encoding.UTF8.GetBytes(text));
				return Gdk.ContentProvider.NewForBytes(mime, bytes);
			}

			if (type == TransferDataType.Image) {
				if (TryGetDragTexture(value, out var texture))
					return Gdk.ContentProvider.NewForValue(new GObject.Value(texture));
				if (value is Image image) {
					var desc = image.ToImageDescription(ApplicationContext);
					if (TryGetDragTexture(desc.Backend, out var imageTexture))
						return Gdk.ContentProvider.NewForValue(new GObject.Value(imageTexture));
				}
				return null;
			}

			if (value is byte[] dataBytes) {
				var bytes = GLib.Bytes.New(dataBytes);
				return Gdk.ContentProvider.NewForBytes("application/" + type.Id, bytes);
			}

			var serialized = TransferDataSource.SerializeValue(value);
			var customBytes = GLib.Bytes.New(serialized);
			return Gdk.ContentProvider.NewForBytes("application/" + type.Id, customBytes);
		}

		bool TryGetDragTexture(object imageBackend, out Gdk.Texture texture)
		{
			texture = null;
			if (imageBackend is Gdk.Texture existing) {
				texture = existing;
				return true;
			}
			if (imageBackend is GtkImage gtkImage && gtkImage.Pixbuf != null) {
				texture = Gdk.Texture.NewForPixbuf(gtkImage.Pixbuf);
				return true;
			}
			return false;
		}

		void BeginDropRead(Gdk.Drop drop, Point position, bool isMotion, DragDropAction action)
		{
			if (drop == null || dragTargetTypes.Length == 0)
				return;
			var mimeTypes = GetMimeTypes(dragTargetTypes);
			if (mimeTypes.Length == 0)
				return;

			var state = new DropReadState {
				Backend = this,
				Drop = drop,
				Position = position,
				IsMotion = isMotion,
				Action = action
			};
			state.Callback = state.HandleReady;
			state.MimeTypesHandle = GLib.Internal.Utf8StringArrayNullTerminatedOwnedHandle.Create(mimeTypes);
			var handle = System.Runtime.InteropServices.GCHandle.Alloc(state);
			Gdk.Internal.Drop.ReadAsync(drop.Handle.DangerousGetHandle(), state.MimeTypesHandle, 0, IntPtr.Zero, state.Callback, System.Runtime.InteropServices.GCHandle.ToIntPtr(handle));
		}

		void FinishDropRead(DropReadState state, Gio.AsyncResult result)
		{
			if (state.Drop == null || result == null)
				return;

			string mimeType;
			Gio.InputStream stream = null;
			try {
				stream = state.Drop.ReadFinish(result, out mimeType);
			} catch {
				return;
			}
			if (stream == null)
				return;
			using (stream) {
				var dataBytes = ReadAllBytes(stream);
				var dataStore = new TransferDataStore();
				AddDropData(dataStore, mimeType, dataBytes);

				if (state.IsMotion) {
					if ((enabledEvents & WidgetEvent.DragOver) == 0)
						return;
					var overArgs = new DragOverEventArgs(state.Position, dataStore, state.Action);
					ApplicationContext.InvokeUserCode(() => eventSink.OnDragOver(overArgs));
					var allowed = ResolveAllowedAction(overArgs.AllowedAction, state.Drop.Actions);
					state.Drop.Status(ConvertDragAction(allowed), ConvertDragAction(allowed));
					return;
				}

				if ((enabledEvents & WidgetEvent.DragDrop) == 0) {
					state.Drop.Finish(Gdk.DragAction.None);
					return;
				}

				var dropArgs = new DragEventArgs(state.Position, dataStore, state.Action);
				ApplicationContext.InvokeUserCode(() => eventSink.OnDragDrop(dropArgs));
				state.Drop.Finish(dropArgs.Success ? ConvertDragAction(state.Action) : Gdk.DragAction.None);
			}
		}

		void AddDropData(TransferDataStore target, string mimeType, byte[] dataBytes)
		{
			if (string.IsNullOrEmpty(mimeType) || dataBytes == null)
				return;

			if (mimeType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)) {
				target.AddText(Encoding.UTF8.GetString(dataBytes));
				return;
			}

			if (string.Equals(mimeType, "text/uri-list", StringComparison.OrdinalIgnoreCase)) {
				var text = Encoding.UTF8.GetString(dataBytes);
				var uris = ParseUris(text);
				if (uris.Length > 0)
					target.AddUris(uris);
				return;
			}

			if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase)) {
				target.AddValue(TransferDataType.Html, Encoding.UTF8.GetString(dataBytes));
				return;
			}

			if (string.Equals(mimeType, "text/rtf", StringComparison.OrdinalIgnoreCase)) {
				target.AddValue(TransferDataType.Rtf, Encoding.UTF8.GetString(dataBytes));
				return;
			}

			if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
				var image = LoadImageFromBytes(dataBytes);
				if (image != null)
					target.AddImage(image);
				return;
			}

			if (TryGetTransferType(mimeType, out var type)) {
				target.AddValue(type, dataBytes);
			}
		}

		Xwt.Drawing.Image LoadImageFromBytes(byte[] dataBytes)
		{
			try {
				using var stream = new MemoryStream(dataBytes);
				var handler = new GtkImageBackendHandler();
				return ApplicationContext.Toolkit.WrapImage(handler.LoadFromStream(stream));
			} catch {
				return null;
			}
		}

		System.Uri[] ParseUris(string text)
		{
			if (string.IsNullOrEmpty(text))
				return Array.Empty<System.Uri>();
			var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var list = new List<System.Uri>();
			foreach (var line in lines) {
				var trimmed = line.Trim();
				if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
					continue;
				if (System.Uri.TryCreate(trimmed, System.UriKind.Absolute, out var uri))
					list.Add(uri);
			}
			return list.ToArray();
		}

		byte[] ReadAllBytes(Gio.InputStream stream)
		{
			using var ms = new MemoryStream();
			while (true) {
				GLib.Bytes bytes;
				try {
					bytes = stream.ReadBytes(8192, null);
				} catch {
					break;
				}
				if (bytes == null)
					break;
				var size = bytes.GetSize();
				if (size == 0) {
					bytes.Unref();
					break;
				}
				var span = bytes.GetRegionSpan<byte>(UIntPtr.Zero, size);
				ms.Write(span);
				bytes.Unref();
			}
			return ms.ToArray();
		}

		Gdk.DragAction ConvertDragAction(DragDropAction dragAction)
		{
			Gdk.DragAction action = 0;
			if ((dragAction & DragDropAction.Copy) != 0)
				action |= Gdk.DragAction.Copy;
			if ((dragAction & DragDropAction.Move) != 0)
				action |= Gdk.DragAction.Move;
			if ((dragAction & DragDropAction.Link) != 0)
				action |= Gdk.DragAction.Link;
			return action;
		}

		DragDropAction ConvertDragAction(Gdk.DragAction dragAction)
		{
			DragDropAction action = 0;
			if ((dragAction & Gdk.DragAction.Copy) != 0)
				action |= DragDropAction.Copy;
			if ((dragAction & Gdk.DragAction.Move) != 0)
				action |= DragDropAction.Move;
			if ((dragAction & Gdk.DragAction.Link) != 0)
				action |= DragDropAction.Link;
			return action;
		}

		sealed class DropReadState
		{
			public WidgetBackend Backend;
			public Gdk.Drop Drop;
			public Point Position;
			public bool IsMotion;
			public DragDropAction Action;
			public Gio.Internal.AsyncReadyCallback Callback;
			public GLib.Internal.Utf8StringArrayNullTerminatedOwnedHandle MimeTypesHandle;

			public void HandleReady(IntPtr sourceObject, IntPtr res, IntPtr data)
			{
				var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(data);
				try {
					var asyncResult = new Gio.AsyncResultHelper(new GObject.Internal.ObjectHandle((IntPtr)res, false));
					Backend?.FinishDropRead(this, asyncResult);
				} finally {
					MimeTypesHandle.Dispose();
					handle.Free();
				}
			}
		}

		void ApplyFont()
		{
			if (widget == null)
				return;

			if (customFont == null) {
				if (!string.IsNullOrEmpty(fontCssClass) && widget.HasCssClass(fontCssClass))
					widget.RemoveCssClass(fontCssClass);
				return;
			}

			if (fontProvider == null) {
				fontProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, fontProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(fontCssClass))
				fontCssClass = "xwt-font-" + Interlocked.Increment(ref fontClassId).ToString();
			if (!widget.HasCssClass(fontCssClass))
				widget.AddCssClass(fontCssClass);

			var family = customFont.GetFamily() ?? "Sans";
			var sizePx = customFont.GetSize() / (double)Pango.Constants.SCALE;
			var weight = customFont.GetWeight();
			var style = customFont.GetStyle();
			string styleCss = style == Pango.Style.Italic ? "italic" : style == Pango.Style.Oblique ? "oblique" : "normal";
			string css = "." + fontCssClass + " { font-family: " + family + "; font-size: " + sizePx.ToString("0.###") + "px; font-style: " + styleCss + "; font-weight: " + (int)weight + "; }";
			fontProvider.LoadFromString(css);
		}

		void ApplyBackgroundColor()
		{
			if (widget == null)
				return;

			if (!customBackgroundColor.HasValue) {
				if (!string.IsNullOrEmpty(backgroundCssClass) && widget.HasCssClass(backgroundCssClass))
					widget.RemoveCssClass(backgroundCssClass);
				return;
			}

			if (backgroundProvider == null) {
				backgroundProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, backgroundProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(backgroundCssClass))
				backgroundCssClass = "xwt-bg-" + Interlocked.Increment(ref backgroundClassId).ToString();
			if (!widget.HasCssClass(backgroundCssClass))
				widget.AddCssClass(backgroundCssClass);

			var color = customBackgroundColor.Value;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + backgroundCssClass + " { background-color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			backgroundProvider.LoadFromString(css);
		}
	}
}
