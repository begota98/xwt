using System;
using System.Linq;
using System.Reflection;
using Xwt;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class FileSelectorBackend : WidgetBackend, IFileSelectorBackend
	{
		Gtk.Box box;
		Gtk.Entry entry;
		Gtk.Button button;
		FileDialogFilterCollection filters;
		FileDialogFilter activeFilter;
		string currentFolder;
		FileSelectionMode selectionMode;
		string title;
		bool fileChangedEnabled;

		public FileSelectorBackend()
		{
			filters = CreateFilters();

			box = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
			entry = Gtk.Entry.New();
			entry.Hexpand = true;
			entry.OnChanged += HandleEntryChanged;
			button = Gtk.Button.NewWithLabel("...");
			button.OnClicked += HandleButtonClicked;

			box.Append(entry);
			box.Append(button);
			Widget = box;
			Widget.Show();
			entry.Show();
			button.Show();
		}

		protected new IFileSelectorEventSink EventSink => (IFileSelectorEventSink)base.EventSink;

		public string CurrentFolder {
			get { return currentFolder; }
			set { currentFolder = value; }
		}

		public FileDialogFilterCollection Filters => filters;

		public FileDialogFilter ActiveFilter {
			get { return activeFilter; }
			set {
				if (value != null && !filters.Contains(value))
					throw new ArgumentException("The active filter must be in the Filters collection");
				activeFilter = value;
			}
		}

		public string FileName {
			get { return entry?.Text_ ?? string.Empty; }
			set {
				if (entry != null)
					entry.Text_ = value ?? string.Empty;
			}
		}

		public FileSelectionMode FileSelectionMode {
			get { return selectionMode; }
			set { selectionMode = value; }
		}

		public string Title {
			get { return title ?? string.Empty; }
			set { title = value ?? string.Empty; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is FileSelectorEvent ev && ev == FileSelectorEvent.FileChanged)
				fileChangedEnabled = true;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is FileSelectorEvent ev && ev == FileSelectorEvent.FileChanged)
				fileChangedEnabled = false;
		}

		void HandleEntryChanged(object sender, EventArgs e)
		{
			if (fileChangedEnabled)
				ApplicationContext.InvokeUserCode(EventSink.OnFileChanged);
		}

		void HandleButtonClicked(Gtk.Button sender, EventArgs e)
		{
			var dialog = Gtk.FileDialog.New();
			dialog.Modal = true;
			dialog.Title = title ?? string.Empty;

			var currentText = entry.Text_ ?? string.Empty;
			if (!string.IsNullOrEmpty(currentFolder))
				dialog.SetInitialFolder(Gio.Functions.FileNewForPath(currentFolder));
			else if (!string.IsNullOrEmpty(currentText)) {
				try {
					var dir = System.IO.Path.GetDirectoryName(currentText);
					if (!string.IsNullOrEmpty(dir))
						dialog.SetInitialFolder(Gio.Functions.FileNewForPath(dir));
				} catch {
				}
			}

			if (filters.Count > 0) {
				var listStore = Gio.ListStore.New(Gtk.FileFilter.GetGType());
				foreach (var filter in filters) {
					var gtkFilter = new Gtk.FileFilter();
					if (!string.IsNullOrEmpty(filter.Name))
						gtkFilter.Name = filter.Name;
					if (filter.Patterns != null) {
						foreach (var pattern in filter.Patterns)
							gtkFilter.AddPattern(pattern);
					}
					listStore.Append(gtkFilter);
				}
				dialog.SetFilters(listStore);
				if (activeFilter != null) {
					var index = filters.IndexOf(activeFilter);
					if (index >= 0) {
						var gtkFilter = (Gtk.FileFilter)listStore.GetObject((uint)index);
						dialog.SetDefaultFilter(gtkFilter);
					}
				}
			}

			var parentWindow = box.GetRoot() as Gtk.Window;
			Gio.File file = null;
			if (selectionMode == FileSelectionMode.Save)
				file = GtkDialogHelper.RunTask(dialog.SaveAsync(parentWindow));
			else
				file = GtkDialogHelper.RunTask(dialog.OpenAsync(parentWindow));

			var path = GetFilePath(file);
			if (!string.IsNullOrEmpty(path)) {
				entry.Text_ = path;
				currentFolder = System.IO.Path.GetDirectoryName(path);
			}
		}

		static string GetFilePath(Gio.File file)
		{
			if (file == null)
				return null;
			var path = file.GetPath();
			return string.IsNullOrEmpty(path) ? file.GetUri() : path;
		}

		static FileDialogFilterCollection CreateFilters()
		{
			var ctor = typeof(FileDialogFilterCollection).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new[] { typeof(Action<FileDialogFilter, bool>) },
				null);
			return (FileDialogFilterCollection)ctor.Invoke(new object[] { null });
		}
	}
}
