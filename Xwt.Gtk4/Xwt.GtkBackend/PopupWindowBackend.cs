using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class PopupWindowBackend : WindowBackend, IPopupWindowBackend
	{
		PopupWindow.PopupType windowType;

		public override void Initialize()
		{
			base.Initialize();
			if (Window != null) {
				Window.Decorated = false;
				Window.HideOnClose = true;
				if (windowType == PopupWindow.PopupType.Tooltip)
					Window.Resizable = false;
			}
		}

		void IPopupWindowBackend.Initialize(IWindowFrameEventSink sink, PopupWindow.PopupType type)
		{
			windowType = type;
			Initialize(sink);
		}
	}
}
