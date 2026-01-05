using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class ListViewBackend : TableViewBackend, IListViewBackend
	{
		IListDataSource source;
		Gio.ListStore listStore;
		Gtk.SortListModel sortModel;
		Gtk.CustomSorter customSorter;
		ListViewColumn sortColumn;
		ColumnSortDirection sortDirection;
		readonly List<IntPtr> rowHandles = new List<IntPtr>();
		bool borderVisible = true;
		bool headersVisible = true;
		GridLines gridLines;
		bool columnWidthUpdateQueued;

		public void SetSource(IListDataSource source, IBackend sourceBackend)
		{
			DetachSource();
			this.source = source;
			BuildModel();
			AttachSource();
			QueueColumnWidthUpdate();
		}

		public void SelectRow(int pos)
		{
			SelectRowIndex(pos, true);
		}

		public void UnselectRow(int pos)
		{
			UnselectRowIndex(pos);
		}

		public void ScrollToRow(int row)
		{
			ScrollToIndex(row);
		}

		public void StartEditingCell(int row, CellView cell)
		{
			if (!TryGetCellBinding(ctx => ctx.RowIndex == row, cell, out _, out var cellBinding))
				return;
			if (cellBinding?.Widget is Gtk.EditableLabel editable)
				editable.StartEditing();
		}

		public int[] SelectedRows => GetSelectedIndexes();

		public int FocusedRow {
			get {
				var selected = GetSelectedIndexes();
				return selected.Length > 0 ? selected[0] : -1;
			}
			set {
				SelectRowIndex(value, true);
			}
		}

		public int CurrentEventRow { get; private set; } = -1;

		public bool BorderVisible {
			get { return borderVisible; }
			set {
				borderVisible = value;
				ScrolledWindow.HasFrame = borderVisible;
			}
		}

		public GridLines GridLinesVisible {
			get { return gridLines; }
			set {
				gridLines = value;
				ColumnView.ShowRowSeparators = value == GridLines.Horizontal || value == GridLines.Both;
				ColumnView.ShowColumnSeparators = value == GridLines.Vertical || value == GridLines.Both;
			}
		}

		public bool HeadersVisible {
			get { return headersVisible; }
			set {
				headersVisible = value;
				UpdateHeaderVisibility();
			}
		}

		protected override bool HeadersVisibleInternal => headersVisible;

		public int GetRowAtPosition(Point p)
		{
			foreach (var info in ColumnInfos) {
				if (!info.IsFirstColumn)
					continue;
				foreach (var binding in info.Bindings.Values) {
					var context = binding.Context;
					if (context == null)
						continue;
					var bounds = GetRowBounds(context.RowIndex, true);
					if (bounds.Contains(p))
						return context.RowIndex;
				}
			}
			return -1;
		}

		public Rectangle GetCellBounds(int row, CellView cell, bool includeMargin)
		{
			if (TryGetCellBinding(ctx => ctx.RowIndex == row, cell, out _, out var cellBinding))
				return GetWidgetBounds(cellBinding.Widget);
			return Rectangle.Zero;
		}

		public Rectangle GetRowBounds(int row, bool includeMargin)
		{
			if (TryGetRowBinding(ctx => ctx.RowIndex == row, true, out var binding)) {
				var bounds = GetWidgetBounds(binding.Container);
				if (bounds.IsEmpty)
					return Rectangle.Zero;
				bounds.Width = ColumnView.GetAllocatedWidth();
				return bounds;
			}
			return Rectangle.Zero;
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is ListViewEvent ev && ev == ListViewEvent.RowActivated)
				ColumnView.OnActivate += HandleRowActivated;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is ListViewEvent ev && ev == ListViewEvent.RowActivated)
				ColumnView.OnActivate -= HandleRowActivated;
		}

		void HandleRowActivated(Gtk.ColumnView sender, Gtk.ColumnView.ActivateSignalArgs args)
		{
			CurrentEventRow = (int)args.Position;
			ApplicationContext.InvokeUserCode(() => ((IListViewEventSink)EventSink).OnRowActivated(CurrentEventRow));
		}

		protected override RowContext CreateRowContext(Gtk.ListItem listItem)
		{
			var row = (int)listItem.Position;
			return new RowContext {
				RowIndex = row,
				ListSource = source,
				CellDataSource = source != null ? new ListCellDataSource(source, row) : null
			};
		}

		void BuildModel()
		{
			listStore = Gio.ListStore.New(GObject.Object.GetGType());
			rowHandles.Clear();
			if (source != null) {
				for (int i = 0; i < source.RowCount; i++) {
					var obj = CreateListObject();
					listStore.Append(obj);
					rowHandles.Add(obj.Handle.DangerousGetHandle());
				}
			}
			EnsureSortModel();
			UpdateSortModel();
			Gio.ListModel model = sortModel != null ? (Gio.ListModel)sortModel : listStore;
			SetListModel(model);
		}

		void AttachSource()
		{
			if (source == null)
				return;
			source.RowInserted += HandleRowInserted;
			source.RowDeleted += HandleRowDeleted;
			source.RowChanged += HandleRowChanged;
			source.RowsReordered += HandleRowsReordered;
		}

		void DetachSource()
		{
			if (source == null)
				return;
			source.RowInserted -= HandleRowInserted;
			source.RowDeleted -= HandleRowDeleted;
			source.RowChanged -= HandleRowChanged;
			source.RowsReordered -= HandleRowsReordered;
		}

		void HandleRowInserted(object sender, ListRowEventArgs e)
		{
			if (listStore == null)
				return;
			var obj = CreateListObject();
			listStore.Insert((uint)e.Row, obj);
			if (e.Row >= 0 && e.Row <= rowHandles.Count)
				rowHandles.Insert(e.Row, obj.Handle.DangerousGetHandle());
			else
				rowHandles.Add(obj.Handle.DangerousGetHandle());
			QueueColumnWidthUpdate();
		}

		void HandleRowDeleted(object sender, ListRowEventArgs e)
		{
			if (listStore == null)
				return;
			listStore.Remove((uint)e.Row);
			if (e.Row >= 0 && e.Row < rowHandles.Count)
				rowHandles.RemoveAt(e.Row);
			QueueColumnWidthUpdate();
		}

		void HandleRowChanged(object sender, ListRowEventArgs e)
		{
			listStore?.ItemsChanged((uint)e.Row, 1, 1);
			QueueColumnWidthUpdate();
		}

		void HandleRowsReordered(object sender, ListRowOrderEventArgs e)
		{
			BuildModel();
			QueueColumnWidthUpdate();
		}

		static GObject.Object CreateListObject()
		{
			return (GObject.Object)GObject.Object.Newv(GObject.Object.GetGType(), Array.Empty<GObject.Parameter>());
		}

		protected override void SetCurrentEventRow(RowContext context)
		{
			CurrentEventRow = context != null ? context.RowIndex : -1;
		}

		protected override void OnColumnsChanged()
		{
			UpdateSortModel();
			QueueColumnWidthUpdate();
		}

		void QueueColumnWidthUpdate()
		{
			if (columnWidthUpdateQueued)
				return;
			columnWidthUpdateQueued = true;
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				columnWidthUpdateQueued = false;
				UpdateColumnWidths();
				return false;
			}));
		}

		void UpdateColumnWidths()
		{
			if (source == null)
				return;

			const int padding = 16;
			int sampleRows = Math.Min(source.RowCount, 200);

			foreach (var info in ColumnInfos) {
				int maxWidth = MeasureText(info.Column?.Title ?? string.Empty);
				for (int row = 0; row < sampleRows; row++) {
					int rowWidth = MeasureRowWidth(info, row);
					if (rowWidth > maxWidth)
						maxWidth = rowWidth;
				}
				if (maxWidth > 0) {
					int targetWidth = maxWidth + padding;
					if (info.Column != null && info.Column.CanResize)
						info.GtkColumn.FixedWidth = -1;
					else
						info.GtkColumn.FixedWidth = targetWidth;
				}
			}
		}

		int MeasureRowWidth(ColumnInfo info, int row)
		{
			int width = 0;
			foreach (var view in info.Views) {
				width += MeasureCellWidth(view, row);
			}
			return width;
		}

		int MeasureCellWidth(CellView view, int row)
		{
			if (view is ITextCellViewFrontend textView) {
				string text = textView.Text ?? string.Empty;
				if (!string.IsNullOrEmpty(textView.Markup))
					text = FormattedText.FromMarkup(textView.Markup).Text;
				if (textView.TextField != null)
					text = source.GetValue(row, textView.TextField.Index)?.ToString() ?? string.Empty;
				return MeasureText(text);
			}

			if (view is ICheckBoxCellViewFrontend || view is IRadioButtonCellViewFrontend)
				return 24;

			if (view is ImageCellView imageView) {
				Image image = imageView.Image;
				if (imageView.ImageField != null)
					image = source.GetValue(row, imageView.ImageField.Index) as Image;
				if (image != null)
					return (int)Math.Ceiling(image.Size.Width);
				return 24;
			}

			if (view is IImageCellViewFrontend)
				return 24;

			return 0;
		}

		int MeasureText(string text)
		{
			if (string.IsNullOrEmpty(text))
				return 0;
			using var layout = ColumnView.CreatePangoLayout(text);
			layout.GetPixelSize(out int width, out _);
			return width;
		}

		void EnsureSortModel()
		{
			if (sortModel != null)
				return;
			if (customSorter == null) {
				GLib.CompareDataFunc compare = (a, b) => CompareItems(a, b);
				customSorter = Gtk.CustomSorter.New(compare);
			}
			sortModel = Gtk.SortListModel.New(listStore, customSorter);
		}

		void UpdateSortModel()
		{
			var info = GetSortColumnInfo();
			sortColumn = info?.Column;
			sortDirection = sortColumn?.SortDirection ?? ColumnSortDirection.Ascending;

			if (customSorter != null)
				customSorter.SetSortFunc((a, b) => CompareItems(a, b));

			if (sortModel != null)
				sortModel.SetSorter(sortColumn != null ? customSorter : null);
		}

		ColumnInfo GetSortColumnInfo()
		{
			foreach (var info in ColumnInfos) {
				if (info.Column != null && info.Column.SortIndicatorVisible && info.Column.SortDataField != null)
					return info;
			}
			return null;
		}

		int CompareItems(object a, object b)
		{
			if (sortColumn == null || source == null || sortColumn.SortDataField == null)
				return 0;
			var rowA = GetRowIndex(GetItemHandle(a));
			var rowB = GetRowIndex(GetItemHandle(b));
			if (rowA < 0 || rowB < 0)
				return 0;
			var field = sortColumn.SortDataField;
			var valueA = source.GetValue(rowA, field.Index);
			var valueB = source.GetValue(rowB, field.Index);
			int result = CompareValues(valueA, valueB);
			if (sortDirection == ColumnSortDirection.Descending)
				result = -result;
			return result;
		}

		int GetRowIndex(IntPtr item)
		{
			if (item == IntPtr.Zero)
				return -1;
			return rowHandles.IndexOf(item);
		}

		static IntPtr GetItemHandle(object item)
		{
			if (item == null)
				return IntPtr.Zero;
			if (item is IntPtr ptr)
				return ptr;
			if (item is GObject.Internal.ObjectHandle handle)
				return handle.DangerousGetHandle();
			if (item is GObject.Object obj)
				return obj.Handle.DangerousGetHandle();
			return IntPtr.Zero;
		}

		static int CompareValues(object a, object b)
		{
			if (ReferenceEquals(a, b))
				return 0;
			if (a == null)
				return -1;
			if (b == null)
				return 1;
			if (a is IComparable comparable)
				return comparable.CompareTo(b);
			if (b is IComparable comparableB)
				return -comparableB.CompareTo(a);
			return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
		}

		class ListCellDataSource : ICellDataSource
		{
			readonly IListDataSource source;
			readonly int row;

			public ListCellDataSource(IListDataSource source, int row)
			{
				this.source = source;
				this.row = row;
			}

			public object GetValue(IDataField field)
			{
				return source.GetValue(row, field.Index);
			}
		}
	}
}
