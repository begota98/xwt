using System;
using System.Collections.Generic;
using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class RichTextViewBackend : WidgetBackend, IRichTextViewBackend
	{
		const string TagBold = "xwt-bold";
		const string TagItalic = "xwt-italic";
		const string TagMonospace = "xwt-mono";
		const string TagListItem = "xwt-li";
		const string TagParagraph = "xwt-paragraph";
		const string TagPre = "xwt-pre";
		const string TagTextColor = "xwt-text-color";
		const string TagHeaderPrefix = "xwt-h";

		Gtk.TextView view;
		Gtk.TextTagTable tagTable;
		RichTextBuffer currentBuffer;
		int lineSpacing;
		bool selectable = true;
		bool readOnly;
		Color textColor;
		bool textColorSet;
		Link activeLink;
		Gtk.EventControllerMotion linkMotion;
		Gtk.GestureClick linkClick;

		double ParagraphSpacing {
			get {
				var font = Font as Pango.FontDescription;
				if (font == null)
					return 0;
				var size = font.GetSize();
				var sizePoints = font.GetSizeIsAbsolute() ? size : size / (double)Pango.Constants.SCALE;
				return sizePoints / 2;
			}
		}

		public RichTextViewBackend()
		{
			view = Gtk.TextView.New();
			view.WrapMode = Gtk.WrapMode.Word;
			Widget = view;
			Widget.Show();
			InitTagTable();
			currentBuffer = new RichTextBuffer(tagTable);
			view.Buffer = currentBuffer.Buffer;
			ReadOnly = true;
		}

		protected new IRichTextViewEventSink EventSink => (IRichTextViewEventSink)base.EventSink;

		void InitTagTable()
		{
			tagTable = Gtk.TextTagTable.New();
			AddTag(TagBold, tag => {
				tag.Weight = (int)Pango.Weight.Bold;
				tag.WeightSet = true;
			});
			AddTag(TagItalic, tag => {
				tag.Style = Pango.Style.Italic;
				tag.StyleSet = true;
			});
			AddTag(TagMonospace, tag => {
				tag.Family = "monospace";
				tag.FamilySet = true;
			});
			AddTag(TagListItem, tag => {
				tag.LeftMargin = 14;
			});
			AddTag(TagPre, tag => {
				tag.Family = "monospace";
				tag.FamilySet = true;
				tag.Indent = 14;
				tag.WrapMode = Gtk.WrapMode.None;
			});
			AddTag(TagParagraph, tag => {
				tag.SizePoints = Math.Max(LineSpacing, ParagraphSpacing);
				tag.SizeSet = true;
			});
			AddTag(TagTextColor, tag => { });

			AddHeaderTag(1, 18, true);
			AddHeaderTag(2, 16, true);
			AddHeaderTag(3, 14, true);
			AddHeaderTag(4, 12, false);
		}

		void AddTag(string name, Action<Gtk.TextTag> init)
		{
			var tag = Gtk.TextTag.New(name);
			init?.Invoke(tag);
			tagTable.Add(tag);
		}

		void AddHeaderTag(int level, int sizePoints, bool bold)
		{
			AddTag(TagHeaderPrefix + level, tag => {
				if (bold) {
					tag.Weight = (int)Pango.Weight.Bold;
					tag.WeightSet = true;
				}
				tag.SizePoints = sizePoints;
				tag.SizeSet = true;
			});
		}

		public override void EnableEvent(object eventId)
		{
			base.EnableEvent(eventId);
			if (eventId is RichTextViewEvent rtv && rtv == RichTextViewEvent.NavigateToUrl)
				EnsureLinkControllers();
		}

		public override void DisableEvent(object eventId)
		{
			base.DisableEvent(eventId);
			if (eventId is RichTextViewEvent rtv && rtv == RichTextViewEvent.NavigateToUrl)
				RemoveLinkControllers();
		}

		void EnsureLinkControllers()
		{
			if (linkMotion != null)
				return;

			linkMotion = Gtk.EventControllerMotion.New();
			linkMotion.OnMotion += HandleLinkMotion;
			linkMotion.OnLeave += HandleLinkLeave;
			view.AddController(linkMotion);

			linkClick = Gtk.GestureClick.New();
			linkClick.SetButton(0);
			linkClick.OnReleased += HandleLinkReleased;
			view.AddController(linkClick);
		}

		void RemoveLinkControllers()
		{
			if (linkMotion != null) {
				view.RemoveController(linkMotion);
				linkMotion.Dispose();
				linkMotion = null;
			}
			if (linkClick != null) {
				view.RemoveController(linkClick);
				linkClick.Dispose();
				linkClick = null;
			}
		}

		void HandleLinkMotion(Gtk.EventControllerMotion sender, Gtk.EventControllerMotion.MotionSignalArgs args)
		{
			var link = GetLinkAtPos(args.X, args.Y);
			if (link != null) {
				if (activeLink != link)
					view.TooltipText = link.Title;
				activeLink = link;
				UpdateCursor(true);
				return;
			}
			if (activeLink != null) {
				activeLink = null;
				view.TooltipText = null;
				UpdateCursor(false);
			}
		}

		void HandleLinkLeave(Gtk.EventControllerMotion sender, EventArgs args)
		{
			if (activeLink != null) {
				activeLink = null;
				view.TooltipText = null;
				UpdateCursor(false);
			}
		}

		void HandleLinkReleased(Gtk.GestureClick sender, Gtk.GestureClick.ReleasedSignalArgs args)
		{
			if ((PointerButton)sender.GetCurrentButton() != PointerButton.Left)
				return;
			var link = activeLink ?? GetLinkAtPos(args.X, args.Y);
			if (link?.Href == null)
				return;
			ApplicationContext.InvokeUserCode(() => EventSink.OnNavigateToUrl(link.Href));
		}

		void UpdateCursor(bool overLink)
		{
			if (view == null)
				return;
			if (overLink) {
				view.SetCursorFromName("pointer");
				return;
			}
			if (selectable)
				view.SetCursorFromName("text");
			else
				view.SetCursor(null);
		}

		Link GetLinkAtPos(double mousex, double mousey)
		{
			var buffer = currentBuffer;
			if (buffer == null)
				return null;
			view.WindowToBufferCoords(Gtk.TextWindowType.Text, (int)mousex, (int)mousey, out var x, out var y);
			if (!view.GetIterAtLocation(out var iter, x, y))
				return null;
			foreach (var entry in buffer.Links) {
				if (iter.HasTag(entry.Key))
					return entry.Value;
			}
			return null;
		}

		void ApplySelectionState()
		{
			if (view == null)
				return;
			view.CanFocus = selectable;
			view.CursorVisible = selectable && !readOnly;
			UpdateCursor(activeLink != null);
		}

		void UpdateParagraphTag()
		{
			if (tagTable == null)
				return;
			var tag = tagTable.Lookup(TagParagraph);
			if (tag == null)
				return;
			tag.SizePoints = Math.Max(LineSpacing, ParagraphSpacing);
			tag.SizeSet = true;
		}

		void ApplyTextColor()
		{
			if (!textColorSet || currentBuffer == null || tagTable == null)
				return;
			var tag = tagTable.Lookup(TagTextColor);
			if (tag == null)
				return;
			tag.ForegroundRgba = textColor.ToGtkValue();
			tag.ForegroundSet = true;
			currentBuffer.ApplyTag(tag);
		}

		public IRichTextBuffer CreateBuffer()
		{
			return new RichTextBuffer(tagTable);
		}

		public void SetBuffer(IRichTextBuffer buffer)
		{
			var buf = buffer as RichTextBuffer;
			if (buf == null)
				throw new ArgumentException("Passed buffer is of incorrect type", nameof(buffer));
			currentBuffer = buf;
			view.Buffer = buf.Buffer;
			ApplyTextColor();
		}

		public bool ReadOnly {
			get { return readOnly; }
			set {
				readOnly = value;
				if (view != null) {
					view.Editable = !value;
					ApplySelectionState();
				}
			}
		}

		public bool Selectable {
			get { return selectable; }
			set {
				selectable = value;
				if (view != null)
					ApplySelectionState();
			}
		}

		public int LineSpacing {
			get { return lineSpacing; }
			set {
				lineSpacing = value;
				if (view != null) {
					view.PixelsInsideWrap = value;
					view.PixelsBelowLines = value;
				}
				UpdateParagraphTag();
			}
		}

		public IRichTextBuffer CurrentBuffer => currentBuffer;

		public Color TextColor {
			get { return textColor; }
			set {
				textColor = value;
				textColorSet = true;
				ApplyTextColor();
			}
		}

		public override object Font {
			get { return base.Font; }
			set {
				base.Font = value;
				UpdateParagraphTag();
			}
		}

		class Link
		{
			public string Title;
			public Uri Href;
			public Gtk.TextMark StartMark;
		}

		class RichTextBuffer : IRichTextBuffer
		{
			const string NewLine = "\n";

			readonly Gtk.TextTagTable table;
			bool needsParagraphBreak;
			bool needsListBreak;

			public RichTextBuffer(Gtk.TextTagTable table)
			{
				this.table = table;
				Buffer = Gtk.TextBuffer.New(table);
				Links = new Dictionary<Gtk.TextTag, Link>();
				openHeaders = new Stack<StartState>();
				openLinks = new Stack<Link>();
			}

			public Gtk.TextBuffer Buffer { get; }

			public Dictionary<Gtk.TextTag, Link> Links { get; }

			struct StartState
			{
				public Gtk.TextMark Mark;
				public int Data;

				public StartState(Gtk.TextMark mark, int data)
				{
					Mark = mark;
					Data = data;
				}
			}

			readonly Stack<StartState> openHeaders;
			readonly Stack<Link> openLinks;

			void BreakParagraph()
			{
				if (!needsParagraphBreak)
					return;
				Buffer.GetEndIter(out var iterEnd);
				var mark = Buffer.CreateMark(null, iterEnd, true);
				Buffer.Insert(iterEnd, NewLine, NewLine.Length);
				Buffer.GetEndIter(out iterEnd);
				Buffer.Insert(iterEnd, NewLine, NewLine.Length);
				Buffer.GetIterAtMark(out var iterStart, mark);
				Buffer.GetEndIter(out iterEnd);
				Buffer.ApplyTagByName(TagParagraph, iterStart, iterEnd);
				Buffer.DeleteMark(mark);
				needsParagraphBreak = false;
			}

			void BreakList()
			{
				if (!needsListBreak)
					return;
				Buffer.GetEndIter(out var iterEnd);
				Buffer.Insert(iterEnd, NewLine, NewLine.Length);
				needsListBreak = false;
			}

			public string PlainText {
				get {
					Buffer.GetStartIter(out var start);
					Buffer.GetEndIter(out var end);
					return Buffer.GetText(start, end, false) ?? string.Empty;
				}
			}

			public void EmitText(string text, RichTextInlineStyle style)
			{
				if (string.IsNullOrEmpty(text))
					return;
				Buffer.GetEndIter(out var iterEnd);
				var mark = Buffer.CreateMark(null, iterEnd, true);
				Buffer.Insert(iterEnd, text, text.Length);
				Buffer.GetIterAtMark(out var iterStart, mark);
				Buffer.GetEndIter(out iterEnd);

				if ((style & RichTextInlineStyle.Bold) != 0)
					Buffer.ApplyTagByName(TagBold, iterStart, iterEnd);
				if ((style & RichTextInlineStyle.Italic) != 0)
					Buffer.ApplyTagByName(TagItalic, iterStart, iterEnd);
				if ((style & RichTextInlineStyle.Monospace) != 0)
					Buffer.ApplyTagByName(TagMonospace, iterStart, iterEnd);

				Buffer.DeleteMark(mark);
			}

			public void EmitText(FormattedText text)
			{
				if (text == null) {
					EmitText(string.Empty, RichTextInlineStyle.Normal);
					return;
				}
				Buffer.GetEndIter(out var iterEnd);
				var textMark = Buffer.CreateMark(null, iterEnd, true);
				Buffer.Insert(iterEnd, text.Text, text.Text.Length);

				foreach (var attr in text.Attributes) {
					Buffer.GetIterAtMark(out var iterAttr, textMark);
					iterAttr.ForwardChars(attr.StartIndex);
					var attrStart = Buffer.CreateMark(null, iterAttr, true);
					iterAttr.ForwardChars(attr.Count);

					var tag = Gtk.TextTag.New(null);
					if (attr is BackgroundTextAttribute background) {
						tag.BackgroundRgba = background.Color.ToGtkValue();
						tag.BackgroundSet = true;
					} else if (attr is ColorTextAttribute color) {
						tag.ForegroundRgba = color.Color.ToGtkValue();
						tag.ForegroundSet = true;
					} else if (attr is FontWeightTextAttribute weight) {
						tag.Weight = (int)weight.Weight;
						tag.WeightSet = true;
					} else if (attr is FontStyleTextAttribute style) {
						tag.Style = (Pango.Style)(int)style.Style;
						tag.StyleSet = true;
					} else if (attr is UnderlineTextAttribute underline) {
						tag.Underline = underline.Underline ? Pango.Underline.Single : Pango.Underline.None;
						tag.UnderlineSet = true;
					} else if (attr is StrikethroughTextAttribute strike) {
						tag.Strikethrough = strike.Strikethrough;
						tag.StrikethroughSet = true;
					} else if (attr is FontTextAttribute font) {
						tag.FontDesc = (Pango.FontDescription)Toolkit.GetBackend(font.Font);
					} else if (attr is LinkTextAttribute linkAttr) {
						Uri uri = linkAttr.Target;
						if (uri == null)
							Uri.TryCreate(text.Text.Substring(linkAttr.StartIndex, linkAttr.Count), UriKind.RelativeOrAbsolute, out uri);
						tag.Underline = Pango.Underline.Single;
						tag.UnderlineSet = true;
						tag.ForegroundRgba = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToGtkValue();
						tag.ForegroundSet = true;
						Links[tag] = new Link { Href = uri };
					}

					table.Add(tag);
					Buffer.GetIterAtMark(out var iterStart, attrStart);
					Buffer.ApplyTag(tag, iterStart, iterAttr);
					Buffer.DeleteMark(attrStart);
				}

				Buffer.DeleteMark(textMark);
			}

			public void EmitStartHeader(int level)
			{
				BreakParagraph();
				Buffer.GetEndIter(out var iter);
				openHeaders.Push(new StartState(Buffer.CreateMark(null, iter, true), level));
			}

			public void EmitEndHeader()
			{
				var start = openHeaders.Pop();
				Buffer.GetIterAtMark(out var iterStart, start.Mark);
				Buffer.GetEndIter(out var iterEnd);
				Buffer.ApplyTagByName(TagHeaderPrefix + start.Data, iterStart, iterEnd);
				Buffer.DeleteMark(start.Mark);
				needsParagraphBreak = true;
			}

			public void EmitStartParagraph(int indentLevel)
			{
				BreakParagraph();
			}

			public void EmitEndParagraph()
			{
				needsParagraphBreak = true;
			}

			public void EmitOpenList()
			{
				BreakParagraph();
			}

			public void EmitOpenBullet()
			{
				BreakList();
				Buffer.GetEndIter(out var iterEnd);
				var mark = Buffer.CreateMark(null, iterEnd, true);
				Buffer.Insert(iterEnd, "- ", 2);
				Buffer.GetIterAtMark(out var iterStart, mark);
				Buffer.GetEndIter(out iterEnd);
				Buffer.ApplyTagByName(TagListItem, iterStart, iterEnd);
				Buffer.DeleteMark(mark);
			}

			public void EmitCloseBullet()
			{
				needsListBreak = true;
			}

			public void EmitCloseList()
			{
				needsListBreak = false;
				needsParagraphBreak = true;
			}

			public void EmitStartLink(string href, string title)
			{
				Buffer.GetEndIter(out var iter);
				if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
					uri = null;
				openLinks.Push(new Link {
					Title = title,
					Href = uri,
					StartMark = Buffer.CreateMark(null, iter, true)
				});
			}

			public void EmitEndLink()
			{
				var link = openLinks.Pop();
				var tag = Gtk.TextTag.New(null);
				tag.Underline = Pango.Underline.Single;
				tag.UnderlineSet = true;
				tag.ForegroundRgba = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToGtkValue();
				tag.ForegroundSet = true;
				table.Add(tag);
				Buffer.GetIterAtMark(out var iterStart, link.StartMark);
				Buffer.GetEndIter(out var iterEnd);
				Buffer.ApplyTag(tag, iterStart, iterEnd);
				Links[tag] = link;
			}

			public void EmitCodeBlock(string code)
			{
				BreakParagraph();
				var text = code?.Trim() ?? string.Empty;
				Buffer.GetEndIter(out var iterEnd);
				var mark = Buffer.CreateMark(null, iterEnd, true);
				Buffer.Insert(iterEnd, text, text.Length);
				Buffer.GetIterAtMark(out var iterStart, mark);
				Buffer.GetEndIter(out iterEnd);
				Buffer.ApplyTagByName(TagPre, iterStart, iterEnd);
				Buffer.DeleteMark(mark);
				needsParagraphBreak = true;
			}

			public void EmitHorizontalRuler()
			{
				EmitText(NewLine + "-----" + NewLine, RichTextInlineStyle.Normal);
			}

			public void ApplyTag(Gtk.TextTag tag)
			{
				Buffer.GetStartIter(out var start);
				Buffer.GetEndIter(out var end);
				Buffer.ApplyTag(tag, start, end);
			}
		}
	}
}
