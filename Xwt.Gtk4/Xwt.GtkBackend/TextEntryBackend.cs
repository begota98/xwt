using System;
using Gtk.Internal;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class TextEntryBackend : WidgetBackend, ITextEntryBackend
	{
		readonly bool ownsEntry;
		Gtk.Entry entry;
		ITextEntryEventSink eventSink;
		bool selectionChangedEnabled;
		bool multiline;

		public TextEntryBackend()
		{
			entry = Gtk.Entry.New();
			Widget = entry;
			Widget.Show();
			ownsEntry = true;
		}

		protected TextEntryBackend(Gtk.Entry entry)
		{
			this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
			Widget = entry;
			Widget.Show();
			ownsEntry = false;
		}

		protected Gtk.Entry Entry => entry;

		public override void Dispose()
		{
			if (ownsEntry)
				base.Dispose();
			else
				Widget = null;
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
			get { return entry.HasFrame; }
			set { entry.HasFrame = value; }
		}

		public bool MultiLine {
			get { return multiline; }
			set {
				multiline = value;
				entry.TruncateMultiline = !value;
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
				entry.GrabFocusWithoutSelecting();
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
				entry.GrabFocusWithoutSelecting();
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

		public bool HasCompletions => entry?.Completion?.Model != null;

		public void SetCompletions(string[] completions)
		{
			var widgetCompletion = entry.Completion;

			if (completions == null || completions.Length == 0) {
				if (widgetCompletion != null)
					widgetCompletion.Model = null;
				return;
			}

			if (widgetCompletion == null)
				entry.Completion = widgetCompletion = CreateCompletion();

			var model = CreateCompletionModel();
			foreach (var c in completions) {
				model.Append(out Gtk.TreeIter iter);
				using var value = new GObject.Value(c);
				model.SetValue(iter, 0, value);
			}
			widgetCompletion.Model = model;
		}

		public void SetCompletionMatchFunc(Func<string, string, bool> matchFunc)
		{
			if (matchFunc == null)
				return;
			var widgetCompletion = entry.Completion;
			if (widgetCompletion == null)
				entry.Completion = widgetCompletion = CreateCompletion();
			widgetCompletion.SetMatchFunc((Gtk.EntryCompletion completion, string key, Gtk.TreeIter iter) => {
				completion.Model.GetValue(iter, 0, out GObject.Value value);
				try {
					var completionText = value.GetString();
					return matchFunc(key, completionText);
				} finally {
					value.Dispose();
				}
			});
		}

		static Gtk.EntryCompletion CreateCompletion()
		{
			var completion = Gtk.EntryCompletion.New();
			completion.PopupCompletion = true;
			completion.InlineCompletion = true;
			completion.InlineSelection = true;
			completion.TextColumn = 0;
			return completion;
		}

		static Gtk.ListStore CreateCompletionModel()
		{
			var types = new UIntPtr[] { GObject.Type.String.Value };
			var handle = new ListStoreHandle(Gtk.Internal.ListStore.New(1, types), true);
			return new Gtk.ListStore(handle);
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

		void HandleChanged(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(() => {
				eventSink.OnChanged();
				if (selectionChangedEnabled)
					eventSink.OnSelectionChanged();
			});
		}

		void HandleActivated(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnActivated);
		}

		void HandleNotify(object sender, GObject.Object.NotifySignalArgs args)
		{
			if (!selectionChangedEnabled || args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "cursor-position" && name != "selection-bound")
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}

		void NotifySelectionChanged()
		{
			if (!selectionChangedEnabled || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}
	}
}
