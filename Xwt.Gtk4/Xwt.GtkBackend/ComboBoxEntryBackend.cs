using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ComboBoxEntryBackend : WidgetBackend, IComboBoxEntryBackend
	{
		Gtk.ComboBoxText combo;
		TextEntryBackend entryBackend;
		Gtk.Entry entry;
		IListDataSource source;
		IComboBoxEventSink eventSink;
		int textColumn;
		bool completes;

		public ComboBoxEntryBackend()
		{
			combo = Gtk.ComboBoxText.NewWithEntry();
			Widget = combo;
			Widget.Show();
			if (combo.Child is Gtk.Entry entry) {
				this.entry = entry;
				entryBackend = new CustomComboEntryBackend(entry);
			}
		}

		public void SetViews(CellViewCollection views)
		{
			textColumn = 0;
			if (views != null) {
				foreach (var view in views) {
					if (view is TextCellView textView) {
						if (textView.TextField != null)
							textColumn = textView.TextField.Index;
						else if (textView.MarkupField != null)
							textColumn = textView.MarkupField.Index;
						break;
					}
				}
			}
			ReloadItems();
		}

		public void SetSource(IListDataSource source, IBackend sourceBackend)
		{
			DetachSource();
			this.source = source;
			ReloadItems();
			AttachSource();
		}

		public int SelectedRow {
			get { return combo.Active; }
			set { combo.Active = value; }
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (IComboBoxEventSink)sink;
			combo.SetRowSeparatorFunc(IsRowSeparator);
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is ComboBoxEvent ev && ev == ComboBoxEvent.SelectionChanged)
				combo.OnChanged += HandleChanged;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is ComboBoxEvent ev && ev == ComboBoxEvent.SelectionChanged)
				combo.OnChanged -= HandleChanged;
		}

		void HandleChanged(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}

		public void SetTextColumn(int column)
		{
			textColumn = column;
			combo.EntryTextColumn = column;
			ReloadItems();
			if (Completes)
				BindCompletion();
		}

		public ITextEntryBackend TextEntryBackend => entryBackend;

		public bool Completes {
			get {
				if (combo?.Model == null)
					return completes;
				return entry?.Completion?.Model == combo.Model;
			}
			set {
				completes = value;
				var combo = (Gtk.ComboBoxText)Widget;
				if (!completes) {
					if (entry?.Completion != null && combo?.Model != null && entry.Completion.Model == combo.Model)
						entry.Completion.Model = null;
					return;
				}
				BindCompletion();
			}
		}

		void BindCompletion()
		{
			if (entry == null)
				return;
			if (combo?.Model == null)
				return;
			var completion = entry.Completion;
			if (completion == null) {
				completion = Gtk.EntryCompletion.New();
				completion.PopupCompletion = true;
				completion.InlineCompletion = true;
				completion.InlineSelection = true;
				entry.Completion = completion;
			}
			completion.Model = combo.Model;
			if (completion.TextColumn != textColumn)
				completion.TextColumn = textColumn;
		}

		void ReloadItems()
		{
			combo.RemoveAll();
			if (source == null)
				return;

			for (int i = 0; i < source.RowCount; i++) {
				var text = source.GetValue(i, textColumn)?.ToString() ?? string.Empty;
				combo.AppendText(text);
			}
			if (completes)
				BindCompletion();
		}

		bool IsRowSeparator(Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			if (eventSink == null)
				return false;
			var path = model.GetPath(iter);
			if (path == null)
				return false;
			int depth = path.GetDepth();
			if (depth <= 0)
				return false;
			var indicesPtr = path.GetIndices();
			if (indicesPtr == IntPtr.Zero)
				return false;
			int index = System.Runtime.InteropServices.Marshal.ReadInt32(indicesPtr);
			bool result = false;
			ApplicationContext.InvokeUserCode(() => {
				result = eventSink.RowIsSeparator(index);
			});
			return result;
		}

		void AttachSource()
		{
			if (source == null)
				return;
			source.RowInserted += HandleRowChanged;
			source.RowDeleted += HandleRowChanged;
			source.RowChanged += HandleRowChanged;
			source.RowsReordered += HandleRowsReordered;
		}

		void DetachSource()
		{
			if (source == null)
				return;
			source.RowInserted -= HandleRowChanged;
			source.RowDeleted -= HandleRowChanged;
			source.RowChanged -= HandleRowChanged;
			source.RowsReordered -= HandleRowsReordered;
		}

		void HandleRowChanged(object sender, ListRowEventArgs e)
		{
			ReloadItems();
		}

		void HandleRowsReordered(object sender, ListRowOrderEventArgs e)
		{
			ReloadItems();
		}

		class CustomComboEntryBackend : TextEntryBackend
		{
			public CustomComboEntryBackend(Gtk.Entry entry) : base(entry)
			{
			}
		}
	}
}
