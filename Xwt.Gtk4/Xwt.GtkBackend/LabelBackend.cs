using System;
using System.Collections.Generic;
using System.Threading;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class LabelBackend : WidgetBackend, ILabelBackend
	{
		Gtk.Label label;
		FormattedText formatted;
		ILabelEventSink eventSink;
		bool linkEventEnabled;
		bool mouseInLink;
		Gtk.EventControllerMotion motionController;
		Gtk.GestureClick clickController;
		List<LabelLink> links;
		Gtk.CssProvider textColorProvider;
		string textColorCssClass;
		static int textColorClassId;
		Color? customTextColor;

		public LabelBackend()
		{
			label = Gtk.Label.New(string.Empty);
			label.Xalign = 0f;
			label.Yalign = 0.5f;
			label.Halign = Gtk.Align.Start;
			label.Valign = Gtk.Align.Center;
			Widget = label;
			Widget.Show();
		}

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			eventSink = (ILabelEventSink)sink;
		}

		public string Text {
			get { return label.Label_; }
			set {
				formatted = null;
				links = null;
				label.UseMarkup = false;
				label.Label_ = value ?? string.Empty;
				label.SetAttributes(null);
			}
		}

		public bool Selectable {
			get { return label.Selectable; }
			set { label.Selectable = value; }
		}

		public Color TextColor {
			get { return customTextColor ?? GetThemeTextColor(); }
			set {
				customTextColor = value;
				ApplyTextColor();
			}
		}

		public Alignment TextAlignment {
			get {
				double xalign = label.Xalign;
				if (xalign <= 0.01)
					return Alignment.Start;
				if (xalign >= 0.99)
					return Alignment.End;
				return Alignment.Center;
			}
			set {
				switch (value) {
				case Alignment.Start:
					label.Xalign = 0f;
					label.Justify = Gtk.Justification.Left;
					break;
				case Alignment.End:
					label.Xalign = 1f;
					label.Justify = Gtk.Justification.Right;
					break;
				default:
					label.Xalign = 0.5f;
					label.Justify = Gtk.Justification.Center;
					break;
				}
			}
		}

		public EllipsizeMode Ellipsize {
			get { return (EllipsizeMode)(int)label.Ellipsize; }
			set { label.Ellipsize = (Pango.EllipsizeMode)(int)value; }
		}

		public WrapMode Wrap {
			get { return (WrapMode)(int)label.WrapMode; }
			set { label.WrapMode = (Pango.WrapMode)(int)value; }
		}

		public void SetFormattedText(FormattedText text)
		{
			formatted = text;
			FormattedTextUtil.ApplyFormattedText(label, text);
			UpdateLinkRanges(text);
		}

		void ApplyTextColor()
		{
			if (!customTextColor.HasValue)
				return;

			if (textColorProvider == null) {
				textColorProvider = Gtk.CssProvider.New();
				var display = Gdk.Display.GetDefault();
				if (display != null)
					Gtk.StyleContext.AddProviderForDisplay(display, textColorProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_USER);
			}

			if (string.IsNullOrEmpty(textColorCssClass))
				textColorCssClass = "xwt-label-text-" + Interlocked.Increment(ref textColorClassId).ToString();
			if (!label.HasCssClass(textColorCssClass))
				label.AddCssClass(textColorCssClass);

			var color = customTextColor.Value;
			var r = (int)(color.Red * 255);
			var g = (int)(color.Green * 255);
			var b = (int)(color.Blue * 255);
			var a = color.Alpha.ToString("0.###");
			var css = "." + textColorCssClass + " { color: rgba(" + r + "," + g + "," + b + "," + a + "); }";
			textColorProvider.LoadFromString(css);
		}

		Color GetThemeTextColor()
		{
			if (label == null)
				return Colors.Black;
			var context = label.GetStyleContext();
			if (context == null)
				return Colors.Black;
			context.GetColor(out var color);
			return color.ToXwtValue();
		}

		public override void EnableEvent(object eventId)
		{
			if (eventId is LabelEvent ev && ev == LabelEvent.LinkClicked) {
				linkEventEnabled = true;
				EnsureLinkControllers();
			}
		}

		public override void DisableEvent(object eventId)
		{
			if (eventId is LabelEvent ev && ev == LabelEvent.LinkClicked) {
				linkEventEnabled = false;
				RemoveLinkControllers();
			}
		}

		void EnsureLinkControllers()
		{
			if (motionController != null)
				return;
			motionController = Gtk.EventControllerMotion.New();
			motionController.OnMotion += HandleMotion;
			motionController.OnLeave += HandleLeave;
			label.AddController(motionController);

			clickController = Gtk.GestureClick.New();
			clickController.SetButton(0);
			clickController.OnReleased += HandleReleased;
			label.AddController(clickController);
		}

		void RemoveLinkControllers()
		{
			if (motionController != null) {
				label.RemoveController(motionController);
				motionController.Dispose();
				motionController = null;
			}
			if (clickController != null) {
				label.RemoveController(clickController);
				clickController.Dispose();
				clickController = null;
			}
		}

		void HandleMotion(Gtk.EventControllerMotion sender, Gtk.EventControllerMotion.MotionSignalArgs args)
		{
			if (!linkEventEnabled || links == null)
				return;
			var link = FindLink(args.X, args.Y);
			if (link != null) {
				if (!mouseInLink) {
					mouseInLink = true;
					label.SetCursorFromName("pointer");
				}
			} else if (mouseInLink) {
				mouseInLink = false;
				label.SetCursor(null);
			}
		}

		void HandleLeave(Gtk.EventControllerMotion sender, EventArgs args)
		{
			if (!mouseInLink)
				return;
			mouseInLink = false;
			label.SetCursor(null);
		}

		void HandleReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if (!linkEventEnabled || links == null)
				return;
			if ((PointerButton)sender.GetCurrentButton() != PointerButton.Left)
				return;
			var link = FindLink(args.X, args.Y);
			if (link == null)
				return;
			ApplicationContext.InvokeUserCode(() => eventSink.OnLinkClicked(link.Target));
		}

		LabelLink FindLink(double px, double py)
		{
			if (links == null || links.Count == 0)
				return null;
			var layout = label.GetLayout();
			if (layout == null)
				return null;
			label.GetLayoutOffsets(out var offsetX, out var offsetY);
			var x = (int)((px - offsetX) * Pango.Constants.SCALE);
			var y = (int)((py - offsetY) * Pango.Constants.SCALE);
			if (!layout.XyToIndex(x, y, out var byteIndex, out _))
				return null;
			foreach (var link in links) {
				if (byteIndex >= link.StartIndex && byteIndex <= link.EndIndex)
					return link;
			}
			return null;
		}

		void UpdateLinkRanges(FormattedText text)
		{
			links = null;
			if (text == null || text.Attributes == null)
				return;
			var indexer = new TextIndexer(text.Text ?? string.Empty);
			foreach (var attr in text.Attributes) {
				if (!(attr is LinkTextAttribute linkAttr))
					continue;
				var link = new LabelLink {
					StartIndex = indexer.IndexToByteIndex(linkAttr.StartIndex),
					EndIndex = indexer.IndexToByteIndex(linkAttr.StartIndex + linkAttr.Count),
					Target = linkAttr.Target
				};
				if (links == null)
					links = new List<LabelLink>();
				links.Add(link);
			}
			if (links == null || links.Count == 0)
				links = null;
			else if (linkEventEnabled)
				EnsureLinkControllers();
		}

		class LabelLink
		{
			public int StartIndex;
			public int EndIndex;
			public Uri Target;
		}
	}
}
