using System;
using System.Collections.Generic;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	internal interface IGtkMenuItemBackend
	{
		Gtk.Widget Widget { get; }
	}

	public class MenuBackend : IMenuBackend
	{
		Gtk.Popover popover;
		Gtk.Box box;
		ApplicationContext context;
		bool isMenuBar;
		Gtk.Orientation boxOrientation;
		Pango.FontDescription customFont;
		Gtk.CssProvider fontProvider;
		string fontCssClass;
		static int fontClassId;

		public object Font {
			get {
				if (customFont != null)
					return customFont;
				if (box != null) {
					using var layout = box.CreatePangoLayout(string.Empty);
					var desc = layout.GetFontDescription();
					if (desc != null)
						return desc;
				}
				var fallback = Pango.FontDescription.New();
				fallback.SetFamily("Sans");
				fallback.SetSize((int)(10 * Pango.Constants.SCALE));
				return fallback;
			}
			set {
				customFont = value as Pango.FontDescription;
				ApplyFont();
			}
		}

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
		}

		public void DisableEvent(object eventId)
		{
		}

		public void InsertItem(int index, IMenuItemBackend menuItem)
		{
			if (!(menuItem is IGtkMenuItemBackend gtkItem))
				return;
			EnsureBox(isMenuBar ? Gtk.Orientation.Horizontal : Gtk.Orientation.Vertical);
			InsertChildAt(index, gtkItem.Widget);
		}

		public void RemoveItem(IMenuItemBackend menuItem)
		{
			if (!(menuItem is IGtkMenuItemBackend gtkItem) || box == null)
				return;
			box.Remove(gtkItem.Widget);
		}

		public void Popup()
		{
			if (popover == null)
				return;
			EnsurePopoverParent(null);
			var display = Gdk.Display.GetDefault();
			var seat = display?.GetDefaultSeat();
			var pointer = seat?.GetPointer();
			if (pointer != null) {
				pointer.GetSurfaceAtPosition(out double x, out double y);
				popover.PointingTo = new Gdk.Rectangle { X = (int)x, Y = (int)y, Width = 1, Height = 1 };
			}
			popover.Popup();
		}

		public void Popup(IWidgetBackend widget, double x, double y)
		{
			if (widget == null)
				return;
			EnsurePopover();
			if (popover == null)
				return;
			var parent = ((IGtkWidgetBackend)widget).Widget;
			EnsurePopoverParent(parent);
			popover.Position = Gtk.PositionType.Bottom;
			popover.PointingTo = new Gdk.Rectangle { X = (int)x, Y = (int)y, Width = 1, Height = 1 };
			popover.Popup();
		}

		internal void AttachTo(Gtk.Widget parent)
		{
			EnsurePopover();
			if (popover == null || parent == null)
				return;
			if (popover.Parent == null)
				popover.SetParent(parent);
		}

		internal Gtk.Popover Popover {
			get {
				EnsurePopover();
				return popover;
			}
		}

		internal Gtk.Widget Menu {
			get {
				if (box == null) {
					if (isMenuBar)
						EnsureBox(Gtk.Orientation.Horizontal);
					else
						EnsurePopover();
				}
				return (Gtk.Widget)popover ?? box;
			}
		}

		internal void Popup(Gtk.Widget parent, int x, int y)
		{
			if (parent == null)
				return;
			EnsurePopover();
			if (popover == null)
				return;
			EnsurePopoverParent(parent);
			popover.Position = Gtk.PositionType.Bottom;
			int width = parent.GetAllocatedWidth();
			if (width <= 0)
				width = 1;
			popover.PointingTo = new Gdk.Rectangle { X = x, Y = y, Width = width, Height = 1 };
			popover.Popup();
		}

		internal Gtk.Widget GetMenuBarWidget()
		{
			isMenuBar = true;
			EnsureBox(Gtk.Orientation.Horizontal);
			box.MarginStart = 6;
			box.MarginEnd = 6;
			box.MarginTop = 4;
			box.MarginBottom = 4;
			box.Hexpand = true;
			box.Halign = Gtk.Align.Start;
			return box;
		}

		void EnsurePopover()
		{
			if (popover != null)
				return;
			if (isMenuBar)
				return;
			EnsureBox(Gtk.Orientation.Vertical);
			popover = Gtk.Popover.New();
			popover.Child = box;
		}

		void EnsureBox(Gtk.Orientation orientation)
		{
			if (box == null) {
				box = Gtk.Box.New(orientation, orientation == Gtk.Orientation.Horizontal ? 6 : 2);
				boxOrientation = orientation;
				box.Show();
				ApplyFont();
				return;
			}

			if (boxOrientation != orientation) {
				var newBox = Gtk.Box.New(orientation, orientation == Gtk.Orientation.Horizontal ? 6 : 2);
				newBox.Show();
				for (var child = box.GetFirstChild(); child != null; ) {
					var next = child.GetNextSibling();
					box.Remove(child);
					newBox.Append(child);
					child = next;
				}
				box = newBox;
				boxOrientation = orientation;
				if (popover != null)
					popover.Child = box;
				return;
			}

			box.Spacing = orientation == Gtk.Orientation.Horizontal ? 6 : 2;
		}

		void EnsurePopoverParent(Gtk.Widget parent)
		{
			if (popover == null || popover.Parent != null)
				return;
			if (parent == null)
				parent = GtkEngine.Application?.ActiveWindow;
			if (parent != null)
				popover.SetParent(parent);
		}

		void ApplyFont()
		{
			if (box == null || customFont == null)
				return;

			if (fontProvider == null) {
				fontProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, fontProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(fontCssClass))
				fontCssClass = "xwt-menu-font-" + System.Threading.Interlocked.Increment(ref fontClassId).ToString();
			if (!box.HasCssClass(fontCssClass))
				box.AddCssClass(fontCssClass);

			var family = customFont.GetFamily() ?? "Sans";
			var sizePx = customFont.GetSize() / (double)Pango.Constants.SCALE;
			var weight = customFont.GetWeight();
			var style = customFont.GetStyle();
			string styleCss = style == Pango.Style.Italic ? "italic" : style == Pango.Style.Oblique ? "oblique" : "normal";
			string css = "." + fontCssClass + " { font-family: " + family + "; font-size: " + sizePx.ToString("0.###") + "px; font-style: " + styleCss + "; font-weight: " + (int)weight + "; }";
			fontProvider.LoadFromString(css);
		}

		void InsertChildAt(int index, Gtk.Widget child)
		{
			if (box == null || child == null)
				return;

			if (index <= 0 || box.GetFirstChild() == null) {
				box.Prepend(child);
				return;
			}

			var next = GetChildAt(index);
			if (next == null) {
				box.Append(child);
				return;
			}

			box.InsertBefore(child, next);
		}

		Gtk.Widget GetChildAt(int index)
		{
			if (index < 0 || box == null)
				return null;
			int currentIndex = 0;
			for (var child = box.GetFirstChild(); child != null; child = child.GetNextSibling()) {
				if (currentIndex == index)
					return child;
				currentIndex++;
			}
			return null;
		}
	}
}
