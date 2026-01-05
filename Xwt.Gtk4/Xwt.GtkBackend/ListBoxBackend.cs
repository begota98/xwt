using System;
using Xwt;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ListBoxBackend : ListViewBackend, IListBoxBackend
	{
		ListViewColumn column;
		object columnHandle;
		Gtk.SignalListItemFactory headerFactory;

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			if (column == null) {
				column = new ListViewColumn(string.Empty) {
					Expands = true
				};
				columnHandle = AddColumn(column);
				SetupHeaderFactory();
			}
		}

		public new bool GridLinesVisible {
			get {
				return base.GridLinesVisible == GridLines.Horizontal || base.GridLinesVisible == GridLines.Both;
			}
			set {
				base.GridLinesVisible = value ? GridLines.Horizontal : GridLines.None;
			}
		}

		public void SetViews(CellViewCollection views)
		{
			if (column == null)
				return;
			column.Views.Clear();
			foreach (var view in views)
				column.Views.Add(view);
			if (columnHandle != null)
				UpdateColumn(column, columnHandle, ListViewColumnChange.Cells);
		}

		void SetupHeaderFactory()
		{
			if (headerFactory != null)
				return;
			headerFactory = Gtk.SignalListItemFactory.New();
			headerFactory.OnSetup += (sender, args) => {
				if (args.Object is Gtk.ListItem listItem) {
					var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
					box.Visible = false;
					box.HeightRequest = 0;
					listItem.SetChild(box);
				}
			};
			ColumnView.HeaderFactory = headerFactory;
		}
	}
}
