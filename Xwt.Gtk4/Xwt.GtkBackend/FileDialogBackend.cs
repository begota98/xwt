using System;
using System.Collections.Generic;
using System.Linq;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	internal enum FileDialogAction
	{
		Open,
		Save
	}

	public class FileDialogBackend : IFileDialogBackend
	{
		readonly FileDialogAction action;
		ApplicationContext context;
		Gtk.FileDialog dialog;
		List<FileDialogFilter> filters = new List<FileDialogFilter>();
		List<Gtk.FileFilter> gtkFilters = new List<Gtk.FileFilter>();
		bool multiselect;
		string initialFileName;
		string title;
		string currentFolder;
		FileDialogFilter activeFilter;
		string fileName;
		string[] fileNames = Array.Empty<string>();

		internal FileDialogBackend(FileDialogAction action)
		{
			this.action = action;
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
		}

		public void DisableEvent(object eventId)
		{
		}

		public void Initialize(IEnumerable<FileDialogFilter> filters, bool multiselect, string initialFileName)
		{
			this.multiselect = multiselect;
			this.initialFileName = initialFileName ?? string.Empty;
			this.filters = filters?.ToList() ?? new List<FileDialogFilter>();
			dialog = Gtk.FileDialog.New();
			dialog.Modal = true;
			if (!string.IsNullOrEmpty(title))
				dialog.Title = title;
			if (!string.IsNullOrEmpty(this.initialFileName))
				dialog.SetInitialName(this.initialFileName);
			if (!string.IsNullOrEmpty(currentFolder))
				dialog.SetInitialFolder(Gio.Functions.FileNewForPath(currentFolder));

			gtkFilters.Clear();
			if (this.filters.Count > 0) {
				var listStore = Gio.ListStore.New(Gtk.FileFilter.GetGType());
				foreach (var filter in this.filters) {
					var gtkFilter = new Gtk.FileFilter();
					gtkFilters.Add(gtkFilter);
					if (!string.IsNullOrEmpty(filter.Name))
						gtkFilter.Name = filter.Name;
					if (filter.Patterns != null) {
						foreach (var pattern in filter.Patterns)
							gtkFilter.AddPattern(pattern);
					}
					listStore.Append(gtkFilter);
				}
				dialog.SetFilters(listStore);
				if (activeFilter != null)
					dialog.SetDefaultFilter(GetGtkFilter(activeFilter));
				else
					dialog.SetDefaultFilter(gtkFilters[0]);
			}
		}

		public string Title {
			get { return title ?? string.Empty; }
			set {
				title = value ?? string.Empty;
				if (dialog != null)
					dialog.Title = title;
			}
		}

		public FileDialogFilter ActiveFilter {
			get {
				if (dialog == null)
					return activeFilter;
				var current = dialog.DefaultFilter;
				var index = gtkFilters.IndexOf(current);
				if (index >= 0 && index < filters.Count)
					return filters[index];
				return activeFilter;
			}
			set {
				activeFilter = value;
				if (dialog != null && value != null)
					dialog.SetDefaultFilter(GetGtkFilter(value));
			}
		}

		public string FileName => fileName;

		public string[] FileNames => fileNames;

		public string CurrentFolder {
			get { return currentFolder; }
			set {
				currentFolder = value;
				if (dialog != null && !string.IsNullOrEmpty(value))
					dialog.SetInitialFolder(Gio.Functions.FileNewForPath(value));
			}
		}

		public bool Run(IWindowFrameBackend parent)
		{
			var parentWindow = parent != null ? context.Toolkit.GetNativeWindow(parent) as Gtk.Window : null;
			fileName = null;
			fileNames = Array.Empty<string>();

			if (dialog == null)
				return false;

			try {
				if (multiselect && action == FileDialogAction.Open) {
					var model = GtkDialogHelper.RunTask(dialog.OpenMultipleAsync(parentWindow));
					var paths = ExtractPaths(model);
					fileNames = paths;
					fileName = fileNames.FirstOrDefault();
				} else {
					Gio.File file;
					if (action == FileDialogAction.Save)
						file = GtkDialogHelper.RunTask(dialog.SaveAsync(parentWindow));
					else
						file = GtkDialogHelper.RunTask(dialog.OpenAsync(parentWindow));

					fileName = GetFilePath(file);
					fileNames = string.IsNullOrEmpty(fileName) ? Array.Empty<string>() : new[] { fileName };
				}
			} catch {
				fileName = null;
				fileNames = Array.Empty<string>();
			}

			if (fileNames.Length > 0 && !string.IsNullOrEmpty(fileNames[0]))
				currentFolder = System.IO.Path.GetDirectoryName(fileNames[0]);

			return fileNames.Length > 0;
		}

		public void Cleanup()
		{
			dialog = null;
			gtkFilters.Clear();
			filters.Clear();
		}

		Gtk.FileFilter GetGtkFilter(FileDialogFilter filter)
		{
			var index = filters.IndexOf(filter);
			if (index >= 0 && index < gtkFilters.Count)
				return gtkFilters[index];
			return gtkFilters.Count > 0 ? gtkFilters[0] : null;
		}

		static string GetFilePath(Gio.File file)
		{
			if (file == null)
				return null;
			var path = file.GetPath();
			return string.IsNullOrEmpty(path) ? file.GetUri() : path;
		}

		static string[] ExtractPaths(Gio.ListModel model)
		{
			if (model == null)
				return Array.Empty<string>();
			var result = new List<string>();
			var count = model.GetNItems();
			for (uint i = 0; i < count; i++) {
				var item = model.GetItem(i);
				if (item == IntPtr.Zero)
					continue;
				var handle = new GObject.Internal.ObjectHandle(item, false);
				var file = new Gio.FileHelper(handle);
				var path = GetFilePath(file);
				if (!string.IsNullOrEmpty(path))
					result.Add(path);
			}
			return result.ToArray();
		}
	}

	public class OpenFileDialogBackend : FileDialogBackend, IOpenFileDialogBackend
	{
		public OpenFileDialogBackend() : base(FileDialogAction.Open)
		{
		}
	}

	public class SaveFileDialogBackend : FileDialogBackend, ISaveFileDialogBackend
	{
		public SaveFileDialogBackend() : base(FileDialogAction.Save)
		{
		}
	}
}
