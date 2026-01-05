using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class SeparatorMenuItemBackend : ISeparatorMenuItemBackend, IGtkMenuItemBackend
	{
		readonly Gtk.Separator separator;

		public SeparatorMenuItemBackend()
		{
			separator = Gtk.Separator.New(Gtk.Orientation.Horizontal);
			separator.Show();
		}

		public Gtk.Widget Widget => separator;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
		}

		public void EnableEvent(object eventId)
		{
		}

		public void DisableEvent(object eventId)
		{
		}

		public void Initialize(IMenuItemEventSink eventSink)
		{
		}

		public void SetSubmenu(IMenuBackend menu)
		{
		}

		public void SetImage(ImageDescription image)
		{
		}

		public string Label { get; set; }

		public string TooltipText { get; set; }

		public bool UseMnemonic { get; set; }

		public bool Sensitive {
			get { return separator.Sensitive; }
			set { separator.Sensitive = value; }
		}

		public bool Visible {
			get { return separator.Visible; }
			set { separator.Visible = value; }
		}

		public void SetFormattedText(FormattedText text)
		{
		}

		public void Dispose()
		{
			separator?.Dispose();
		}
	}
}
