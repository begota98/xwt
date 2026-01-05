using System;
using System.Collections.Generic;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class NotebookBackend : WidgetBackend, INotebookBackend
	{
		readonly Gtk.Notebook notebook;
		readonly Dictionary<NotebookTab, Gtk.Widget> tabLabels = new Dictionary<NotebookTab, Gtk.Widget>();
		readonly Dictionary<NotebookTab, Gtk.Widget> tabChildren = new Dictionary<NotebookTab, Gtk.Widget>();
		INotebookEventSink eventSink;

		public NotebookBackend()
		{
			notebook = Gtk.Notebook.New();
			Widget = notebook;
			Widget.Show();
		}

		public void Add(IWidgetBackend widget, NotebookTab tab)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			var label = Gtk.Label.New(tab.Label ?? string.Empty);
			tabLabels[tab] = label;
			tabChildren[tab] = child;
			notebook.AppendPage(child, label);
			WidgetBackend.ApplyChildPlacement(widget);
		}

		public void Remove(IWidgetBackend widget)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			var page = notebook.PageNum(child);
			if (page >= 0)
				notebook.RemovePage(page);
			NotebookTab removeTab = null;
			foreach (var pair in tabChildren) {
				if (pair.Value == child) {
					removeTab = pair.Key;
					break;
				}
			}
			if (removeTab != null) {
				tabChildren.Remove(removeTab);
				tabLabels.Remove(removeTab);
			}
		}

		public void UpdateLabel(NotebookTab tab, string hint)
		{
			if (tabLabels.TryGetValue(tab, out var label) && label is Gtk.Label gtkLabel)
				gtkLabel.Label_ = tab.Label ?? string.Empty;
		}

		public int CurrentTab {
			get { return notebook.Page; }
			set { notebook.SetCurrentPage(value); }
		}

		public NotebookTabOrientation TabOrientation {
			get { return ToOrientation(notebook.TabPos); }
			set { notebook.TabPos = ToGtkPosition(value); }
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (INotebookEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is NotebookEvent ev && ev == NotebookEvent.CurrentTabChanged)
				notebook.OnSwitchPage += HandleSwitchPage;
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is NotebookEvent ev && ev == NotebookEvent.CurrentTabChanged)
				notebook.OnSwitchPage -= HandleSwitchPage;
		}

		void HandleSwitchPage(object sender, Gtk.Notebook.SwitchPageSignalArgs args)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnCurrentTabChanged);
		}

		static Gtk.PositionType ToGtkPosition(NotebookTabOrientation orientation)
		{
			switch (orientation) {
				case NotebookTabOrientation.Left:
					return Gtk.PositionType.Left;
				case NotebookTabOrientation.Right:
					return Gtk.PositionType.Right;
				case NotebookTabOrientation.Bottom:
					return Gtk.PositionType.Bottom;
				default:
					return Gtk.PositionType.Top;
			}
		}

		static NotebookTabOrientation ToOrientation(Gtk.PositionType position)
		{
			switch (position) {
				case Gtk.PositionType.Left:
					return NotebookTabOrientation.Left;
				case Gtk.PositionType.Right:
					return NotebookTabOrientation.Right;
				case Gtk.PositionType.Bottom:
					return NotebookTabOrientation.Bottom;
				default:
					return NotebookTabOrientation.Top;
			}
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}
	}
}
