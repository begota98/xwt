using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SpinnerBackend : WidgetBackend, ISpinnerBackend
	{
		readonly Gtk.Spinner spinner;

		public SpinnerBackend()
		{
			spinner = Gtk.Spinner.New();
			Widget = spinner;
			Widget.Show();
		}

		public void StartAnimation()
		{
			spinner.Start();
		}

		public void StopAnimation()
		{
			spinner.Stop();
		}

		public bool IsAnimating => spinner.Spinning;
	}
}
