using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SearchTextEntryBackend : WidgetBackend, ISearchTextEntryBackend
	{
		Gtk.SearchEntry entry;
		ITextEntryEventSink eventSink;
		bool selectionChangedEnabled;
		bool multiline;
		bool showFrame = true;
		Gtk.CssProvider frameProvider;
		string frameCssClass;
		static int frameClassId;

		public SearchTextEntryBackend()
		{
			entry = Gtk.SearchEntry.New();
			Widget = entry;
			Widget.Show();
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ITextEntryEventSink)sink;
		}

		public string Text {
			get { return entry.Text_ ?? string.Empty; }
			set { entry.Text_ = value ?? string.Empty; }
		}

		public Alignment TextAlignment {
			get {
				if (entry.Xalign <= 0)
					return Alignment.Start;
				if (entry.Xalign >= 1)
					return Alignment.End;
				return Alignment.Center;
			}
			set {
				switch (value) {
					case Alignment.Start:
						entry.Xalign = 0;
						break;
					case Alignment.End:
						entry.Xalign = 1;
						break;
					default:
						entry.Xalign = 0.5f;
						break;
				}
			}
		}

		public string PlaceholderText {
			get { return entry.PlaceholderText ?? string.Empty; }
			set { entry.PlaceholderText = value ?? string.Empty; }
		}

		public bool ReadOnly {
			get { return !entry.Editable; }
			set { entry.Editable = !value; }
		}

		public bool ShowFrame {
			get { return showFrame; }
			set {
				showFrame = value;
				ApplyFrameStyle();
			}
		}

		public bool MultiLine {
			get { return multiline; }
			set {
				multiline = value;
			}

		}

		public int CursorPosition {
			get { return entry.CursorPosition; }
			set { entry.SetPosition(value); }
		}

		public int SelectionStart {
			get {
				int start, end;
				if (!entry.GetSelectionBounds(out start, out end))
					return entry.CursorPosition;
				return start;
			}
			set {
				int oldStart = SelectionStart;
				int oldLength = SelectionLength;
				int length = SelectionLength;
				entry.GrabFocus();
				if (string.IsNullOrEmpty(Text))
					return;
				entry.SelectRegion(value, value + length);
				if (oldStart != value || oldLength != SelectionLength)
					NotifySelectionChanged();
			}
		}

		public int SelectionLength {
			get {
				int start, end;
				if (!entry.GetSelectionBounds(out start, out end))
					return 0;
				return end - start;
			}
			set {
				int oldStart = SelectionStart;
				int oldLength = SelectionLength;
				int start = SelectionStart;
				entry.GrabFocus();
				if (string.IsNullOrEmpty(Text))
					return;
				entry.SelectRegion(start, start + value);
				if (oldStart != start || oldLength != value)
					NotifySelectionChanged();
			}
		}

		public string SelectedText {
			get {
				int start = SelectionStart;
				int length = SelectionLength;
				if (length <= 0)
					return string.Empty;
				try {
					return Text.Substring(start, length);
				} catch {
					return string.Empty;
				}
			}
			set {
				int oldStart = SelectionStart;
				int oldLength = SelectionLength;
				int start = SelectionStart;
				int end = start + SelectionLength;
				if (end > start)
					entry.DeleteText(start, end);
				if (!string.IsNullOrEmpty(value)) {
					int pos = start;
					entry.InsertText(value, value.Length, ref pos);
					entry.SelectRegion(start, pos);
				}
				if (oldStart != SelectionStart || oldLength != SelectionLength)
					NotifySelectionChanged();
			}
		}

		public bool HasCompletions => false;

		public void SetCompletions(string[] completions)
		{
		}

		public void SetCompletionMatchFunc(Func<string, string, bool> matchFunc)
		{
		}

		public override void EnableEvent(object eventId)
		{
			if (!(eventId is TextEntryEvent ev))
				return;
			switch (ev) {
				case TextEntryEvent.Changed:
					entry.OnChanged += HandleChanged;
					break;
				case TextEntryEvent.Activated:
					entry.OnActivate += HandleActivated;
					break;
				case TextEntryEvent.SelectionChanged:
					selectionChangedEnabled = true;
					entry.OnNotify += HandleNotify;
					break;
			}
		}

		public override void DisableEvent(object eventId)
		{
			if (!(eventId is TextEntryEvent ev))
				return;
			switch (ev) {
				case TextEntryEvent.Changed:
					entry.OnChanged -= HandleChanged;
					break;
				case TextEntryEvent.Activated:
					entry.OnActivate -= HandleActivated;
					break;
				case TextEntryEvent.SelectionChanged:
					selectionChangedEnabled = false;
					entry.OnNotify -= HandleNotify;
					break;
			}
		}

		void HandleChanged(Gtk.Editable sender, EventArgs e)
		{
			if (eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(() => {
				eventSink.OnChanged();
				if (selectionChangedEnabled)
					eventSink.OnSelectionChanged();
			});
		}

		void HandleActivated(Gtk.Editable sender, EventArgs e)
		{
			if (eventSink != null)
				ApplicationContext.InvokeUserCode(eventSink.OnActivated);
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (!selectionChangedEnabled || args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name == "cursor-position" || name == "selection-bound")
				ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}

		void NotifySelectionChanged()
		{
			if (!selectionChangedEnabled || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}

		void ApplyFrameStyle()
		{
			if (showFrame) {
				if (!string.IsNullOrEmpty(frameCssClass) && entry.HasCssClass(frameCssClass))
					entry.RemoveCssClass(frameCssClass);
				return;
			}

			if (frameProvider == null) {
				frameProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, frameProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(frameCssClass))
				frameCssClass = "xwt-searchentry-flat-" + System.Threading.Interlocked.Increment(ref frameClassId).ToString();
			if (!entry.HasCssClass(frameCssClass))
				entry.AddCssClass(frameCssClass);

			var css = "." + frameCssClass + " { border: 0; box-shadow: none; }";
			frameProvider.LoadFromString(css);
		}
	}
}
