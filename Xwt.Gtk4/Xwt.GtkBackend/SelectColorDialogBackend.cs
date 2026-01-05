using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class SelectColorDialogBackend : ISelectColorDialogBackend
	{
		Color color = Colors.Transparent;

		public Color Color {
			get { return color; }
			set { color = value; }
		}

		public bool Run(IWindowFrameBackend parent, string title, bool supportsAlpha)
		{
			var parentWindow = parent != null ? Toolkit.CurrentEngine.GetNativeWindow(parent) as Gtk.Window : null;
			var dialog = Gtk.ColorChooserDialog.New(title ?? string.Empty, parentWindow);
			dialog.AddButton("Cancel", (int)Gtk.ResponseType.Cancel);
			dialog.AddButton("Select", (int)Gtk.ResponseType.Ok);
			dialog.UseAlpha = supportsAlpha;
			if (color != Colors.Transparent)
				dialog.Rgba = color.ToGtkValue();

			bool result = GtkDialogHelper.RunDialog(dialog, parentWindow, out _);
			if (result)
				color = dialog.Rgba.ToXwtValue();
			return result;
		}

		public void Dispose()
		{
		}
	}
}
