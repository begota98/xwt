using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ExpanderBackend : WidgetBackend, IExpanderBackend
	{
		Gtk.Expander expander;
		IExpandEventSink expandEventSink;

		public ExpanderBackend()
		{
			expander = Gtk.Expander.New(string.Empty);
			Widget = expander;
			Widget.Show();
		}

		protected new Gtk.Expander Widget {
			get { return (Gtk.Expander)base.Widget; }
			set { base.Widget = value; }
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			expandEventSink = (IExpandEventSink)sink;
		}

		public string Label {
			get { return expander.Label ?? string.Empty; }
			set { expander.Label = value ?? string.Empty; }
		}

		public bool Expanded {
			get { return expander.Expanded; }
			set { expander.Expanded = value; }
		}

		public void SetContent(IWidgetBackend child)
		{
			expander.Child = child != null ? ((IGtkWidgetBackend)child).Widget : null;
			if (child != null)
				WidgetBackend.ApplyChildPlacement(child);
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is ExpandEvent ev && ev == ExpandEvent.ExpandChanged)
				expander.OnNotify += HandleNotify;
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is ExpandEvent ev && ev == ExpandEvent.ExpandChanged)
				expander.OnNotify -= HandleNotify;
		}

		void HandleNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
		{
			if (args?.Pspec == null || args.Pspec.GetName() != "expanded")
				return;
			ApplicationContext.InvokeUserCode(expandEventSink.ExpandChanged);
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}
	}
}
