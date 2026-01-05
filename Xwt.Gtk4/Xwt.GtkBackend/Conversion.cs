using Xwt.Accessibility;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	static class Conversion
	{
		public static Gdk.RGBA ToGtkValue(this Color color)
		{
			return new Gdk.RGBA {
				Red = (float)color.Red,
				Green = (float)color.Green,
				Blue = (float)color.Blue,
				Alpha = (float)color.Alpha
			};
		}

		public static Color ToXwtValue(this Gdk.RGBA color)
		{
			return new Color(color.Red, color.Green, color.Blue, color.Alpha);
		}

		public static ModifierKeys ToXwtValue(this Gdk.ModifierType state)
		{
			ModifierKeys modifiers = ModifierKeys.None;
			if ((state & Gdk.ModifierType.ShiftMask) != 0)
				modifiers |= ModifierKeys.Shift;
			if ((state & Gdk.ModifierType.ControlMask) != 0)
				modifiers |= ModifierKeys.Control;
			if ((state & Gdk.ModifierType.AltMask) != 0)
				modifiers |= ModifierKeys.Alt;
			if ((state & Gdk.ModifierType.SuperMask) != 0 || (state & Gdk.ModifierType.MetaMask) != 0)
				modifiers |= ModifierKeys.Command;
			return modifiers;
		}

		public static Gtk.AccessibleRole ToGtkAccessibleRole(this Role role)
		{
			switch (role) {
				case Role.Button:
				case Role.ButtonClose:
				case Role.ButtonMinimize:
				case Role.ButtonMaximize:
				case Role.ButtonFullscreen:
					return Gtk.AccessibleRole.Button;
				case Role.Calendar:
					return Gtk.AccessibleRole.Widget;
				case Role.Cell:
					return Gtk.AccessibleRole.Cell;
				case Role.CheckBox:
					return Gtk.AccessibleRole.Checkbox;
				case Role.ColorChooser:
					return Gtk.AccessibleRole.Widget;
				case Role.Column:
					return Gtk.AccessibleRole.ColumnHeader;
				case Role.ComboBox:
					return Gtk.AccessibleRole.ComboBox;
				case Role.Custom:
					return Gtk.AccessibleRole.Widget;
				case Role.Disclosure:
					return Gtk.AccessibleRole.Button;
				case Role.Filler:
					return Gtk.AccessibleRole.Widget;
				case Role.Group:
					return Gtk.AccessibleRole.Group;
				case Role.Image:
					return Gtk.AccessibleRole.Img;
				case Role.Label:
					return Gtk.AccessibleRole.Label;
				case Role.LevelIndicator:
					return Gtk.AccessibleRole.Meter;
				case Role.Link:
					return Gtk.AccessibleRole.Link;
				case Role.List:
					return Gtk.AccessibleRole.List;
				case Role.Menu:
					return Gtk.AccessibleRole.Menu;
				case Role.MenuBar:
					return Gtk.AccessibleRole.MenuBar;
				case Role.MenuBarItem:
				case Role.MenuItem:
					return Gtk.AccessibleRole.MenuItem;
				case Role.MenuButton:
					return Gtk.AccessibleRole.Button;
				case Role.MenuItemCheckBox:
					return Gtk.AccessibleRole.MenuItemCheckbox;
				case Role.MenuItemRadio:
					return Gtk.AccessibleRole.MenuItemRadio;
				case Role.Notebook:
					return Gtk.AccessibleRole.TabList;
				case Role.NotebookTab:
					return Gtk.AccessibleRole.Tab;
				case Role.Paned:
					return Gtk.AccessibleRole.Widget;
				case Role.PanedSplitter:
					return Gtk.AccessibleRole.Separator;
				case Role.Popup:
					return Gtk.AccessibleRole.Dialog;
				case Role.ProgressBar:
					return Gtk.AccessibleRole.ProgressBar;
				case Role.RadioButton:
					return Gtk.AccessibleRole.Radio;
				case Role.RadioGroup:
					return Gtk.AccessibleRole.RadioGroup;
				case Role.Row:
					return Gtk.AccessibleRole.Row;
				case Role.ScrollBar:
					return Gtk.AccessibleRole.Scrollbar;
				case Role.ScrollView:
					return Gtk.AccessibleRole.Region;
				case Role.Separator:
					return Gtk.AccessibleRole.Separator;
				case Role.Slider:
					return Gtk.AccessibleRole.Slider;
				case Role.SpinButton:
					return Gtk.AccessibleRole.SpinButton;
				case Role.Table:
					return Gtk.AccessibleRole.Table;
				case Role.TextArea:
				case Role.TextEntry:
				case Role.TextEntryPassword:
					return Gtk.AccessibleRole.TextBox;
				case Role.TextEntrySearch:
					return Gtk.AccessibleRole.SearchBox;
				case Role.ToggleButton:
					return Gtk.AccessibleRole.ToggleButton;
				case Role.ToolBar:
					return Gtk.AccessibleRole.Toolbar;
				case Role.ToolTip:
					return Gtk.AccessibleRole.Tooltip;
				case Role.Tree:
					return Gtk.AccessibleRole.Tree;
				case Role.None:
					return Gtk.AccessibleRole.None;
				default:
					return Gtk.AccessibleRole.Widget;
			}
		}

		public static Role ToXwtRole(this Gtk.AccessibleRole role)
		{
			switch (role) {
				case Gtk.AccessibleRole.Button:
					return Role.Button;
				case Gtk.AccessibleRole.Checkbox:
					return Role.CheckBox;
				case Gtk.AccessibleRole.ColumnHeader:
					return Role.Column;
				case Gtk.AccessibleRole.ComboBox:
					return Role.ComboBox;
				case Gtk.AccessibleRole.Img:
					return Role.Image;
				case Gtk.AccessibleRole.Label:
					return Role.Label;
				case Gtk.AccessibleRole.Link:
					return Role.Link;
				case Gtk.AccessibleRole.List:
					return Role.List;
				case Gtk.AccessibleRole.Menu:
					return Role.Menu;
				case Gtk.AccessibleRole.MenuBar:
					return Role.MenuBar;
				case Gtk.AccessibleRole.MenuItem:
					return Role.MenuItem;
				case Gtk.AccessibleRole.MenuItemCheckbox:
					return Role.MenuItemCheckBox;
				case Gtk.AccessibleRole.MenuItemRadio:
					return Role.MenuItemRadio;
				case Gtk.AccessibleRole.ProgressBar:
					return Role.ProgressBar;
				case Gtk.AccessibleRole.Radio:
					return Role.RadioButton;
				case Gtk.AccessibleRole.RadioGroup:
					return Role.RadioGroup;
				case Gtk.AccessibleRole.Row:
					return Role.Row;
				case Gtk.AccessibleRole.Scrollbar:
					return Role.ScrollBar;
				case Gtk.AccessibleRole.Slider:
					return Role.Slider;
				case Gtk.AccessibleRole.SpinButton:
					return Role.SpinButton;
				case Gtk.AccessibleRole.Table:
					return Role.Table;
				case Gtk.AccessibleRole.TextBox:
					return Role.TextEntry;
				case Gtk.AccessibleRole.SearchBox:
					return Role.TextEntrySearch;
				case Gtk.AccessibleRole.ToggleButton:
					return Role.ToggleButton;
				case Gtk.AccessibleRole.Toolbar:
					return Role.ToolBar;
				case Gtk.AccessibleRole.Tooltip:
					return Role.ToolTip;
				case Gtk.AccessibleRole.Tree:
					return Role.Tree;
				case Gtk.AccessibleRole.TabList:
					return Role.Notebook;
				case Gtk.AccessibleRole.Tab:
					return Role.NotebookTab;
				case Gtk.AccessibleRole.None:
					return Role.None;
				default:
					return Role.None;
			}
		}
	}
}
