using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class UtilityWindowBackend : WindowBackend, IUtilityWindowBackend
	{
		public override void Initialize()
		{
			base.Initialize();
			if (Window != null) {
				Window.HideOnClose = true;
			}
		}
	}
}
