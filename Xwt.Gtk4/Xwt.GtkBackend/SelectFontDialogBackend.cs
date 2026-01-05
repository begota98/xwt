using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class SelectFontDialogBackend : ISelectFontDialogBackend
	{
		ApplicationContext context;
		Font selectedFont = Font.SystemFont;
		string title;
		string previewText;

		public void Initialize(ApplicationContext context)
		{
			this.context = context;
		}

		public string Title {
			get { return title ?? string.Empty; }
			set { title = value ?? string.Empty; }
		}

		public Font SelectedFont {
			get { return selectedFont; }
			set { selectedFont = value; }
		}

		public string PreviewText {
			get { return previewText ?? string.Empty; }
			set { previewText = value ?? string.Empty; }
		}

		public bool Run(IWindowFrameBackend parent)
		{
			var toolkit = context?.Toolkit ?? Toolkit.CurrentEngine;
			var parentWindow = parent != null ? toolkit.GetNativeWindow(parent) as Gtk.Window : null;
			var dialog = Gtk.FontChooserDialog.New(Title, parentWindow);
			dialog.AddButton("Cancel", (int)Gtk.ResponseType.Cancel);
			dialog.AddButton("Select", (int)Gtk.ResponseType.Ok);
			dialog.Font = selectedFont.ToString();
			dialog.PreviewText = PreviewText;

			bool result = GtkDialogHelper.RunDialog(dialog, parentWindow, out _);
			if (result)
				selectedFont = Font.FromName(dialog.Font ?? string.Empty);
			return result;
		}

		public void Dispose()
		{
		}
	}
}
