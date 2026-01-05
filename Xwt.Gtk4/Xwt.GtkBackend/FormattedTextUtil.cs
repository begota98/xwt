using Xwt;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	static class FormattedTextUtil
	{
		public static void ApplyFormattedText(Gtk.Label label, FormattedText text)
		{
			if (label == null)
				return;

			if (text == null) {
				label.UseMarkup = false;
				label.Label_ = string.Empty;
				label.SetAttributes(null);
				return;
			}

			label.UseMarkup = false;
			label.Label_ = text.Text ?? string.Empty;

			using var attrs = Pango.AttrList.New();
			var indexer = new TextIndexer(text.Text ?? string.Empty);
			foreach (var attr in text.Attributes)
				AddAttribute(attrs, indexer, attr);
			label.SetAttributes(attrs);
		}

		static void AddAttribute(Pango.AttrList list, TextIndexer indexer, TextAttribute attribute)
		{
			if (attribute == null)
				return;

			var start = (uint)indexer.IndexToByteIndex(attribute.StartIndex);
			var end = (uint)indexer.IndexToByteIndex(attribute.StartIndex + attribute.Count);

			void AddToList(Pango.Attribute attr)
			{
				if (attr == null)
					return;
				attr.StartIndex = start;
				attr.EndIndex = end;
				list.Insert(attr);
			}

			if (attribute is BackgroundTextAttribute background) {
				var color = background.Color;
				AddToList(Pango.Functions.AttrBackgroundNew(ToPangoColor(color.Red), ToPangoColor(color.Green), ToPangoColor(color.Blue)));
			} else if (attribute is ColorTextAttribute foreground) {
				var color = foreground.Color;
				AddToList(Pango.Functions.AttrForegroundNew(ToPangoColor(color.Red), ToPangoColor(color.Green), ToPangoColor(color.Blue)));
			} else if (attribute is FontWeightTextAttribute weight) {
				AddToList(Pango.Functions.AttrWeightNew((Pango.Weight)(int)weight.Weight));
			} else if (attribute is FontSizeTextAttribute size) {
				AddToList(Pango.Functions.AttrSizeNewAbsolute((int)(size.Size * Pango.Constants.SCALE)));
			} else if (attribute is FontStyleTextAttribute style) {
				AddToList(Pango.Functions.AttrStyleNew((Pango.Style)(int)style.Style));
			} else if (attribute is UnderlineTextAttribute underline) {
				AddToList(Pango.Functions.AttrUnderlineNew(underline.Underline ? Pango.Underline.Single : Pango.Underline.None));
			} else if (attribute is StrikethroughTextAttribute strike) {
				AddToList(Pango.Functions.AttrStrikethroughNew(strike.Strikethrough));
			} else if (attribute is FontTextAttribute font) {
				var backendFont = Toolkit.CurrentEngine?.GetSafeBackend(font.Font) as Pango.FontDescription;
				if (backendFont != null)
					AddToList(Pango.Functions.AttrFontDescNew(backendFont));
			} else if (attribute is LinkTextAttribute) {
				var color = Toolkit.CurrentEngine.Defaults.FallbackLinkColor;
				AddToList(Pango.Functions.AttrUnderlineNew(Pango.Underline.Single));
				AddToList(Pango.Functions.AttrForegroundNew(ToPangoColor(color.Red), ToPangoColor(color.Green), ToPangoColor(color.Blue)));
			}
		}

		static ushort ToPangoColor(double channel)
		{
			if (channel <= 0)
				return 0;
			if (channel >= 1)
				return ushort.MaxValue;
			return (ushort)(channel * ushort.MaxValue);
		}
	}
}
