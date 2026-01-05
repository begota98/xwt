using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SegmentedButtonBackend : WidgetBackend, ISegmentedButtonBackend
	{
		Gtk.Box box;

		public SegmentedButtonBackend()
		{
			box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
			box.Homogeneous = true;
			Widget = box;
			Widget.Show();
		}

		protected new Gtk.Box Widget {
			get { return (Gtk.Box)base.Widget; }
			set { base.Widget = value; }
		}

		public void AddChildButton(int index, Button button)
		{
			var child = ((WidgetBackend)Toolkit.GetBackend(button)).Widget;
			InsertChild(child, index);
		}

		public void RemoveChildButton(int index)
		{
			var child = GetChildAt(index);
			if (child != null)
				Widget.Remove(child);
		}

		public void ReplaceChildButton(int index, Button button)
		{
			var oldChild = GetChildAt(index);
			if (oldChild != null)
				Widget.Remove(oldChild);
			var newChild = ((WidgetBackend)Toolkit.GetBackend(button)).Widget;
			InsertChild(newChild, index);
		}

		public int Spacing {
			get { return Widget.Spacing; }
			set { Widget.Spacing = value; }
		}

		void InsertChild(Gtk.Widget child, int index)
		{
			if (child == null)
				return;
			if (child.Parent != null)
				child.Unparent();

			if (index <= 0 || Widget.GetFirstChild() == null) {
				Widget.Prepend(child);
				return;
			}

			var next = GetChildAt(index);
			if (next == null) {
				Widget.Append(child);
				return;
			}

			Widget.InsertBefore(child, next);
		}

		Gtk.Widget GetChildAt(int index)
		{
			if (index < 0)
				return null;
			int i = 0;
			for (var child = Widget.GetFirstChild(); child != null; child = child.GetNextSibling()) {
				if (i == index)
					return child;
				i++;
			}
			return null;
		}
	}
}
