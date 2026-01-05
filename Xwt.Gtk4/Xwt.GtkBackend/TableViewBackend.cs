using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Backends;
using Xwt.CairoBackend;
using Xwt.Drawing;
using Graphene;

namespace Xwt.GtkBackend
{
	public abstract class TableViewBackend : WidgetBackend, ITableViewBackend, IColumnContainerBackend
	{
		static readonly bool logScrolledWindow = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_SCROLLED_LOG"), "1", StringComparison.Ordinal);
		static readonly bool logAdjustmentTrace = string.Equals(Environment.GetEnvironmentVariable("XWT_GTK4_ADJUSTMENT_TRACE"), "1", StringComparison.Ordinal);
		readonly Gtk.ScrolledWindow scrolledWindow;
		readonly Gtk.ColumnView columnView;
		readonly Gtk.SignalListItemFactory headerFactory;
		static Gtk.CssProvider alternatingRowProvider;
		static bool alternatingRowProviderInstalled;
		bool headerFactoryAttached = true;
		Gtk.SelectionModel selectionModel;
		Gio.ListModel listModel;
		SelectionMode selectionMode;
		bool selectionChangedEnabled;
		bool adjustmentFixing;
		Gtk.Adjustment scrolledHAdjustment;
		Gtk.Adjustment scrolledVAdjustment;
		Gtk.Adjustment columnHAdjustment;
		Gtk.Adjustment columnVAdjustment;
		Gtk.ColumnViewColumn autoExpandColumn;
		Gtk.TickCallback tickCallback;
		uint tickCallbackId;
		int lastRequestedWidth = -1;
		int lastRequestedHeight = -1;
		bool useAlternatingRowColors;

		const string AlternatingRowCssClass = "xwt-alt-row";

		readonly Dictionary<ListViewColumn, ColumnInfo> columns = new Dictionary<ListViewColumn, ColumnInfo>();
		readonly Dictionary<CellView, Gtk4CellViewBackend> cellBackends = new Dictionary<CellView, Gtk4CellViewBackend>();
		readonly Dictionary<Gtk.ListHeader, HeaderBinding> headerBindings = new Dictionary<Gtk.ListHeader, HeaderBinding>();

		protected TableViewBackend()
		{
			columnView = Gtk.ColumnView.New(null);
			columnView.HscrollPolicy = Gtk.ScrollablePolicy.Natural;
			columnView.VscrollPolicy = Gtk.ScrollablePolicy.Natural;
			headerFactory = Gtk.SignalListItemFactory.New();
			headerFactory.OnSetup += SetupHeaderItem;
			headerFactory.OnBind += BindHeaderItem;
			headerFactory.OnTeardown += TeardownHeaderItem;
			columnView.SetHeaderFactory(headerFactory);
			scrolledWindow = Gtk.ScrolledWindow.New();
			scrolledWindow.HasFrame = true;
			scrolledWindow.PropagateNaturalWidth = true;
			scrolledWindow.PropagateNaturalHeight = true;
			scrolledWindow.SetChild(columnView);
			columnView.Hexpand = true;
			columnView.Vexpand = true;
			scrolledWindow.Hexpand = true;
			scrolledWindow.Vexpand = true;
			columnView.Show();
			scrolledWindow.Show();
			Widget = scrolledWindow;
			tickCallback = HandleTick;
			tickCallbackId = scrolledWindow.AddTickCallback(tickCallback);
			if (logScrolledWindow) {
				Console.WriteLine($"[Xwt.Gtk4] TableViewBackend scrolled window created: 0x{scrolledWindow.Handle.DangerousGetHandle():x}");
				Console.WriteLine($"[Xwt.Gtk4] TableViewBackend column view created: 0x{columnView.Handle.DangerousGetHandle():x}");
			}
			scrolledWindow.OnNotify += HandleScrolledWindowNotify;
			columnView.OnNotify += HandleColumnViewNotify;
			AttachAdjustmentGuards();
			AttachColumnViewAdjustmentGuards();
		}

		protected Gtk.ColumnView ColumnView => columnView;

		protected Gtk.ScrolledWindow ScrolledWindow => scrolledWindow;

		protected IEnumerable<ColumnInfo> ColumnInfos => columns.Values;

		protected new ITableViewEventSink EventSink => (ITableViewEventSink)base.EventSink;

		protected virtual bool HeadersVisibleInternal => true;

		public ScrollPolicy VerticalScrollPolicy {
			get { return ToXwtPolicy(scrolledWindow.VscrollbarPolicy); }
			set { scrolledWindow.VscrollbarPolicy = ToGtkPolicy(value); }
		}

		public ScrollPolicy HorizontalScrollPolicy {
			get { return ToXwtPolicy(scrolledWindow.HscrollbarPolicy); }
			set { scrolledWindow.HscrollbarPolicy = ToGtkPolicy(value); }
		}

		public IScrollControlBackend CreateVerticalScrollControl()
		{
			return new ScrollControlBackend(scrolledWindow.Vadjustment);
		}

		public IScrollControlBackend CreateHorizontalScrollControl()
		{
			return new ScrollControlBackend(scrolledWindow.Hadjustment);
		}

		public void SetSelectionMode(SelectionMode mode)
		{
			selectionMode = mode;
			UpdateSelectionModel();
		}

		public void SelectAll()
		{
			selectionModel?.SelectAll();
		}

