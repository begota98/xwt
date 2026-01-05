using Xwt.Backends;
using Xwt.Drawing;
using System.Threading;

namespace Xwt.GtkBackend
{
	public class FrameBackend : WidgetBackend, IFrameBackend
	{
		Gtk.Frame frame;
		Gtk.Box paddingBox;
		Gtk.Widget content;
		FrameType frameType = FrameType.WidgetBox;
		Color? borderColor;
		Gtk.CssProvider borderProvider;
		string borderCssClass;
		static int borderClassId;
		double borderLeft;
		double borderRight;
		double borderTop;
		double borderBottom;

		public FrameBackend()
		{
			frame = Gtk.Frame.New(string.Empty);
			Widget = frame;
			Widget.Show();
		}

		public string Label {
			get { return frame.Label ?? string.Empty; }
			set { frame.Label = value ?? string.Empty; }
		}

		public Color BorderColor {
			get { return borderColor ?? Colors.Transparent; }
			set {
				borderColor = value;
				ApplyBorderStyle();
			}
		}

		public void SetFrameType(FrameType type)
		{
			frameType = type;
			ApplyBorderStyle();
		}

		public void SetContent(IWidgetBackend child)
		{
			EnsurePaddingBox();
			if (content != null)
				paddingBox.Remove(content);
			content = child != null ? ((IGtkWidgetBackend)child).Widget : null;
			if (content != null) {
				content.Hexpand = true;
				content.Vexpand = true;
				WidgetBackend.ApplyChildPlacement(child);
				paddingBox.Append(content);
			}
		}

		public void SetBorderSize(double left, double right, double top, double bottom)
		{
			borderLeft = left;
			borderRight = right;
			borderTop = top;
			borderBottom = bottom;
			ApplyBorderStyle();
		}

		public void SetPadding(double left, double right, double top, double bottom)
		{
			EnsurePaddingBox();
			paddingBox.MarginStart = (int)left;
			paddingBox.MarginEnd = (int)right;
			paddingBox.MarginTop = (int)top;
			paddingBox.MarginBottom = (int)bottom;
		}

		public void UpdateChildPlacement(IWidgetBackend childBackend)
		{
			WidgetBackend.ApplyChildPlacement(childBackend);
		}

		void EnsurePaddingBox()
		{
			if (paddingBox != null)
				return;
			paddingBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
			paddingBox.Hexpand = true;
			paddingBox.Vexpand = true;
			frame.SetChild(paddingBox);
		}

		void ApplyBorderStyle()
		{
			if (frameType != FrameType.Custom)
				return;

			if (borderProvider == null) {
				borderProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, borderProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(borderCssClass))
				borderCssClass = "xwt-frame-border-" + Interlocked.Increment(ref borderClassId).ToString();
			if (!frame.HasCssClass(borderCssClass))
				frame.AddCssClass(borderCssClass);

			var color = borderColor ?? Colors.Transparent;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + borderCssClass
				+ " { border-style: solid; border-width: "
				+ borderTop.ToString("0.###") + "px "
				+ borderRight.ToString("0.###") + "px "
				+ borderBottom.ToString("0.###") + "px "
				+ borderLeft.ToString("0.###") + "px; "
				+ "border-color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			borderProvider.LoadFromString(css);
		}
	}
}
