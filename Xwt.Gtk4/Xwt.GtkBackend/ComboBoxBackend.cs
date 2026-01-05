using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class ComboBoxBackend : WidgetBackend, IComboBoxBackend
	{
		readonly Gtk.DropDown dropDown;
		Gtk.SignalListItemFactory factory;
		Gtk.SignalListItemFactory listFactory;
		IListDataSource source;
		IComboBoxEventSink eventSink;
		Gio.ListStore listStore;
		readonly List<GObject.Object> rowObjects = new List<GObject.Object>();
		readonly Dictionary<Gtk.ListItem, RowBinding> bindings = new Dictionary<Gtk.ListItem, RowBinding>();
		readonly Dictionary<Gtk.ListItem, RowBinding> listBindings = new Dictionary<Gtk.ListItem, RowBinding>();
		List<CellView> views = new List<CellView>();

		public ComboBoxBackend()
		{
			dropDown = Gtk.DropDown.New(null, null);
			Widget = dropDown;
			Widget.Show();
			SetupFactories();
		}

		public void SetViews(CellViewCollection views)
		{
			this.views = views != null ? new List<CellView>(views) : new List<CellView>();
			SetupFactories();
			RefreshList();
		}

		public void SetSource(IListDataSource source, IBackend sourceBackend)
		{
			DetachSource();
			this.source = source;
			BuildModel();
			AttachSource();
		}

		public int SelectedRow {
			get {
				if (dropDown.Selected == Gtk.Constants.INVALID_LIST_POSITION)
					return -1;
				return (int)dropDown.Selected;
			}
			set {
				if (value < 0)
					dropDown.Selected = Gtk.Constants.INVALID_LIST_POSITION;
				else
					dropDown.Selected = (uint)value;
			}
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (IComboBoxEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is ComboBoxEvent ev && ev == ComboBoxEvent.SelectionChanged)
				dropDown.OnNotify += HandleNotify;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is ComboBoxEvent ev && ev == ComboBoxEvent.SelectionChanged)
				dropDown.OnNotify -= HandleNotify;
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null || args.Pspec.GetName() != "selected")
				return;
			ApplicationContext.InvokeUserCode(eventSink.OnSelectionChanged);
		}

		void SetupFactories()
		{
			bindings.Clear();
			listBindings.Clear();
			factory = Gtk.SignalListItemFactory.New();
			listFactory = Gtk.SignalListItemFactory.New();

			factory.OnSetup += (sender, args) => SetupListItem(args, bindings, false);
			factory.OnBind += (sender, args) => BindListItem(args, bindings, false);
			factory.OnTeardown += (sender, args) => TeardownListItem(args, bindings);

			listFactory.OnSetup += (sender, args) => SetupListItem(args, listBindings, true);
			listFactory.OnBind += (sender, args) => BindListItem(args, listBindings, true);
			listFactory.OnTeardown += (sender, args) => TeardownListItem(args, listBindings);

			dropDown.Factory = factory;
			dropDown.ListFactory = listFactory;
		}

		void SetupListItem(Gtk.SignalListItemFactory.SetupSignalArgs args, Dictionary<Gtk.ListItem, RowBinding> map, bool allowSeparator)
		{
			if (args.Object == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;

			var binding = new RowBinding(allowSeparator);
			if (views.Count == 0) {
				binding.DefaultLabel = Gtk.Label.New(string.Empty);
				binding.DefaultLabel.Xalign = 0f;
				binding.ContentBox.Append(binding.DefaultLabel);
			} else {
				foreach (var view in views) {
					if (view == null)
						continue;
					var cellBinding = CreateCellBinding(view);
					binding.Cells.Add(cellBinding);
					binding.ContentBox.Append(cellBinding.Widget);
				}
			}

			listItem.SetChild(binding.Container);
			map[listItem] = binding;
		}

		void BindListItem(Gtk.SignalListItemFactory.BindSignalArgs args, Dictionary<Gtk.ListItem, RowBinding> map, bool allowSeparator)
		{
			if (args.Object == null || source == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;
			if (!map.TryGetValue(listItem, out var binding))
				return;

			var row = (int)listItem.Position;
			var context = new RowContext(source, row);

			bool isSeparator = false;
			if (allowSeparator && eventSink != null) {
				ApplicationContext.InvokeUserCode(() => {
					isSeparator = eventSink.RowIsSeparator(row);
				});
			}

			if (allowSeparator && isSeparator) {
				binding.SetSeparator(true);
				listItem.Selectable = false;
				listItem.Activatable = false;
				return;
			}

			binding.SetSeparator(false);
			listItem.Selectable = true;
			listItem.Activatable = true;

			if (binding.DefaultLabel != null) {
				var text = source.GetValue(row, 0)?.ToString() ?? string.Empty;
				binding.DefaultLabel.Label_ = text;
				binding.DefaultLabel.Hexpand = true;
				return;
			}

			foreach (var cell in binding.Cells) {
				var viewFrontend = (ICellViewFrontend)cell.View;
				viewFrontend.Load(context.DataSource);
				UpdateCellWidget(cell, viewFrontend, context);
				cell.Widget.Hexpand = viewFrontend.Expands;
			}
		}

		void TeardownListItem(Gtk.SignalListItemFactory.TeardownSignalArgs args, Dictionary<Gtk.ListItem, RowBinding> map)
		{
			if (args.Object == null)
				return;
			var listItem = (Gtk.ListItem)args.Object;
			if (map.TryGetValue(listItem, out var binding)) {
				binding.Dispose();
				map.Remove(listItem);
			}
		}

		CellBinding CreateCellBinding(CellView view)
		{
			var viewFrontend = (ICellViewFrontend)view;
			Gtk.Widget widget;

			if (viewFrontend is ITextCellViewFrontend) {
				var label = Gtk.Label.New(string.Empty);
				label.Xalign = 0f;
				label.Yalign = 0.5f;
				label.Halign = Gtk.Align.Start;
				label.Valign = Gtk.Align.Center;
				widget = label;
			} else if (viewFrontend is ICanvasCellViewFrontend canvasView) {
				var area = Gtk.DrawingArea.New();
				area.Halign = Gtk.Align.Fill;
				area.Valign = Gtk.Align.Fill;
				var cellBinding = new CellBinding(view, area);
				area.SetDrawFunc((drawingArea, cr, width, height) => DrawCanvasCell(canvasView, cellBinding, context: null, drawingArea, cr, width, height));
				area.Show();
				return cellBinding;
			} else if (viewFrontend is ICheckBoxCellViewFrontend || viewFrontend is IRadioButtonCellViewFrontend) {
				var check = Gtk.CheckButton.New();
				check.Sensitive = false;
				widget = check;
			} else if (viewFrontend is IImageCellViewFrontend) {
				var picture = Gtk.Picture.New();
				picture.SetKeepAspectRatio(true);
				picture.SetCanShrink(false);
				picture.Halign = Gtk.Align.Center;
				picture.Valign = Gtk.Align.Center;
				widget = picture;
			} else {
				widget = Gtk.Label.New(string.Empty);
			}

			widget.Show();
			return new CellBinding(view, widget);
		}

		void UpdateCellWidget(CellBinding cell, ICellViewFrontend viewFrontend, RowContext context)
		{
			if (viewFrontend is ITextCellViewFrontend textView && cell.Widget is Gtk.Label label) {
				if (!string.IsNullOrEmpty(textView.Markup)) {
					var formatted = FormattedText.FromMarkup(textView.Markup);
					FormattedTextUtil.ApplyFormattedText(label, formatted);
				} else {
					label.UseMarkup = false;
					label.Label_ = textView.Text ?? string.Empty;
					label.SetAttributes(null);
				}
				label.Ellipsize = (Pango.EllipsizeMode)(int)textView.Ellipsize;
				label.Visible = viewFrontend.Visible;
				return;
			}

			if ((viewFrontend is ICheckBoxCellViewFrontend || viewFrontend is IRadioButtonCellViewFrontend) && cell.Widget is Gtk.CheckButton check) {
				if (viewFrontend is ICheckBoxCellViewFrontend checkView) {
					check.Inconsistent = checkView.State == CheckBoxState.Mixed;
					check.Active = checkView.State == CheckBoxState.On;
				} else if (viewFrontend is IRadioButtonCellViewFrontend radioView) {
					check.Active = radioView.Active;
				}
				check.Visible = viewFrontend.Visible;
				return;
			}

			if (viewFrontend is IImageCellViewFrontend imageView && cell.Widget is Gtk.Picture picture) {
				Image image = imageView.Image;
				var desc = image.ToImageDescription(ApplicationContext);
				var gtkImage = desc.Backend as GtkImage;
				picture.SetPixbuf(gtkImage?.Pixbuf);
				picture.Visible = viewFrontend.Visible;
				return;
			}

			if (viewFrontend is ICanvasCellViewFrontend canvasView && cell.Widget is Gtk.DrawingArea area) {
				var size = canvasView.GetRequiredSize(SizeConstraint.Unconstrained);
				if (!size.IsZero)
					area.SetSizeRequest((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
				area.Visible = viewFrontend.Visible;
				area.QueueDraw();
			}
		}

		void DrawCanvasCell(ICanvasCellViewFrontend canvasView, CellBinding cell, RowContext context, Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
		{
			var rect = new Rectangle(0, 0, width, height);
			var backend = new Xwt.CairoBackend.CairoContextBackend(area.ScaleFactor) {
				Context = cr
			};
			canvasView.Draw(backend, rect);
		}

		void BuildModel()
		{
			listStore = Gio.ListStore.New(GObject.Object.GetGType());
			rowObjects.Clear();
			if (source != null) {
				for (int i = 0; i < source.RowCount; i++) {
					var obj = CreateListObject();
					listStore.Append(obj);
					rowObjects.Add(obj);
				}
			}
			dropDown.SetModel(listStore);
			RefreshList();
		}

		void RefreshList()
		{
			if (listStore != null)
				listStore.ItemsChanged(0, 0, 0);
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
			if (e.Row >= 0 && e.Row <= rowObjects.Count)
				rowObjects.Insert(e.Row, obj);
			else
				rowObjects.Add(obj);
		}

		void HandleRowDeleted(object sender, ListRowEventArgs e)
		{
			if (listStore == null)
				return;
			listStore.Remove((uint)e.Row);
			if (e.Row >= 0 && e.Row < rowObjects.Count)
				rowObjects.RemoveAt(e.Row);
		}

		void HandleRowChanged(object sender, ListRowEventArgs e)
		{
			listStore?.ItemsChanged((uint)e.Row, 1, 1);
		}

		void HandleRowsReordered(object sender, ListRowOrderEventArgs e)
		{
			BuildModel();
		}

		static GObject.Object CreateListObject()
		{
			return (GObject.Object)GObject.Object.Newv(GObject.Object.GetGType(), Array.Empty<GObject.Parameter>());
		}

		class RowContext
		{
			public RowContext(IListDataSource source, int rowIndex)
			{
				RowIndex = rowIndex;
				DataSource = source != null ? new ComboCellDataSource(source, rowIndex) : null;
			}

			public int RowIndex { get; }
			public ICellDataSource DataSource { get; }
		}

		class ComboCellDataSource : ICellDataSource
		{
			readonly IListDataSource source;
			readonly int row;

			public ComboCellDataSource(IListDataSource source, int row)
			{
				this.source = source;
				this.row = row;
			}

			public object GetValue(IDataField field)
			{
				if (source == null || field == null)
					return null;
				return source.GetValue(row, field.Index);
			}
		}

		class RowBinding : IDisposable
		{
			public RowBinding(bool allowSeparator)
			{
				ContentBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
				ContentBox.Hexpand = true;
				ContentBox.Valign = Gtk.Align.Center;

				if (allowSeparator) {
					Separator = Gtk.Separator.New(Gtk.Orientation.Horizontal);
					Separator.Hexpand = true;
					Separator.Visible = false;
					var box = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
					box.Hexpand = true;
					box.Append(Separator);
					box.Append(ContentBox);
					Container = box;
				} else {
					Container = ContentBox;
				}

				Container.Show();
				ContentBox.Show();
				Separator?.Show();
			}

			public Gtk.Widget Container { get; }
			public Gtk.Box ContentBox { get; }
			public Gtk.Separator Separator { get; }
			public Gtk.Label DefaultLabel { get; set; }
			public List<CellBinding> Cells { get; } = new List<CellBinding>();

			public void SetSeparator(bool isSeparator)
			{
				if (Separator == null)
					return;
				Separator.Visible = isSeparator;
				ContentBox.Visible = !isSeparator;
			}

			public void Dispose()
			{
				foreach (var cell in Cells)
					cell.Dispose();
				Cells.Clear();
			}
		}

		class CellBinding : IDisposable
		{
			public CellBinding(CellView view, Gtk.Widget widget)
			{
				View = view;
				Widget = widget;
			}

			public CellView View { get; }
			public Gtk.Widget Widget { get; }

			public void Dispose()
			{
				Widget?.Unparent();
			}
		}
	}
}
