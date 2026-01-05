using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class GtkKeyboardHandler : KeyboardHandler
	{
		public override ModifierKeys CurrentModifiers {
			get {
				var display = Gdk.Display.GetDefault();
				var seat = display?.GetDefaultSeat();
				var device = seat?.GetKeyboard() ?? seat?.GetPointer();
				return device?.ModifierState.ToXwtValue() ?? ModifierKeys.None;
			}
		}
	}
}
