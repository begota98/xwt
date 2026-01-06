using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class SeparatorMenuItemBackend : ISeparatorMenuItemBackend, IGtkMenuItemBackend
	{
		MenuBackend menuHost;
		string label = string.Empty;
		string tooltip = string.Empty;
		bool useMnemonic;
		bool sensitive = true;
		bool visible = true;

		public bool IsSeparator => true;

		public bool IsVisible => visible;

		public void Attach(MenuBackend menu)
		{
			menuHost = menu;
		}

		public void Detach(MenuBackend menu)
		{
			if (menuHost == menu)
				menuHost = null;
		}

		public Gio.MenuItem BuildMenuItem(string actionGroupName)
		{
			return null;
		}

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

		public string Label {
			get { return label ?? string.Empty; }
			set { label = value ?? string.Empty; }
		}

		public string TooltipText {
			get { return tooltip ?? string.Empty; }
			set { tooltip = value ?? string.Empty; }
		}

		public bool UseMnemonic {
			get { return useMnemonic; }
			set { useMnemonic = value; }
		}

		public bool Sensitive {
			get { return sensitive; }
			set { sensitive = value; }
		}

		public bool Visible {
			get { return visible; }
			set {
				visible = value;
				NotifyMenuChanged();
			}
		}

		public void SetFormattedText(FormattedText text)
		{
		}

		public void Dispose()
		{
			menuHost = null;
		}

		void NotifyMenuChanged()
		{
			menuHost?.Invalidate();
		}
	}
}
