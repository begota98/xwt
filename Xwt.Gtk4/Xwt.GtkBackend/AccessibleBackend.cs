using System;
using System.Collections.Generic;
using System.Linq;
using Xwt.Accessibility;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class AccessibleBackend : IAccessibleBackend
	{
		Gtk.Widget widget;
		IAccessibleEventSink eventSink;
		ApplicationContext context;
		string label;
		string description;
		string identifier;
		string roleDescription;
		string value;
		Uri uri;

		public event EventHandler AccessibilityInUseChanged;

		public void Initialize(IAccessibleEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void Initialize(IWidgetBackend parentWidget, IAccessibleEventSink eventSink)
		{
			var backend = parentWidget as WidgetBackend;
			if (Platform.IsMac && backend is IComboBoxEntryBackend) {
				// Apply a11y settings to the entry inside the combo box.
				Initialize((backend?.Widget as Gtk.ComboBoxText)?.Child, eventSink);
			} else if (backend is TableViewBackend && backend.Widget is Gtk.ScrolledWindow) {
				// Apply a11y settings to the scrollable child.
				Initialize((backend.Widget as Gtk.ScrolledWindow)?.Child, eventSink);
			} else {
				Initialize(backend?.Widget, eventSink);
			}
		}

		public void Initialize(IPopoverBackend parentPopover, IAccessibleEventSink eventSink)
		{
			// Not currently supported.
		}

		public void Initialize(IMenuBackend parentMenu, IAccessibleEventSink eventSink)
		{
			var menuBackend = parentMenu as MenuBackend;
			Initialize(menuBackend?.Menu, eventSink);
		}

		public void Initialize(IMenuItemBackend parentMenuItem, IAccessibleEventSink eventSink)
		{
			var menuItemBackend = parentMenuItem as MenuItemBackend;
			Initialize(menuItemBackend?.MenuItem, eventSink);
		}

		public void Initialize(object parentWidget, IAccessibleEventSink eventSink)
		{
			this.eventSink = eventSink;
			widget = parentWidget as Gtk.Widget;
			if (widget == null)
				throw new ArgumentException("The widget is not a Gtk.Widget.", nameof(parentWidget));
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public Rectangle Bounds {
			get {
				if (widget == null)
					return Rectangle.Zero;
				if (widget.GetBounds(out int x, out int y, out int width, out int height))
					return new Rectangle(x, y, width, height);
				return Rectangle.Zero;
			}
			set {
				// Gtk.Accessible does not expose a bounds setter.
			}
		}

		public string Description {
			get { return description; }
			set {
				description = value;
				UpdateProperty(Gtk.AccessibleProperty.Description, description);
			}
		}

		public string Label {
			get { return label; }
			set {
				label = value;
				UpdateProperty(Gtk.AccessibleProperty.Label, label);
			}
		}

		public string Identifier {
			get { return identifier; }
			set {
				identifier = value;
				UpdateProperty(Gtk.AccessibleProperty.Label, identifier);
			}
		}

		public Role Role {
			get {
				if (widget == null)
					return Role.None;
				return widget.AccessibleRole.ToXwtRole();
			}
			set {
				if (widget != null)
					widget.AccessibleRole = value.ToGtkAccessibleRole();
			}
		}

		public string RoleDescription {
			get { return roleDescription; }
			set {
				roleDescription = value;
				UpdateProperty(Gtk.AccessibleProperty.RoleDescription, roleDescription);
			}
		}

		public string Title { get; set; }

		public string Value {
			get { return value; }
			set {
				this.value = value;
				UpdateProperty(Gtk.AccessibleProperty.ValueText, this.value);
			}
		}

		public Widget LabelWidget {
			set { /* Not supported */ }
		}

		public Uri Uri {
			get { return uri; }
			set { uri = value; }
		}

		public bool IsAccessible {
			get {
				if (widget == null)
					return false;
				return widget.AccessibleRole != Gtk.AccessibleRole.None;
			}
			set {
				if (widget == null)
					return;
				if (value && widget.AccessibleRole == Gtk.AccessibleRole.None)
					widget.AccessibleRole = Gtk.AccessibleRole.Widget;
				if (!value)
					widget.AccessibleRole = Gtk.AccessibleRole.None;
			}
		}

		public bool AccessibilityInUse => false;

		public void AddChild(object nativeChild)
		{
			// Not supported.
		}

		public void RemoveChild(object nativeChild)
		{
			// Not supported.
		}

		public void RemoveAllChildren()
		{
			// Not supported.
		}

		public IEnumerable<object> GetChildren()
		{
			return Enumerable.Empty<object>();
		}

		public void DisableEvent(object eventId)
		{
		}

		public void EnableEvent(object eventId)
		{
		}

		public void MakeAnnouncement(string message, bool polite = false)
		{
			if (widget == null || string.IsNullOrEmpty(message))
				return;
			var priority = polite ? Gtk.AccessibleAnnouncementPriority.Medium : Gtk.AccessibleAnnouncementPriority.High;
			widget.Announce(message, priority);
		}

		void UpdateProperty(Gtk.AccessibleProperty property, string text)
		{
			if (widget == null)
				return;
			var handle = widget.Handle.DangerousGetHandle();
			if (string.IsNullOrEmpty(text)) {
				Gtk.Internal.Accessible.ResetProperty(handle, property);
				return;
			}
			using var value = new GObject.Value(text);
			using var values = GObject.Internal.ValueArray2OwnedHandle.Create(new[] { value });
			Gtk.Internal.Accessible.UpdateProperty(handle, 1, new[] { property }, values);
		}
	}
}
