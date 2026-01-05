using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ProgressBarBackend : WidgetBackend, IProgressBarBackend
	{
		readonly Gtk.ProgressBar progressBar;
		bool isIndeterminate;
		uint? timerId;

		public ProgressBarBackend()
		{
			progressBar = Gtk.ProgressBar.New();
			Widget = progressBar;
			Widget.Show();
		}

		public void SetFraction(double fraction)
		{
			if (isIndeterminate)
				return;
			if (fraction < 0)
				fraction = 0;
			if (fraction > 1)
				fraction = 1;
			progressBar.Fraction = fraction;
		}

		public void SetIndeterminate(bool indeterminate)
		{
			isIndeterminate = indeterminate;
			if (isIndeterminate) {
				if (timerId != null)
					return;
				timerId = GLib.Functions.TimeoutAdd(GLib.Constants.PRIORITY_DEFAULT, 100, Pulse);
			} else {
				DisposeTimeout();
			}
		}

		bool Pulse()
		{
			progressBar.Pulse();
			return true;
		}

		void DisposeTimeout()
		{
			if (timerId != null) {
				GLib.Functions.SourceRemove(timerId.Value);
				timerId = null;
			}
		}

		public override void Dispose()
		{
			DisposeTimeout();
			base.Dispose();
		}
	}
}
