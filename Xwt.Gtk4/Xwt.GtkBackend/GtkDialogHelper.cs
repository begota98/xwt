using System.Threading.Tasks;

namespace Xwt.GtkBackend
{
	static class GtkDialogHelper
	{
		public static T RunTask<T>(Task<T> task)
		{
			if (task == null)
				return default;

			if (!task.IsCompleted) {
				var context = GLib.Functions.MainContextDefault();
				while (!task.IsCompleted)
					context.Iteration(true);
			}

			return task.GetAwaiter().GetResult();
		}

		public static bool RunDialog(Gtk.Dialog dialog, Gtk.Window parent, out int responseId)
		{
			int response = (int)Gtk.ResponseType.None;
			responseId = response;
			if (dialog == null)
				return false;

			var context = GLib.Functions.MainContextDefault();
			using var loop = GLib.MainLoop.New(context, false);

			GObject.SignalHandler<Gtk.Dialog, Gtk.Dialog.ResponseSignalArgs> handler = (sender, args) => {
				response = args.ResponseId;
				loop.Quit();
			};

			dialog.OnResponse += handler;
			if (parent != null)
				dialog.TransientFor = parent;
			dialog.Modal = true;
			dialog.SetDefaultResponse((int)Gtk.ResponseType.Ok);
			dialog.Show();

			loop.Run();

			dialog.OnResponse -= handler;
			dialog.Hide();
			dialog.Destroy();

			responseId = response;

			return response == (int)Gtk.ResponseType.Ok || response == (int)Gtk.ResponseType.Accept;
		}
	}
}
