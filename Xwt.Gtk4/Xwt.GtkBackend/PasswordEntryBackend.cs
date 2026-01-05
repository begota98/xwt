using System;
using System.Security;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class PasswordEntryBackend : WidgetBackend, IPasswordEntryBackend
	{
		readonly Gtk.PasswordEntry entry;
		IPasswordEntryEventSink eventSink;

		public PasswordEntryBackend()
		{
			entry = Gtk.PasswordEntry.New();
			Widget = entry;
			Widget.Show();
		}

		public string Password {
			get { return entry.Text_ ?? string.Empty; }
			set { entry.Text_ = value ?? string.Empty; }
		}

		public SecureString SecurePassword {
			get {
				var secure = new SecureString();
				foreach (var ch in Password)
					secure.AppendChar(ch);
				secure.MakeReadOnly();
				return secure;
			}
		}

		public string PlaceholderText {
			get { return entry.PlaceholderText ?? string.Empty; }
			set { entry.PlaceholderText = value ?? string.Empty; }
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (IPasswordEntryEventSink)sink;
		}

		public override void EnableEvent(object eventId)
		{
			if (!(eventId is PasswordEntryEvent ev))
				return;
			switch (ev) {
				case PasswordEntryEvent.Changed:
					entry.OnChanged += HandleChanged;
					break;
				case PasswordEntryEvent.Activated:
					entry.OnActivate += HandleActivated;
					break;
			}
		}

		public override void DisableEvent(object eventId)
		{
			if (!(eventId is PasswordEntryEvent ev))
				return;
			switch (ev) {
				case PasswordEntryEvent.Changed:
					entry.OnChanged -= HandleChanged;
					break;
				case PasswordEntryEvent.Activated:
					entry.OnActivate -= HandleActivated;
					break;
			}
		}

		void HandleChanged(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnChanged);
		}

		void HandleActivated(object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode(eventSink.OnActivated);
		}
	}
}
