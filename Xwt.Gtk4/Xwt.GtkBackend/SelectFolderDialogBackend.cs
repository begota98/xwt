using System;
using System.Collections.Generic;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SelectFolderDialogBackend : ISelectFolderDialogBackend
	{
		ApplicationContext context;
		Gtk.FileDialog dialog;
		bool multiselect;
		string title;
		string currentFolder;
		bool canCreateFolders;
		string folder;
		string[] folders = Array.Empty<string>();

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

		public void Initialize(bool multiselect)
		{
			this.multiselect = multiselect;
			dialog = Gtk.FileDialog.New();
			dialog.Modal = true;
			if (!string.IsNullOrEmpty(title))
				dialog.Title = title;
			if (!string.IsNullOrEmpty(currentFolder))
				dialog.SetInitialFolder(Gio.Functions.FileNewForPath(currentFolder));
		}

		public string Title {
			get { return title ?? string.Empty; }
			set {
				title = value ?? string.Empty;
				if (dialog != null)
					dialog.Title = title;
			}
		}

		public string Folder => folder;

		public string[] Folders => folders;

		public string CurrentFolder {
			get { return currentFolder; }
			set {
				currentFolder = value;
				if (dialog != null && !string.IsNullOrEmpty(value))
					dialog.SetInitialFolder(Gio.Functions.FileNewForPath(value));
			}
		}

		public bool CanCreateFolders {
			get { return canCreateFolders; }
			set { canCreateFolders = value; }
		}

		public bool Run(IWindowFrameBackend parent)
		{
			var parentWindow = parent != null ? context.Toolkit.GetNativeWindow(parent) as Gtk.Window : null;
			folder = null;
			folders = Array.Empty<string>();

			if (dialog == null)
				return false;

			try {
				if (multiselect) {
					var model = GtkDialogHelper.RunTask(dialog.SelectMultipleFoldersAsync(parentWindow));
					folders = ExtractPaths(model);
					folder = folders.Length > 0 ? folders[0] : null;
				} else {
					var file = GtkDialogHelper.RunTask(dialog.SelectFolderAsync(parentWindow));
					folder = GetFilePath(file);
					folders = string.IsNullOrEmpty(folder) ? Array.Empty<string>() : new[] { folder };
				}
			} catch {
				folder = null;
				folders = Array.Empty<string>();
			}

			if (!string.IsNullOrEmpty(folder))
				currentFolder = folder;

			return folders.Length > 0;
		}

		public void Cleanup()
		{
			dialog = null;
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
}
