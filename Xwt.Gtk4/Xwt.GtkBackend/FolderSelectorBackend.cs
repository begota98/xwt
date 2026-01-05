using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class FolderSelectorBackend : WidgetBackend, IFolderSelectorBackend
	{
		Gtk.Box box;
		Gtk.Entry entry;
		Gtk.Button button;
		string currentFolder;
		string title;
		bool canCreateFolders;
		bool folderChangedEnabled;

		public FolderSelectorBackend()
		{
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

		protected new IFolderSelectorEventSink EventSink => (IFolderSelectorEventSink)base.EventSink;

		public string CurrentFolder {
			get { return currentFolder; }
			set { currentFolder = value; }
		}

		public string Folder {
			get { return entry?.Text_ ?? string.Empty; }
			set {
				if (entry != null)
					entry.Text_ = value ?? string.Empty;
			}
		}

		public string Title {
			get { return title ?? string.Empty; }
			set { title = value ?? string.Empty; }
		}

		public bool CanCreateFolders {
			get { return canCreateFolders; }
			set { canCreateFolders = value; }
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is FolderSelectorEvent ev && ev == FolderSelectorEvent.FolderChanged)
				folderChangedEnabled = true;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is FolderSelectorEvent ev && ev == FolderSelectorEvent.FolderChanged)
				folderChangedEnabled = false;
		}

		void HandleEntryChanged(object sender, EventArgs e)
		{
			if (folderChangedEnabled)
				ApplicationContext.InvokeUserCode(EventSink.OnFolderChanged);
		}

		void HandleButtonClicked(Gtk.Button sender, EventArgs e)
		{
			var dialog = Gtk.FileDialog.New();
			dialog.Modal = true;
			dialog.Title = title ?? string.Empty;

			if (!string.IsNullOrEmpty(currentFolder))
				dialog.SetInitialFolder(Gio.Functions.FileNewForPath(currentFolder));

			var parentWindow = box.GetRoot() as Gtk.Window;
			var file = GtkDialogHelper.RunTask(dialog.SelectFolderAsync(parentWindow));
			var path = GetFilePath(file);
			if (!string.IsNullOrEmpty(path)) {
				entry.Text_ = path;
				currentFolder = path;
			}
		}

		static string GetFilePath(Gio.File file)
		{
			if (file == null)
				return null;
			var path = file.GetPath();
			return string.IsNullOrEmpty(path) ? file.GetUri() : path;
		}
	}
}
