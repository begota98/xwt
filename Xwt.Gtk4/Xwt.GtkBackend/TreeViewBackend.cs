using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class TreeViewBackend : TableViewBackend, ITreeViewBackend
	{
		ITreeDataSource source;
		Gio.ListStore rootStore;
		Gtk.TreeListModel treeModel;
		readonly Dictionary<GObject.Object, TreePosition> objectToPosition = new Dictionary<GObject.Object, TreePosition>();
		bool borderVisible = true;
		bool headersVisible = true;
		GridLines gridLines;
		bool rowExpandedEnabled;
		bool rowCollapsedEnabled;
		bool rowExpandingEnabled;
		bool rowCollapsingEnabled;
		IDataField sortField;
		ColumnSortDirection sortDirection = ColumnSortDirection.Ascending;

		public TreeViewBackend()
		{
		}

		public void SetSource(ITreeDataSource source, IBackend sourceBackend)
		{
			DetachSource();
			this.source = source;
			UpdateSortSettings();
			RebuildModel();
			AttachSource();
		}

		public TreePosition[] SelectedRows {
			get {
				var indexes = GetSelectedIndexes();
				var rows = new List<TreePosition>();
				for (int i = 0; i < indexes.Length; i++) {
					var pos = GetTreePositionFromRow(indexes[i]);
					if (pos != null)
						rows.Add(pos);
				}
				return rows.ToArray();
			}
		}

		public TreePosition FocusedRow {
			get {
				var indexes = GetSelectedIndexes();
				return indexes.Length > 0 ? GetTreePositionFromRow(indexes[0]) : null;
			}
			set {
				var index = FindRowIndex(value);
				if (index >= 0)
					SelectRowIndex(index, true);
			}
		}

		public TreePosition CurrentEventRow { get; private set; }

		public void SelectRow(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index >= 0)
				SelectRowIndex(index, true);
		}

		public void UnselectRow(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index >= 0)
				UnselectRowIndex(index);
		}

		public bool IsRowSelected(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index < 0)
				return false;
			return Array.IndexOf(GetSelectedIndexes(), index) >= 0;
		}

		public bool IsRowExpanded(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index < 0)
				return false;
			var row = treeModel.GetRow((uint)index);
			return row != null && row.Expanded;
		}

		public void ExpandRow(TreePosition pos, bool expandChildren)
		{
			var index = FindRowIndex(pos);
			if (index < 0)
				return;
			var row = treeModel.GetRow((uint)index);
			if (row != null) {
				row.SetExpanded(true);
				if (expandChildren)
					ExpandDescendants(row);
			}
		}

		public void CollapseRow(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index < 0)
				return;
			var row = treeModel.GetRow((uint)index);
			if (row != null)
				row.SetExpanded(false);
		}

		public void ScrollToRow(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index >= 0)
				ScrollToIndex(index);
		}

		public void ExpandToRow(TreePosition pos)
		{
			var index = FindRowIndex(pos);
			if (index < 0)
				return;
			var row = treeModel.GetRow((uint)index);
			while (row != null) {
				row.SetExpanded(true);
				row = row.GetParent();
			}
		}

		public bool BorderVisible {
			get { return borderVisible; }
			set {
				borderVisible = value;
				ScrolledWindow.HasFrame = borderVisible;
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

		public GridLines GridLinesVisible {
			get { return gridLines; }
			set {
				gridLines = value;
				ColumnView.ShowRowSeparators = value == GridLines.Horizontal || value == GridLines.Both;
				ColumnView.ShowColumnSeparators = value == GridLines.Vertical || value == GridLines.Both;
			}
		}

		public bool UseAlternatingRowColors {
			get { return GetAlternatingRowColors(); }
			set { SetAlternatingRowColors(value); }
		}

		public bool AnimationsEnabled { get; set; }

		public bool GetDropTargetRow(double x, double y, out RowDropPosition pos, out TreePosition nodePosition)
		{
			var point = new Point(x, y);
			var row = GetRowAtPosition(point);
			if (row == null) {
				pos = RowDropPosition.Into;
				nodePosition = null;
				return false;
			}

			var bounds = GetRowBounds(row, true);
			if (bounds.IsEmpty) {
				pos = RowDropPosition.Into;
				nodePosition = null;
				return false;
			}

			var yRel = y - bounds.Y;
			var third = bounds.Height / 3;
			if (yRel < third)
				pos = RowDropPosition.Before;
			else if (yRel > 2 * third)
				pos = RowDropPosition.After;
			else
				pos = RowDropPosition.Into;

			nodePosition = row;
			return true;
		}

		public TreePosition GetRowAtPosition(Point p)
		{
			foreach (var info in ColumnInfos) {
				if (!info.IsFirstColumn)
					continue;
				foreach (var binding in info.Bindings.Values) {
					var context = binding.Context;
					if (context == null)
						continue;
					var bounds = GetWidgetBounds(binding.Container);
					if (!bounds.IsEmpty && bounds.Contains(p))
						return context.TreePosition;
				}
			}
			return null;
		}

		public Rectangle GetCellBounds(TreePosition pos, CellView cell, bool includeMargin)
		{
			if (TryGetCellBinding(ctx => ctx.TreePosition != null && ctx.TreePosition.Equals(pos), cell, out _, out var cellBinding))
				return GetWidgetBounds(cellBinding.Widget);
			return Rectangle.Zero;
		}

		public Rectangle GetRowBounds(TreePosition pos, bool includeMargin)
		{
			if (TryGetRowBinding(ctx => ctx.TreePosition != null && ctx.TreePosition.Equals(pos), true, out var binding)) {
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
			if (eventId is TreeViewEvent ev) {
				switch (ev) {
					case TreeViewEvent.RowActivated:
						ColumnView.OnActivate += HandleRowActivated;
						break;
					case TreeViewEvent.RowExpanded:
						rowExpandedEnabled = true;
						break;
					case TreeViewEvent.RowCollapsed:
						rowCollapsedEnabled = true;
						break;
					case TreeViewEvent.RowExpanding:
						rowExpandingEnabled = true;
						break;
					case TreeViewEvent.RowCollapsing:
						rowCollapsingEnabled = true;
						break;
				}
			}
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is TreeViewEvent ev) {
				switch (ev) {
					case TreeViewEvent.RowActivated:
						ColumnView.OnActivate -= HandleRowActivated;
						break;
					case TreeViewEvent.RowExpanded:
						rowExpandedEnabled = false;
						break;
					case TreeViewEvent.RowCollapsed:
						rowCollapsedEnabled = false;
						break;
					case TreeViewEvent.RowExpanding:
						rowExpandingEnabled = false;
						break;
					case TreeViewEvent.RowCollapsing:
						rowCollapsingEnabled = false;
						break;
				}
			}
		}

		void HandleRowActivated(Gtk.ColumnView sender, Gtk.ColumnView.ActivateSignalArgs args)
		{
			var pos = GetTreePositionFromRow((int)args.Position);
			if (pos == null)
				return;
			CurrentEventRow = pos;
			ApplicationContext.InvokeUserCode(() => ((ITreeViewEventSink)EventSink).OnRowActivated(pos));
		}

		protected override Gtk.Widget CreateColumnContainer(ColumnInfo info, Gtk.ListItem listItem)
		{
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			if (info.IsFirstColumn) {
				var expander = Gtk.TreeExpander.New();
				expander.SetChild(box);
				return expander;
			}
			return box;
		}

		protected override void UpdateRowContainer(RowBinding binding, Gtk.ListItem listItem, RowContext context)
		{
			if (binding.Container is Gtk.TreeExpander expander && listItem.Item is Gtk.TreeListRow treeRow)
				expander.ListRow = treeRow;

			if (!(listItem.Item is Gtk.TreeListRow row))
				return;

			if (!rowExpandedEnabled && !rowCollapsedEnabled && !rowExpandingEnabled && !rowCollapsingEnabled)
				return;

			binding.DisposeAction?.Invoke();
			binding.DisposeAction = null;

			GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs> handler = (sender, args) => {
				if (args?.Pspec == null || args.Pspec.GetName() != "expanded")
					return;
				var pos = context?.TreePosition;
				if (pos == null)
					return;

				CurrentEventRow = pos;
				if (row.Expanded) {
					if (rowExpandingEnabled)
						ApplicationContext.InvokeUserCode(() => ((ITreeViewEventSink)EventSink).OnRowExpanding(pos));
					if (rowExpandedEnabled)
						ApplicationContext.InvokeUserCode(() => ((ITreeViewEventSink)EventSink).OnRowExpanded(pos));
				} else {
					if (rowCollapsingEnabled)
						ApplicationContext.InvokeUserCode(() => ((ITreeViewEventSink)EventSink).OnRowCollapsing(pos));
					if (rowCollapsedEnabled)
						ApplicationContext.InvokeUserCode(() => ((ITreeViewEventSink)EventSink).OnRowCollapsed(pos));
				}
			};

			row.OnNotify += handler;
			binding.DisposeAction = () => row.OnNotify -= handler;
		}

		protected override RowContext CreateRowContext(Gtk.ListItem listItem)
		{
			TreePosition pos = null;
			if (listItem.Item is Gtk.TreeListRow row) {
				var item = row.GetItem();
				if (item is GObject.Object obj && objectToPosition.TryGetValue(obj, out var mapped))
					pos = mapped;
			}

			return new RowContext {
				RowIndex = (int)listItem.Position,
				TreePosition = pos,
				TreeSource = source,
				CellDataSource = pos != null && source != null ? new TreeCellDataSource(source, pos) : null
			};
		}

		void RebuildModel()
		{
			objectToPosition.Clear();
			rootStore = Gio.ListStore.New(GObject.Object.GetGType());

			if (source != null) {
				var children = GetSortedChildren(null);
				for (int i = 0; i < children.Count; i++) {
					var pos = children[i];
					var obj = CreateNodeObject(pos);
					rootStore.Append(obj);
				}
			}

			treeModel = Gtk.TreeListModel.New(rootStore, false, false, CreateChildModel);
			SetListModel(treeModel);
		}

		Gio.ListModel CreateChildModel(GObject.Object item)
		{
			if (source == null)
				return null;

			if (!objectToPosition.TryGetValue(item, out var pos))
				return null;

			var count = source.GetChildrenCount(pos);
			if (count <= 0)
				return null;

			var store = Gio.ListStore.New(GObject.Object.GetGType());
			var children = GetSortedChildren(pos);
			for (int i = 0; i < children.Count; i++) {
				var child = children[i];
				var obj = CreateNodeObject(child);
				store.Append(obj);
			}
			return store;
		}

		GObject.Object CreateNodeObject(TreePosition pos)
		{
			var obj = (GObject.Object)GObject.Object.Newv(GObject.Object.GetGType(), Array.Empty<GObject.Parameter>());
			if (pos != null && !objectToPosition.ContainsKey(obj))
				objectToPosition[obj] = pos;
			return obj;
		}

		void AttachSource()
		{
			if (source == null)
				return;
			source.NodeInserted += HandleTreeChanged;
			source.NodeDeleted += HandleTreeChanged;
			source.NodeChanged += HandleTreeChanged;
			source.NodesReordered += HandleTreeChanged;
			source.Cleared += HandleTreeCleared;
		}

		void DetachSource()
		{
			if (source == null)
				return;
			source.NodeInserted -= HandleTreeChanged;
			source.NodeDeleted -= HandleTreeChanged;
			source.NodeChanged -= HandleTreeChanged;
			source.NodesReordered -= HandleTreeChanged;
			source.Cleared -= HandleTreeCleared;
		}

		void HandleTreeChanged(object sender, EventArgs e)
		{
			RebuildModel();
		}

		void HandleTreeCleared(object sender, EventArgs e)
		{
			RebuildModel();
		}

		void ExpandDescendants(Gtk.TreeListRow root)
		{
			if (treeModel == null || root == null)
				return;

			bool changed;
			do {
				changed = false;
				var count = (int)treeModel.GetNItems();
				for (int i = 0; i < count; i++) {
					var row = treeModel.GetRow((uint)i);
					if (row == null)
						continue;
					if (!IsDescendant(row, root))
						continue;
					if (!row.Expanded) {
						row.SetExpanded(true);
						changed = true;
					}
				}
			} while (changed);
		}

		static bool IsDescendant(Gtk.TreeListRow row, Gtk.TreeListRow ancestor)
		{
			for (var current = row?.GetParent(); current != null; current = current.GetParent()) {
				if (ReferenceEquals(current, ancestor))
					return true;
			}
			return false;
		}

		int FindRowIndex(TreePosition pos)
		{
			if (pos == null || treeModel == null)
				return -1;
			var count = (int)treeModel.GetNItems();
			for (int i = 0; i < count; i++) {
				var row = treeModel.GetRow((uint)i);
				if (row == null)
					continue;
				var item = row.GetItem();
				if (item is GObject.Object obj && objectToPosition.TryGetValue(obj, out var mapped)) {
					if (mapped.Equals(pos))
						return i;
				}
			}
			return -1;
		}

		TreePosition GetTreePositionFromRow(int rowIndex)
		{
			if (rowIndex < 0 || treeModel == null)
				return null;
			var row = treeModel.GetRow((uint)rowIndex);
			if (row == null)
				return null;
			var item = row.GetItem();
			if (item is GObject.Object obj && objectToPosition.TryGetValue(obj, out var pos))
				return pos;
			return null;
		}

		protected override void SetCurrentEventRow(RowContext context)
		{
			CurrentEventRow = context?.TreePosition;
		}

		protected override void OnColumnsChanged()
		{
			base.OnColumnsChanged();
			if (UpdateSortSettings())
				RebuildModel();
		}

		class TreeCellDataSource : ICellDataSource
		{
			readonly ITreeDataSource source;
			readonly TreePosition position;

			public TreeCellDataSource(ITreeDataSource source, TreePosition position)
			{
				this.source = source;
				this.position = position;
			}

			public object GetValue(IDataField field)
			{
				return source.GetValue(position, field.Index);
			}
		}

		bool UpdateSortSettings()
		{
			var previousField = sortField;
			var previousDirection = sortDirection;

			sortField = null;
			sortDirection = ColumnSortDirection.Ascending;

			foreach (var info in ColumnInfos) {
				var column = info.Column;
				if (column != null && column.SortIndicatorVisible && column.SortDataField != null) {
					sortField = column.SortDataField;
					sortDirection = column.SortDirection;
					break;
				}
			}

			return !Equals(previousField, sortField) || previousDirection != sortDirection;
		}

		List<TreePosition> GetSortedChildren(TreePosition parent)
		{
			if (source == null)
				return new List<TreePosition>();

			var count = source.GetChildrenCount(parent);
			var items = new List<(TreePosition Position, int Index)>(count);
			for (int i = 0; i < count; i++) {
				var pos = source.GetChild(parent, i);
				items.Add((pos, i));
			}

			if (sortField == null || items.Count <= 1) {
				var unsorted = new List<TreePosition>(items.Count);
				foreach (var item in items)
					unsorted.Add(item.Position);
				return unsorted;
			}

			items.Sort((left, right) => {
				var valueA = source.GetValue(left.Position, sortField.Index);
				var valueB = source.GetValue(right.Position, sortField.Index);
				int result = CompareValues(valueA, valueB);
				if (sortDirection == ColumnSortDirection.Descending)
					result = -result;
				if (result != 0)
					return result;
				return left.Index.CompareTo(right.Index);
			});

			var sorted = new List<TreePosition>(items.Count);
			foreach (var item in items)
				sorted.Add(item.Position);
			return sorted;
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
	}
}