		public void UnselectAll()
		{
			selectionModel?.UnselectAll();
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is TableViewEvent ev && ev == TableViewEvent.SelectionChanged) {
				selectionChangedEnabled = true;
				AttachSelectionChanged();
			}
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is TableViewEvent ev && ev == TableViewEvent.SelectionChanged) {
				selectionChangedEnabled = false;
				DetachSelectionChanged();
			}
		}

		protected void SetListModel(Gio.ListModel model)
		{
			listModel = model;
			UpdateSelectionModel();
		}

		void UpdateSelectionModel()
		{
			DetachSelectionChanged();
			if (listModel == null) {
				selectionModel = null;
				columnView.SetModel(null);
				return;
			}

			switch (selectionMode) {
				case SelectionMode.Multiple:
					selectionModel = Gtk.MultiSelection.New(listModel);
					break;
				case SelectionMode.None:
					selectionModel = Gtk.NoSelection.New(listModel);
					break;
				default:
					selectionModel = Gtk.SingleSelection.New(listModel);
					break;
			}

			columnView.SetModel(selectionModel);
			AttachSelectionChanged();
		}

		void AttachSelectionChanged()
		{
			if (!selectionChangedEnabled || selectionModel == null)
				return;
			selectionModel.OnSelectionChanged += HandleSelectionChanged;
		}

		void DetachSelectionChanged()
		{
			if (selectionModel != null)
				selectionModel.OnSelectionChanged -= HandleSelectionChanged;
		}

		void HandleSelectionChanged(Gtk.SelectionModel sender, Gtk.SelectionModel.SelectionChangedSignalArgs args)
		{
			ApplicationContext.InvokeUserCode(EventSink.OnSelectionChanged);
		}

		public object AddColumn(ListViewColumn col)
		{
			if (col == null)
				throw new ArgumentNullException(nameof(col));

			var factory = Gtk.SignalListItemFactory.New();
			var gtkColumn = Gtk.ColumnViewColumn.New(col.Title ?? string.Empty, factory);
			gtkColumn.Resizable = col.CanResize;
			gtkColumn.Expand = col.Expands;
			gtkColumn.Visible = true;

			var info = new ColumnInfo(col, gtkColumn, factory) {
				IsFirstColumn = columnView.GetColumns().GetNItems() == 0
			};
			columns[col] = info;

			EnsureCellBackends(col.Views);
			UpdateColumnViews(info, col);

			columnView.AppendColumn(gtkColumn);
			OnColumnsChanged();
			return gtkColumn;
		}

		public void RemoveColumn(ListViewColumn col, object handle)
		{
			if (col == null || handle == null)
				return;
			if (columns.TryGetValue(col, out var info)) {
				info.Dispose();
				columns.Remove(col);
			}
			columnView.RemoveColumn((Gtk.ColumnViewColumn)handle);
			OnColumnsChanged();
		}

		public void UpdateColumn(ListViewColumn col, object handle, ListViewColumnChange change)
		{
			if (col == null || handle == null)
				return;

			var gtkColumn = (Gtk.ColumnViewColumn)handle;
			if (!columns.TryGetValue(col, out var info))
				return;

			switch (change) {
				case ListViewColumnChange.Cells:
					EnsureCellBackends(col.Views);
					UpdateColumnViews(info, col);
					break;
				case ListViewColumnChange.Title:
					gtkColumn.Title = col.Title ?? string.Empty;
					UpdateHeaderForColumn(info);
					break;
				case ListViewColumnChange.CanResize:
					gtkColumn.Resizable = col.CanResize;
					break;
				case ListViewColumnChange.Expanding:
					gtkColumn.Expand = col.Expands;
					break;
				case ListViewColumnChange.SortDirection:
				case ListViewColumnChange.SortDataField:
				case ListViewColumnChange.SortIndicatorVisible:
				case ListViewColumnChange.Alignment:
					ApplyColumnAlignment(info);
					UpdateHeaderForColumn(info);
					break;
			}
			OnColumnsChanged();
		}

		protected abstract RowContext CreateRowContext(Gtk.ListItem listItem);

		protected virtual Gtk.Widget CreateColumnContainer(ColumnInfo info, Gtk.ListItem listItem)
		{
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			return box;
		}

		protected virtual void UpdateRowContainer(RowBinding binding, Gtk.ListItem listItem, RowContext context)
		{
		}

		void SetupHeaderItem(Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.SetupSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listHeader = (Gtk.ListHeader)args.Object;
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			box.Halign = Gtk.Align.Fill;
			box.Valign = Gtk.Align.Center;
			box.Hexpand = true;
			listHeader.SetChild(box);
			headerBindings[listHeader] = new HeaderBinding(box);
		}

		void BindHeaderItem(Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.BindSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listHeader = (Gtk.ListHeader)args.Object;
			if (!headerBindings.TryGetValue(listHeader, out var binding))
				return;
			var column = GetHeaderColumn(listHeader);
			binding.ColumnInfo = FindColumnInfo(column);
			UpdateHeaderBinding(binding);
		}

		void TeardownHeaderItem(Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.TeardownSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listHeader = (Gtk.ListHeader)args.Object;
			if (headerBindings.TryGetValue(listHeader, out var binding)) {
				binding.Dispose();
				headerBindings.Remove(listHeader);
			}
		}

		void EnsureCellBackends(IEnumerable<CellView> views)
		{
			foreach (var view in views) {
				if (view == null || cellBackends.ContainsKey(view))
					continue;
				var backend = new Gtk4CellViewBackend();
				((ICellViewFrontend)view).AttachBackend((Widget)Frontend, backend);
				cellBackends[view] = backend;
			}
		}

		void UpdateColumnViews(ColumnInfo info, ListViewColumn col)
		{
			info.Views.Clear();
			info.Views.AddRange(col.Views);

			if (info.OnSetupHandler != null)
				info.Factory.OnSetup -= info.OnSetupHandler;
			if (info.OnBindHandler != null)
				info.Factory.OnBind -= info.OnBindHandler;
			if (info.OnTeardownHandler != null)
				info.Factory.OnTeardown -= info.OnTeardownHandler;

			info.OnSetupHandler = (sender, args) => SetupListItem(info, sender, args);
			info.OnBindHandler = (sender, args) => BindListItem(info, sender, args);
			info.OnTeardownHandler = (sender, args) => TeardownListItem(info, sender, args);

			info.Factory.OnSetup += info.OnSetupHandler;
			info.Factory.OnBind += info.OnBindHandler;
			info.Factory.OnTeardown += info.OnTeardownHandler;
		}

		protected void UpdateHeaderVisibility()
		{
			if (!HeadersVisibleInternal && headerFactoryAttached) {
				columnView.SetHeaderFactory(null);
				headerFactoryAttached = false;
				return;
			}

			if (HeadersVisibleInternal && !headerFactoryAttached) {
				columnView.SetHeaderFactory(headerFactory);
				headerFactoryAttached = true;
			}

			foreach (var binding in headerBindings.Values)
				UpdateHeaderBinding(binding);
		}

		void UpdateHeaderForColumn(ColumnInfo info)
		{
			foreach (var binding in headerBindings.Values) {
				if (binding.ColumnInfo == info)
					UpdateHeaderBinding(binding);
			}
		}

		void UpdateHeaderBinding(HeaderBinding binding)
		{
			if (binding?.Container == null)
				return;
			binding.Container.Visible = HeadersVisibleInternal;
			if (!HeadersVisibleInternal)
				return;

			var content = CreateHeaderContent(binding.ColumnInfo);
			if (ReferenceEquals(binding.Content, content))
				return;
			if (binding.Content != null)
				binding.Container.Remove(binding.Content);
			binding.Content = content;
			if (content != null) {
				binding.Container.Append(content);
				content.Show();
			}
		}

		Gtk.Widget CreateHeaderContent(ColumnInfo info)
		{
			var alignment = info?.Column?.Alignment ?? Alignment.Start;
			var headerView = info?.Column?.HeaderView;
			if (headerView != null)
				EnsureCellBackends(new[] { headerView });

			Gtk.Widget content;
			if (headerView is IImageCellViewFrontend imageView) {
				var picture = Gtk.Picture.New();
				var desc = imageView.Image.ToImageDescription(ApplicationContext);
				var gtkImage = desc.Backend as GtkImage;
				picture.SetPixbuf(gtkImage?.Pixbuf);
				picture.Halign = ToGtkAlign(alignment);
				picture.Valign = Gtk.Align.Center;
				content = picture;
			} else if (headerView is ICanvasCellViewFrontend canvasView) {
				var area = Gtk.DrawingArea.New();
				var size = canvasView.GetRequiredSize(SizeConstraint.Unconstrained);
				if (!size.IsZero)
					area.SetSizeRequest((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
				area.SetDrawFunc((drawingArea, cr, width, height) => DrawHeaderCanvas(canvasView, drawingArea, cr, width, height));
				area.Halign = ToGtkAlign(alignment);
				area.Valign = Gtk.Align.Center;
				content = area;
			} else {
				var text = GetHeaderText(info?.Column, out bool useMarkup);
				var label = Gtk.Label.New(text ?? string.Empty);
				label.UseMarkup = useMarkup;
				label.Halign = ToGtkAlign(alignment);
				label.Xalign = ToGtkXAlign(alignment);
				content = label;
			}
			return WrapHeaderWithSortIndicator(info, content, alignment);
		}

		static string GetHeaderText(ListViewColumn column, out bool useMarkup)
		{
			useMarkup = false;
			if (column?.HeaderView is TextCellView textView) {
				if (!string.IsNullOrEmpty(textView.Markup)) {
					useMarkup = true;
					return textView.Markup;
				}
				return textView.Text ?? string.Empty;
			}
			return column?.Title ?? string.Empty;
		}

		ColumnInfo FindColumnInfo(Gtk.ColumnViewColumn column)
		{
			if (column == null)
				return null;
			foreach (var info in columns.Values) {
				if (ReferenceEquals(info.GtkColumn, column))
					return info;
				if (info.GtkColumn?.Handle.DangerousGetHandle() == column.Handle.DangerousGetHandle())
					return info;
			}
			return null;
		}

		static Gtk.ColumnViewColumn GetHeaderColumn(Gtk.ListHeader listHeader)
		{
			if (listHeader == null)
				return null;
			var item = listHeader.GetItem();
			if (item is Gtk.ColumnViewColumn column)
				return column;
			return null;
		}

		protected virtual void OnColumnsChanged()
		{
			var columnList = columnView.GetColumns();
			if (columnList == null)
				return;
			int count = (int)columnList.GetNItems();
			bool firstVisibleSet = false;
			bool needsRebuild = false;
			Gtk.ColumnViewColumn lastVisible = null;

			for (int i = 0; i < count; i++) {
				var gtkColumn = columnList.GetObject((uint)i) as Gtk.ColumnViewColumn;
				if (gtkColumn == null)
					continue;
				if (gtkColumn.Visible)
					lastVisible = gtkColumn;
				var info = FindColumnInfo(gtkColumn);
				if (info == null)
					continue;
				bool isFirst = false;
				if (gtkColumn.Visible && !firstVisibleSet) {
					isFirst = true;
					firstVisibleSet = true;
				}
				if (info.IsFirstColumn != isFirst) {
					info.IsFirstColumn = isFirst;
					needsRebuild = true;
				}
			}

			UpdateAutoExpandColumn(lastVisible);

			if (!needsRebuild || selectionModel == null)
				return;

			columnView.SetModel(null);
			columnView.SetModel(selectionModel);
		}

		void UpdateAutoExpandColumn(Gtk.ColumnViewColumn lastVisible)
		{
			bool hasExpandingColumn = false;
			foreach (var info in columns.Values) {
				if (info.Column?.Expands == true) {
					hasExpandingColumn = true;
					break;
				}
			}

			if (autoExpandColumn != null && (hasExpandingColumn || !ReferenceEquals(autoExpandColumn, lastVisible))) {
				var info = FindColumnInfo(autoExpandColumn);
				if (info != null)
					autoExpandColumn.Expand = info.Column?.Expands ?? false;
				autoExpandColumn = null;
			}

			if (!hasExpandingColumn && lastVisible != null) {
				if (!ReferenceEquals(autoExpandColumn, lastVisible)) {
					autoExpandColumn = lastVisible;
					autoExpandColumn.Expand = true;
				}
			}
		}

		void AttachAdjustmentGuards()
		{
			var hadj = scrolledWindow.Hadjustment;
			var vadj = scrolledWindow.Vadjustment;
			if (hadj != null && hadj != scrolledHAdjustment) {
				if (scrolledHAdjustment != null) {
					scrolledHAdjustment.OnChanged -= HandleAdjustmentChanged;
					scrolledHAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				hadj.OnChanged += HandleAdjustmentChanged;
				hadj.OnNotify += HandleAdjustmentNotify;
				scrolledHAdjustment = hadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] TableViewBackend attach sw hadj=0x{hadj.Handle.DangerousGetHandle():x} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x}");
			}
			if (vadj != null && vadj != scrolledVAdjustment) {
				if (scrolledVAdjustment != null) {
					scrolledVAdjustment.OnChanged -= HandleAdjustmentChanged;
					scrolledVAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				vadj.OnChanged += HandleAdjustmentChanged;
				vadj.OnNotify += HandleAdjustmentNotify;
				scrolledVAdjustment = vadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] TableViewBackend attach sw vadj=0x{vadj.Handle.DangerousGetHandle():x} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x}");
			}
		}

		void AttachColumnViewAdjustmentGuards()
		{
			var hadj = columnView.Hadjustment;
			var vadj = columnView.Vadjustment;
			if (hadj != null && hadj != columnHAdjustment) {
				if (columnHAdjustment != null && columnHAdjustment != scrolledHAdjustment) {
					columnHAdjustment.OnChanged -= HandleAdjustmentChanged;
					columnHAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				if (hadj != scrolledHAdjustment) {
					hadj.OnChanged += HandleAdjustmentChanged;
					hadj.OnNotify += HandleAdjustmentNotify;
				}
				columnHAdjustment = hadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] TableViewBackend attach cv hadj=0x{hadj.Handle.DangerousGetHandle():x} cv=0x{columnView.Handle.DangerousGetHandle():x}");
			}
			if (vadj != null && vadj != columnVAdjustment) {
				if (columnVAdjustment != null && columnVAdjustment != scrolledVAdjustment) {
					columnVAdjustment.OnChanged -= HandleAdjustmentChanged;
					columnVAdjustment.OnNotify -= HandleAdjustmentNotify;
				}
				if (vadj != scrolledVAdjustment) {
					vadj.OnChanged += HandleAdjustmentChanged;
					vadj.OnNotify += HandleAdjustmentNotify;
				}
				columnVAdjustment = vadj;
				if (logAdjustmentTrace)
					Console.WriteLine($"[Xwt.Gtk4] TableViewBackend attach cv vadj=0x{vadj.Handle.DangerousGetHandle():x} cv=0x{columnView.Handle.DangerousGetHandle():x}");
			}
		}

		void HandleScrolledWindowNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name == "hadjustment" || name == "vadjustment")
				AttachAdjustmentGuards();
		}

		void HandleColumnViewNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name == "hadjustment" || name == "vadjustment")
				AttachColumnViewAdjustmentGuards();
		}


		void HandleAdjustmentChanged(Gtk.Adjustment sender, EventArgs e)
		{
			if (adjustmentFixing)
				return;
			if (logAdjustmentTrace)
				LogAdjustment("TableViewBackend", sender);
			adjustmentFixing = true;
			try {
				FixAdjustment(sender);
			} finally {
				adjustmentFixing = false;
			}
		}

		void HandleAdjustmentNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (adjustmentFixing || !(sender is Gtk.Adjustment adjustment))
				return;
			if (args?.Pspec == null)
				return;
			var name = args.Pspec.GetName();
			if (name != "upper" && name != "page-size" && name != "lower")
				return;
			if (logAdjustmentTrace)
				LogAdjustment($"TableViewBackend notify:{name}", adjustment);
			if (adjustment.PageSize > adjustment.Upper && (adjustment == scrolledHAdjustment || adjustment == scrolledVAdjustment))
				UpdateColumnViewRequest(scrolledWindow.GetAllocatedWidth(), scrolledWindow.GetAllocatedHeight());
			adjustmentFixing = true;
			try {
				FixAdjustment(adjustment);
			} finally {
				adjustmentFixing = false;
			}
		}

		void LogAdjustment(string source, Gtk.Adjustment adjustment)
		{
			if (adjustment == null)
				return;
			string owner = "unknown";
			if (adjustment == scrolledHAdjustment || adjustment == scrolledVAdjustment)
				owner = "scrolled-window";
			else if (adjustment == columnHAdjustment || adjustment == columnVAdjustment)
				owner = "column-view";
			int swW = scrolledWindow.GetAllocatedWidth();
			int swH = scrolledWindow.GetAllocatedHeight();
			int cvW = columnView.GetAllocatedWidth();
			int cvH = columnView.GetAllocatedHeight();
			Console.WriteLine($"[Xwt.Gtk4] {source} owner={owner} adj=0x{adjustment.Handle.DangerousGetHandle():x} lower={adjustment.Lower} upper={adjustment.Upper} page={adjustment.PageSize} value={adjustment.Value} sw=0x{scrolledWindow.Handle.DangerousGetHandle():x} swSize={swW}x{swH} cv=0x{columnView.Handle.DangerousGetHandle():x} cvSize={cvW}x{cvH}");
		}

		bool HandleTick(Gtk.Widget widget, Gdk.FrameClock frameClock)
		{
			UpdateColumnViewRequest(scrolledWindow.GetAllocatedWidth(), scrolledWindow.GetAllocatedHeight());
			return true;
		}

		void UpdateColumnViewRequest(int width, int height)
		{
			if (width <= 0 || height <= 0) {
				if (lastRequestedWidth != -1 || lastRequestedHeight != -1) {
					lastRequestedWidth = -1;
					lastRequestedHeight = -1;
					columnView.SetSizeRequest(-1, -1);
				}
				return;
			}
			if (width == lastRequestedWidth && height == lastRequestedHeight)
				return;
			lastRequestedWidth = width;
			lastRequestedHeight = height;
			columnView.SetSizeRequest(width, height);
			columnView.QueueResize();
		}

		static void FixAdjustment(Gtk.Adjustment adjustment)
		{
			ScrollAdjustmentBackend.FixAdjustment(adjustment);
		}

		protected bool TryGetRowBinding(Func<RowContext, bool> predicate, bool firstColumnOnly, out RowBinding binding)
		{
			binding = null;
			if (predicate == null)
				return false;

			foreach (var info in ColumnInfos) {
				if (firstColumnOnly && !info.IsFirstColumn)
					continue;
				foreach (var rowBinding in info.Bindings.Values) {
					if (rowBinding.Context == null || !predicate(rowBinding.Context))
						continue;
					binding = rowBinding;
					return true;
				}
			}

			if (firstColumnOnly)
				return TryGetRowBinding(predicate, false, out binding);

			return false;
		}

		protected bool TryGetCellBinding(Func<RowContext, bool> predicate, CellView cell, out RowBinding rowBinding, out CellBinding cellBinding)
		{
			rowBinding = null;
			cellBinding = null;
			if (predicate == null)
				return false;

			RowBinding fallback = null;

			foreach (var info in ColumnInfos) {
				foreach (var binding in info.Bindings.Values) {
					var context = binding.Context;
					if (context == null || !predicate(context))
						continue;
					if (cell == null) {
						rowBinding = binding;
						return true;
					}
					foreach (var candidate in binding.Cells) {
						if (candidate.View == cell) {
							rowBinding = binding;
							cellBinding = candidate;
							return true;
						}
					}
					if (fallback == null)
						fallback = binding;
				}
			}

			if (cell == null && fallback != null) {
				rowBinding = fallback;
				return true;
			}

			return false;
		}

		protected Rectangle GetWidgetBounds(Gtk.Widget widget)
		{
			if (widget == null)
				return Rectangle.Zero;
			if (!widget.ComputeBounds(ColumnView, out Rect bounds))
				return Rectangle.Zero;
			return new Rectangle(bounds.GetX(), bounds.GetY(), bounds.GetWidth(), bounds.GetHeight());
		}

		void SetupListItem(ColumnInfo info, Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.SetupSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;

			var container = CreateColumnContainer(info, listItem);
			Gtk.Box contentBox = container as Gtk.Box;
			if (container is Gtk.TreeExpander expander && expander.Child is Gtk.Box expanderBox)
				contentBox = expanderBox;
			if (contentBox != null) {
				contentBox.Halign = Gtk.Align.Fill;
				contentBox.Valign = Gtk.Align.Center;
				contentBox.Hexpand = true;
			}
			listItem.SetChild(container);

			var binding = new RowBinding(container, listItem, contentBox, info);
			foreach (var view in info.Views) {
				if (view == null)
					continue;
				var cellBinding = CreateCellBinding(view, binding);
				binding.Cells.Add(cellBinding);
				if (contentBox != null)
					contentBox.Append(cellBinding.Widget);
			}
			ApplyColumnAlignment(info, binding);
			info.Bindings[listItem] = binding;
		}

		void BindListItem(ColumnInfo info, Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.BindSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;
			if (!info.Bindings.TryGetValue(listItem, out var binding))
				return;

			var context = CreateRowContext(listItem);
			binding.Context = context;

			foreach (var cell in binding.Cells) {
				var viewFrontend = (ICellViewFrontend)cell.View;
				viewFrontend.Load(context.CellDataSource);
				UpdateCellWidget(binding, cell, context);
			}

			UpdateRowContainer(binding, listItem, context);
			ApplyAlternatingRowStyle(binding);
		}

		void TeardownListItem(ColumnInfo info, Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.TeardownSignalArgs args)
		{
			if (args.Object == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;
			if (info.Bindings.TryGetValue(listItem, out var binding)) {
				binding.Dispose();
				info.Bindings.Remove(listItem);
			}
		}

		CellBinding CreateCellBinding(CellView view, RowBinding binding)
		{
			var viewFrontend = (ICellViewFrontend)view;
			Gtk.Widget widget;

			if (viewFrontend is ITextCellViewFrontend) {
				var label = Gtk.Label.New(string.Empty);
				label.Show();
				return new CellBinding(view, label);
			} else if (viewFrontend is ICanvasCellViewFrontend canvasView) {
				var area = Gtk.DrawingArea.New();
				var cellBinding = new CellBinding(view, area);
				area.Halign = Gtk.Align.Fill;
				area.Valign = Gtk.Align.Fill;
				area.SetDrawFunc((drawingArea, cr, width, height) => DrawCanvasCell(canvasView, binding, cellBinding, drawingArea, cr, width, height));
				AttachCanvasEvents(binding, cellBinding, area);
				area.Show();
				return cellBinding;
			} else if (viewFrontend is ICheckBoxCellViewFrontend || viewFrontend is IRadioButtonCellViewFrontend) {
				var button = Gtk.CheckButton.New();
				var cellBinding = new CellBinding(view, button);
				button.OnToggled += (sender, args) => HandleToggle(binding, cellBinding, viewFrontend, button);
				button.Show();
				return cellBinding;
			} else if (viewFrontend is IComboBoxCellViewFrontend) {
				var dropdown = Gtk.DropDown.NewFromStrings(Array.Empty<string>());
				var cellBinding = new CellBinding(view, dropdown);
				dropdown.OnActivate += (sender, args) => HandleComboChanged(binding, cellBinding, viewFrontend, dropdown);
				dropdown.Show();
				return cellBinding;
			} else if (viewFrontend is IImageCellViewFrontend) {
				var picture = Gtk.Picture.New();
				picture.SetKeepAspectRatio(true);
				picture.SetCanShrink(false);
				picture.Halign = Gtk.Align.Center;
				picture.Valign = Gtk.Align.Center;
				widget = picture;
			} else {
				var label = Gtk.Label.New(string.Empty);
				widget = label;
			}

			widget.Show();
			return new CellBinding(view, widget);
		}

		void UpdateCellWidget(RowBinding binding, CellBinding cell, RowContext context)
		{
			var viewFrontend = (ICellViewFrontend)cell.View;

			if (viewFrontend is ITextCellViewFrontend textView) {
				EnsureTextCellWidget(binding, cell, textView);
				if (cell.Widget is Gtk.EditableLabel editLabel) {
					var text = textView.Text ?? string.Empty;
					if (editLabel.Text_ != text) {
						cell.IsUpdating = true;
						editLabel.Text_ = text;
						cell.IsUpdating = false;
					}
					editLabel.Editable = textView.Editable;
					editLabel.Visible = textView.Visible;
					return;
				}
				if (cell.Widget is Gtk.Label label) {
					if (!string.IsNullOrEmpty(textView.Markup)) {
						var formatted = FormattedText.FromMarkup(textView.Markup);
						FormattedTextUtil.ApplyFormattedText(label, formatted);
					} else {
						label.UseMarkup = false;
						label.Label_ = textView.Text ?? string.Empty;
						label.SetAttributes(null);
					}
					label.Ellipsize = (Pango.EllipsizeMode)(int)textView.Ellipsize;
					label.Visible = textView.Visible;
					return;
				}
			}

			if ((viewFrontend is ICheckBoxCellViewFrontend || viewFrontend is IRadioButtonCellViewFrontend) && cell.Widget is Gtk.CheckButton check) {
				var toggleView = (IToggleCellViewFrontend)viewFrontend;
				check.Sensitive = toggleView.Editable;
				check.Visible = toggleView.Visible;

				cell.IsUpdating = true;
				if (viewFrontend is ICheckBoxCellViewFrontend checkView) {
					check.Inconsistent = checkView.State == CheckBoxState.Mixed;
					check.Active = checkView.State == CheckBoxState.On;
				} else if (viewFrontend is IRadioButtonCellViewFrontend radioView) {
					check.Active = radioView.Active;
				}
				cell.IsUpdating = false;
				return;
			}

			if (viewFrontend is IComboBoxCellViewFrontend comboView && cell.Widget is Gtk.DropDown dropDown) {
				cell.IsUpdating = true;
				UpdateCombo(dropDown, comboView, context);
				cell.IsUpdating = false;
				dropDown.Visible = comboView.Visible;
				return;
			}

			if (viewFrontend is IImageCellViewFrontend imageView && cell.Widget is Gtk.Picture picture) {
				Image image = imageView.Image;
				var desc = image.ToImageDescription(ApplicationContext);
				var gtkImage = desc.Backend as GtkImage;
				picture.SetPixbuf(gtkImage?.Pixbuf);
				picture.Visible = imageView.Visible;
				return;
			}

			if (viewFrontend is ICanvasCellViewFrontend canvasView && cell.Widget is Gtk.DrawingArea area) {
				var size = canvasView.GetRequiredSize(SizeConstraint.Unconstrained);
				if (!size.IsZero)
					area.SetSizeRequest((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
				area.Visible = canvasView.Visible;
				area.QueueDraw();
			}
		}

		void EnsureTextCellWidget(RowBinding binding, CellBinding cell, ITextCellViewFrontend textView)
		{
			bool wantsEditable = textView.Editable;
			if (wantsEditable && cell.Widget is Gtk.EditableLabel)
				return;
			if (!wantsEditable && cell.Widget is Gtk.Label)
				return;

			var container = binding?.ContentBox;
			if (container == null)
				return;

			if (cell.Widget != null)
				container.Remove(cell.Widget);

			if (wantsEditable) {
				var label = Gtk.EditableLabel.New(string.Empty);
				label.OnChanged += (sender, args) => HandleTextEdited(binding, cell, textView, label);
				label.Show();
				cell.Widget = label;
			} else {
				var label = Gtk.Label.New(string.Empty);
				label.Show();
				cell.Widget = label;
			}
			container.Append(cell.Widget);
			ApplyColumnAlignmentForCell(binding, cell);
		}

		void ApplyColumnAlignmentForCell(RowBinding binding, CellBinding cell)
		{
			if (binding == null || cell == null)
				return;
			var columnInfo = binding.ColumnInfo;
			if (columnInfo?.Column == null)
				return;
			cell.Widget.Halign = ToGtkAlign(columnInfo.Column.Alignment);
		}

		Gtk4CellViewBackend GetCellBackend(CellView view)
		{
			if (view == null)
				return null;
			if (cellBackends.TryGetValue(view, out var backend))
				return backend;
			return null;
		}

		CellViewStatus CreateCellStatus(RowBinding binding, CellBinding cell)
		{
			var status = new CellViewStatus {
				CellBounds = GetWidgetBounds(cell.Widget),
				BackgroundBounds = GetWidgetBounds(binding.Container),
				Selected = binding.ListItem?.GetSelected() ?? false,
				HasFocus = ColumnView.HasFocus
			};
			return status;
		}

		void DrawCanvasCell(ICanvasCellViewFrontend canvasView, RowBinding binding, CellBinding cell, Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
		{
			if (binding.Context == null)
				return;
			var backend = GetCellBackend(cell.View);
			if (backend == null)
				return;
			var status = CreateCellStatus(binding, cell);
			backend.LoadData(binding.Context.CellDataSource, status, area);
			var rect = new Rectangle(0, 0, width, height);
			var drawArea = canvasView.GetDrawingAreaForBounds(rect);
			var ctxBackend = new CairoContextBackend(area.ScaleFactor) {
				Context = cr
			};
			if (ApplicationContext != null)
				ApplicationContext.InvokeUserCode(() => canvasView.Draw(ctxBackend, drawArea));
			else
				canvasView.Draw(ctxBackend, drawArea);
		}

		void DrawHeaderCanvas(ICanvasCellViewFrontend canvasView, Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
		{
			var rect = new Rectangle(0, 0, width, height);
			var drawArea = canvasView.GetDrawingAreaForBounds(rect);
			var ctxBackend = new CairoContextBackend(area.ScaleFactor) {
				Context = cr
			};
			if (ApplicationContext != null)
				ApplicationContext.InvokeUserCode(() => canvasView.Draw(ctxBackend, drawArea));
			else
				canvasView.Draw(ctxBackend, drawArea);
		}

		void AttachCanvasEvents(RowBinding binding, CellBinding cell, Gtk.DrawingArea area)
		{
			var click = Gtk.GestureClick.New();
			click.SetButton(0);
			click.OnPressed += (sender, args) => HandleCellPressed(binding, cell, click, args.X, args.Y);
			click.OnReleased += (sender, args) => HandleCellReleased(binding, cell, click, args.X, args.Y);
			area.AddController(click);

			var motion = Gtk.EventControllerMotion.New();
			motion.OnEnter += (sender, args) => HandleCellEnter(binding, cell);
			motion.OnLeave += (sender, args) => HandleCellLeave(binding, cell);
			motion.OnMotion += (sender, args) => HandleCellMotion(binding, cell, motion, args.X, args.Y);
			area.AddController(motion);
		}

		void HandleCellPressed(RowBinding binding, CellBinding cell, Gtk.GestureClick gesture, double x, double y)
		{
			var backend = GetCellBackend(cell.View);
			if (backend == null || !backend.IsEventEnabled(WidgetEvent.ButtonPressed))
				return;
			var sink = PrepareCellEvent(binding, cell, backend);
			if (sink == null)
				return;
			var button = (PointerButton)gesture.GetCurrentButton();
			var args = new ButtonEventArgs {
				X = x,
				Y = y,
				Button = button,
				IsContextMenuTrigger = button == PointerButton.Right
			};
			ApplicationContext.InvokeUserCode(() => sink.OnButtonPressed(args));
		}

		void HandleCellReleased(RowBinding binding, CellBinding cell, Gtk.GestureClick gesture, double x, double y)
		{
			var backend = GetCellBackend(cell.View);
			if (backend == null || !backend.IsEventEnabled(WidgetEvent.ButtonReleased))
				return;
			var sink = PrepareCellEvent(binding, cell, backend);
			if (sink == null)
				return;
			var button = (PointerButton)gesture.GetCurrentButton();
			var args = new ButtonEventArgs {
				X = x,
				Y = y,
				Button = button
			};
			ApplicationContext.InvokeUserCode(() => sink.OnButtonReleased(args));
		}

		void HandleCellEnter(RowBinding binding, CellBinding cell)
		{
			var backend = GetCellBackend(cell.View);
			if (backend == null || !backend.IsEventEnabled(WidgetEvent.MouseEntered))
				return;
			var sink = PrepareCellEvent(binding, cell, backend);
			if (sink == null)
				return;
			ApplicationContext.InvokeUserCode(sink.OnMouseEntered);
		}

		void HandleCellLeave(RowBinding binding, CellBinding cell)
		{
			var backend = GetCellBackend(cell.View);
			if (backend == null || !backend.IsEventEnabled(WidgetEvent.MouseExited))
				return;
			var sink = PrepareCellEvent(binding, cell, backend);
			if (sink == null)
				return;
			ApplicationContext.InvokeUserCode(sink.OnMouseExited);
		}

		void HandleCellMotion(RowBinding binding, CellBinding cell, Gtk.EventControllerMotion motion, double x, double y)
		{
			var backend = GetCellBackend(cell.View);
			if (backend == null || !backend.IsEventEnabled(WidgetEvent.MouseMoved))
				return;
			var sink = PrepareCellEvent(binding, cell, backend);
			if (sink == null)
				return;
			var timestamp = (long)motion.GetCurrentEventTime();
			var args = new MouseMovedEventArgs(timestamp, x, y);
			ApplicationContext.InvokeUserCode(() => sink.OnMouseMoved(args));
		}

		ICellViewEventSink PrepareCellEvent(RowBinding binding, CellBinding cell, Gtk4CellViewBackend backend)
		{
			if (binding.Context == null)
				return null;
			var status = CreateCellStatus(binding, cell);
			return backend.LoadData(binding.Context.CellDataSource, status, cell.Widget);
		}

		void HandleTextEdited(RowBinding binding, CellBinding cell, ICellViewFrontend viewFrontend, Gtk.EditableLabel label)
		{
			if (binding.Context == null || cell.IsUpdating)
				return;

			SetCurrentEventRow(binding.Context);
			var textView = (ITextCellViewFrontend)viewFrontend;
			var newText = label.Text_ ?? string.Empty;
			if (textView.RaiseTextChanged(newText))
				return;

			if (textView.TextField == null)
				return;

			if (binding.Context.ListSource != null)
				binding.Context.ListSource.SetValue(binding.Context.RowIndex, textView.TextField.Index, newText);
			else if (binding.Context.TreeSource != null && binding.Context.TreePosition != null)
				binding.Context.TreeSource.SetValue(binding.Context.TreePosition, textView.TextField.Index, newText);
		}

		void HandleToggle(RowBinding binding, CellBinding cell, ICellViewFrontend viewFrontend, Gtk.CheckButton button)
		{
			if (binding.Context == null || cell.IsUpdating)
				return;

			SetCurrentEventRow(binding.Context);
			var toggleView = (IToggleCellViewFrontend)viewFrontend;
			IDataField field = (IDataField)(viewFrontend as ICheckBoxCellViewFrontend)?.StateField ?? toggleView.ActiveField;

			if (toggleView.RaiseToggled() || field == null)
				return;

			object newValue = null;
			if (viewFrontend is ICheckBoxCellViewFrontend checkView && field.FieldType == typeof(CheckBoxState)) {
				CheckBoxState newState;
				if (checkView.AllowMixed) {
					if (button.Inconsistent)
						newState = CheckBoxState.Off;
					else if (button.Active)
						newState = CheckBoxState.Mixed;
					else
						newState = CheckBoxState.On;
				} else {
					newState = button.Active ? CheckBoxState.On : CheckBoxState.Off;
				}
				newValue = newState;
			} else if (field.FieldType == typeof(bool)) {
				newValue = button.Active;
			}

			if (newValue == null)
				return;

			if (binding.Context.ListSource != null)
				binding.Context.ListSource.SetValue(binding.Context.RowIndex, field.Index, newValue);
			else if (binding.Context.TreeSource != null && binding.Context.TreePosition != null)
				binding.Context.TreeSource.SetValue(binding.Context.TreePosition, field.Index, newValue);
		}

		void HandleComboChanged(RowBinding binding, CellBinding cell, ICellViewFrontend viewFrontend, Gtk.DropDown dropDown)
		{
			if (binding.Context == null || cell.IsUpdating)
				return;

			SetCurrentEventRow(binding.Context);
			var comboView = (IComboBoxCellViewFrontend)viewFrontend;
			if (comboView.RaiseSelectionChanged())
				return;

			var source = comboView.ItemsSource;
			var selectedIndex = (int)dropDown.Selected;
			if (source != null && selectedIndex >= 0 && selectedIndex < source.RowCount) {
				if (comboView.SelectedItemField != null && source.ColumnTypes.Length > 1) {
					var item = source.GetValue(selectedIndex, 1);
					SetComboValue(binding, comboView.SelectedItemField, item);
				}
				if (comboView.SelectedIndexField != null)
					SetComboValue(binding, comboView.SelectedIndexField, selectedIndex);
				if (comboView.SelectedTextField != null) {
					var text = source.GetValue(selectedIndex, 0)?.ToString();
					SetComboValue(binding, comboView.SelectedTextField, text);
				}
			}
		}

		void UpdateCombo(Gtk.DropDown dropDown, IComboBoxCellViewFrontend comboView, RowContext context)
		{
			var source = comboView.ItemsSource;
			string[] items;
			if (source != null) {
				items = new string[source.RowCount];
				for (int i = 0; i < source.RowCount; i++)
					items[i] = source.GetValue(i, 0)?.ToString() ?? string.Empty;
			} else {
				items = Array.Empty<string>();
			}

			var model = Gtk.StringList.New(items);
			dropDown.SetModel(model);

			int selectedIndex = -1;
			if (comboView.SelectedIndexField != null && context.CellDataSource != null) {
				var val = context.CellDataSource.GetValue(comboView.SelectedIndexField);
				selectedIndex = val != null ? Convert.ToInt32(val) : -1;
			} else if (comboView.SelectedTextField != null) {
				var selectedText = comboView.SelectedText ?? string.Empty;
				for (int i = 0; i < items.Length; i++) {
					if (items[i] == selectedText) {
						selectedIndex = i;
						break;
					}
				}
			}

			if (selectedIndex >= 0 && selectedIndex < items.Length)
				dropDown.Selected = (uint)selectedIndex;
		}

		void SetComboValue(RowBinding binding, IDataField field, object value)
		{
			if (binding.Context.ListSource != null)
				binding.Context.ListSource.SetValue(binding.Context.RowIndex, field.Index, value);
			else if (binding.Context.TreeSource != null && binding.Context.TreePosition != null)
				binding.Context.TreeSource.SetValue(binding.Context.TreePosition, field.Index, value);
		}

		protected virtual void SetCurrentEventRow(RowContext context)
		{
		}

		protected int[] GetSelectedIndexes()
		{
			if (selectionModel == null)
				return Array.Empty<int>();
			var bitset = selectionModel.GetSelection();
			if (bitset == null || bitset.IsEmpty())
				return Array.Empty<int>();

			var count = (int)bitset.GetSize();
			var result = new int[count];
			for (uint i = 0; i < count; i++)
				result[i] = (int)bitset.GetNth(i);
			return result;
		}

		protected void SelectRowIndex(int row, bool unselectRest)
		{
			if (selectionModel == null || row < 0)
				return;
			selectionModel.SelectItem((uint)row, unselectRest);
		}

		protected void UnselectRowIndex(int row)
		{
			if (selectionModel == null || row < 0)
				return;
			selectionModel.UnselectItem((uint)row);
		}

		protected void ScrollToIndex(int row)
		{
			if (row < 0)
				return;
			using var info = Gtk.ScrollInfo.New();
			columnView.ScrollTo((uint)row, null, (Gtk.ListScrollFlags)0, info);
		}

		void ApplyColumnAlignment(ColumnInfo info)
		{
			foreach (var binding in info.Bindings.Values)
				ApplyColumnAlignment(info, binding);
		}

		void ApplyColumnAlignment(ColumnInfo info, RowBinding binding)
		{
			if (info?.Column == null || binding == null)
				return;
			var align = ToGtkAlign(info.Column.Alignment);
			foreach (var cell in binding.Cells) {
				if (cell?.Widget == null)
					continue;
				cell.Widget.Halign = align;
			}
		}

		static Gtk.Align ToGtkAlign(Alignment alignment)
		{
			switch (alignment) {
				case Alignment.Center:
					return Gtk.Align.Center;
				case Alignment.End:
					return Gtk.Align.End;
				default:
					return Gtk.Align.Start;
			}
		}

		static float ToGtkXAlign(Alignment alignment)
		{
			switch (alignment) {
				case Alignment.Center:
					return 0.5f;
				case Alignment.End:
					return 1f;
				default:
					return 0f;
			}
		}

		protected void SetAlternatingRowColors(bool value)
		{
			useAlternatingRowColors = value;
			if (useAlternatingRowColors)
				EnsureAlternatingRowCss();
			UpdateAlternatingRowStyles();
		}

		protected bool GetAlternatingRowColors()
		{
			return useAlternatingRowColors;
		}

		void UpdateAlternatingRowStyles()
		{
			foreach (var info in columns.Values) {
				foreach (var binding in info.Bindings.Values)
					ApplyAlternatingRowStyle(binding);
			}
		}

		void ApplyAlternatingRowStyle(RowBinding binding)
		{
			if (binding?.Container == null)
				return;
			int rowIndex = -1;
			if (binding.Context != null && binding.Context.RowIndex >= 0)
				rowIndex = binding.Context.RowIndex;
			else if (binding.ListItem != null)
				rowIndex = (int)binding.ListItem.Position;
			bool isAlternate = useAlternatingRowColors && rowIndex >= 0 && (rowIndex % 2 != 0);
			ToggleCssClass(binding.Container, AlternatingRowCssClass, isAlternate);
		}

		static void ToggleCssClass(Gtk.Widget widget, string cssClass, bool enabled)
		{
			if (widget == null || string.IsNullOrEmpty(cssClass))
				return;
			if (enabled) {
				if (!widget.HasCssClass(cssClass))
					widget.AddCssClass(cssClass);
			} else {
				if (widget.HasCssClass(cssClass))
					widget.RemoveCssClass(cssClass);
			}
		}

		static void EnsureAlternatingRowCss()
		{
			if (alternatingRowProviderInstalled)
				return;
			alternatingRowProviderInstalled = true;
			alternatingRowProvider = Gtk.CssProvider.New();
			alternatingRowProvider.LoadFromString(".xwt-alt-row { background-color: shade(@theme_base_color, 0.98); }");
			var display = Gdk.Display.GetDefault();
			if (display != null)
				Gtk.StyleContext.AddProviderForDisplay(display, alternatingRowProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
		}

		Gtk.Widget WrapHeaderWithSortIndicator(ColumnInfo info, Gtk.Widget content, Alignment alignment)
		{
			if (info?.Column == null || !info.Column.SortIndicatorVisible || content == null)
				return content;
			var iconName = info.Column.SortDirection == ColumnSortDirection.Descending ? "pan-down-symbolic" : "pan-up-symbolic";
			var arrow = Gtk.Image.NewFromIconName(iconName);
			arrow.Valign = Gtk.Align.Center;
			var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 4);
			box.Halign = ToGtkAlign(alignment);
			box.Valign = Gtk.Align.Center;
			if (alignment == Alignment.End) {
				box.Append(arrow);
				box.Append(content);
			} else {
				box.Append(content);
				box.Append(arrow);
			}
			box.Show();
			return box;
		}

		protected static Gtk.PolicyType ToGtkPolicy(ScrollPolicy policy)
		{
			switch (policy) {
				case ScrollPolicy.Always:
					return Gtk.PolicyType.Always;
				case ScrollPolicy.Never:
					return Gtk.PolicyType.Never;
				default:
					return Gtk.PolicyType.Automatic;
			}
		}

		protected static ScrollPolicy ToXwtPolicy(Gtk.PolicyType policy)
		{
			switch (policy) {
				case Gtk.PolicyType.Always:
					return ScrollPolicy.Always;
				case Gtk.PolicyType.Never:
					return ScrollPolicy.Never;
				default:
					return ScrollPolicy.Automatic;
			}
		}

		protected class ColumnInfo
		{
			public bool IsFirstColumn { get; set; }
			public ListViewColumn Column { get; }
			public Gtk.ColumnViewColumn GtkColumn { get; }
			public Gtk.SignalListItemFactory Factory { get; }
			public List<CellView> Views { get; } = new List<CellView>();
			public Dictionary<Gtk.ListItem, RowBinding> Bindings { get; } = new Dictionary<Gtk.ListItem, RowBinding>();

			public GObject.SignalHandler<Gtk.SignalListItemFactory, Gtk.SignalListItemFactory.SetupSignalArgs> OnSetupHandler { get; set; }
			public GObject.SignalHandler<Gtk.SignalListItemFactory, Gtk.SignalListItemFactory.BindSignalArgs> OnBindHandler { get; set; }
			public GObject.SignalHandler<Gtk.SignalListItemFactory, Gtk.SignalListItemFactory.TeardownSignalArgs> OnTeardownHandler { get; set; }

			public ColumnInfo(ListViewColumn column, Gtk.ColumnViewColumn gtkColumn, Gtk.SignalListItemFactory factory)
			{
				Column = column;
				GtkColumn = gtkColumn;
				Factory = factory;
			}

			public void Dispose()
			{
				if (OnSetupHandler != null)
					Factory.OnSetup -= OnSetupHandler;
				if (OnBindHandler != null)
					Factory.OnBind -= OnBindHandler;
				if (OnTeardownHandler != null)
					Factory.OnTeardown -= OnTeardownHandler;
				foreach (var binding in Bindings.Values)
					binding.Dispose();
				Bindings.Clear();
			}
		}

		class HeaderBinding : IDisposable
		{
			public Gtk.Box Container { get; }
			public Gtk.Widget Content { get; set; }
			public ColumnInfo ColumnInfo { get; set; }

			public HeaderBinding(Gtk.Box container)
			{
				Container = container;
			}

			public void Dispose()
			{
				if (Container != null && Content != null)
					Container.Remove(Content);
				Content = null;
				ColumnInfo = null;
			}
		}

		protected class RowBinding : IDisposable
		{
			public Gtk.Widget Container { get; }
			public Gtk.Box ContentBox { get; }
			public Gtk.ListItem ListItem { get; }
			public ColumnInfo ColumnInfo { get; }
			public List<CellBinding> Cells { get; } = new List<CellBinding>();
			public RowContext Context { get; set; }
			public Action DisposeAction { get; set; }

			public RowBinding(Gtk.Widget container, Gtk.ListItem listItem, Gtk.Box contentBox, ColumnInfo columnInfo)
			{
				Container = container;
				ListItem = listItem;
				ContentBox = contentBox;
				ColumnInfo = columnInfo;
			}

			public void Dispose()
			{
				DisposeAction?.Invoke();
				DisposeAction = null;
				foreach (var cell in Cells)
					cell.Dispose();
				Cells.Clear();
			}
		}

		protected class CellBinding : IDisposable
		{
			public CellView View { get; }
			public Gtk.Widget Widget { get; set; }
			public bool IsUpdating { get; set; }

			public CellBinding(CellView view, Gtk.Widget widget)
			{
				View = view;
				Widget = widget;
			}

			public void Dispose()
			{
			}
		}

		protected class RowContext
		{
			public ICellDataSource CellDataSource { get; set; }
			public IListDataSource ListSource { get; set; }
			public ITreeDataSource TreeSource { get; set; }
			public int RowIndex { get; set; } = -1;
			public TreePosition TreePosition { get; set; }
		}
	}

	class Gtk4CellViewBackend : ICanvasCellViewBackend
	{
		ICellViewFrontend frontend;
		ApplicationContext context;
		CellViewStatus status = new CellViewStatus();
		Gtk.Widget currentWidget;
		WidgetEvent enabledEvents;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.frontend = (ICellViewFrontend)frontend;
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is WidgetEvent ev)
				enabledEvents |= ev;
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is WidgetEvent ev)
				enabledEvents &= ~ev;
		}

		public Rectangle CellBounds => status?.CellBounds ?? Rectangle.Zero;

		public Rectangle BackgroundBounds => status?.BackgroundBounds ?? Rectangle.Zero;

		public bool Selected => status != null && status.Selected;

		public bool HasFocus => status != null && status.HasFocus;

		public void QueueDraw()
		{
			currentWidget?.QueueDraw();
		}

		public void QueueResize()
		{
			currentWidget?.QueueResize();
		}

		public bool IsHighlighted => status != null && status.Selected;

		internal ICellViewEventSink LoadData(ICellDataSource dataSource, CellViewStatus status, Gtk.Widget widget)
		{
			this.status = status ?? new CellViewStatus();
			currentWidget = widget;
			if (frontend == null)
				return null;
			return frontend.Load(dataSource);
		}

		internal bool IsEventEnabled(WidgetEvent ev)
		{
			return (enabledEvents & ev) == ev;
		}
	}
}
