using System;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class GtkTextLayoutBackendHandler : TextLayoutBackendHandler
	{
		internal class PangoBackend : IDisposable
		{
			public Pango.Layout Layout { get; set; }
			public string Text { get; set; } = string.Empty;
			public TextIndexer Indexer { get; set; } = new TextIndexer(string.Empty);
			public double Height { get; set; } = -1;
			public Pango.AttrList Attributes { get; set; }

			public void Dispose()
			{
				Layout?.Dispose();
				Layout = null;
				Attributes?.Dispose();
				Attributes = null;
			}
		}

		public static void DisposeResources()
		{
		}

		public override object Create()
		{
			var ctx = Pango.Context.New();
			var layout = Pango.Layout.New(ctx);
			return new PangoBackend {
				Layout = layout
			};
		}

		public override void SetText(object backend, string text)
		{
			var tl = (PangoBackend)backend;
			tl.Text = text ?? string.Empty;
			tl.Indexer = new TextIndexer(tl.Text);
			tl.Attributes?.Dispose();
			tl.Attributes = null;
			tl.Layout.SetText(tl.Text, -1);
		}

		public override void SetFont(object backend, Font font)
		{
			var tl = (PangoBackend)backend;
			var fontBackend = ApplicationContext.Toolkit.GetSafeBackend(font);
			if (fontBackend is Pango.FontDescription desc)
				tl.Layout.SetFontDescription(desc);
		}

		public override void SetWidth(object backend, double value)
		{
			var tl = (PangoBackend)backend;
			tl.Layout.SetWidth((int)(value * Pango.Constants.SCALE));
		}

		public override void SetHeight(object backend, double value)
		{
			((PangoBackend)backend).Height = value;
		}

		public override void SetAlignment(object backend, Alignment alignment)
		{
			var tl = (PangoBackend)backend;
			tl.Layout.SetAlignment(ToPangoAlignment(alignment));
		}

		public override void SetTrimming(object backend, TextTrimming textTrimming)
		{
			var tl = (PangoBackend)backend;
			switch (textTrimming) {
				case TextTrimming.WordElipsis:
					tl.Layout.SetEllipsize(Pango.EllipsizeMode.End);
					break;
				default:
					tl.Layout.SetEllipsize(Pango.EllipsizeMode.None);
					break;
			}
		}

		public override Size GetSize(object backend)
		{
			var tl = (PangoBackend)backend;
			int w;
			int h;
			tl.Layout.GetPixelSize(out w, out h);
			return new Size(w, h);
		}

		public override void AddAttribute(object backend, TextAttribute attribute)
		{
			if (attribute == null)
				return;

			var tl = (PangoBackend)backend;
			if (tl.Attributes == null)
				tl.Attributes = Pango.AttrList.New();

			var start = (uint)tl.Indexer.IndexToByteIndex(attribute.StartIndex);
			var end = (uint)tl.Indexer.IndexToByteIndex(attribute.StartIndex + attribute.Count);

			void AddToList(Pango.Attribute attr)
			{
				if (attr == null)
					return;
				attr.StartIndex = start;
				attr.EndIndex = end;
				tl.Attributes.Insert(attr);
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
				var backendFont = ApplicationContext.Toolkit.GetSafeBackend(font.Font) as Pango.FontDescription;
				if (backendFont != null)
					AddToList(Pango.Functions.AttrFontDescNew(backendFont));
			} else if (attribute is LinkTextAttribute) {
				var color = Toolkit.CurrentEngine.Defaults.FallbackLinkColor;
				AddToList(Pango.Functions.AttrUnderlineNew(Pango.Underline.Single));
				AddToList(Pango.Functions.AttrForegroundNew(ToPangoColor(color.Red), ToPangoColor(color.Green), ToPangoColor(color.Blue)));
			}

			tl.Layout.SetAttributes(tl.Attributes);
		}

		public override void ClearAttributes(object backend)
		{
			var tl = (PangoBackend)backend;
			tl.Attributes?.Dispose();
			tl.Attributes = null;
			tl.Layout.SetAttributes(null);
		}

		public override int GetIndexFromCoordinates(object backend, double x, double y)
		{
			var tl = (PangoBackend)backend;
			int index;
			int trailing;
			var success = tl.Layout.XyToIndex((int)(x * Pango.Constants.SCALE), (int)(y * Pango.Constants.SCALE), out index, out trailing);
			if (!success)
				return tl.Text.Length;
			return tl.Indexer.ByteIndexToIndex(index);
		}

		public override Point GetCoordinateFromIndex(object backend, int index)
		{
			var tl = (PangoBackend)backend;
			int byteIndex = tl.Indexer.IndexToByteIndex(index);
			Pango.Rectangle pos = new Pango.Rectangle();
			tl.Layout.IndexToPos(byteIndex, out pos);
			return new Point(pos.X / (double)Pango.Constants.SCALE, pos.Y / (double)Pango.Constants.SCALE);
		}

		public override double GetBaseline(object backend)
		{
			var tl = (PangoBackend)backend;
			return tl.Layout.GetBaseline() / (double)Pango.Constants.SCALE;
		}

		public override double GetMeanline(object backend)
		{
			var tl = (PangoBackend)backend;
			var baseline = GetBaseline(backend);
			var ctx = tl.Layout.GetContext();
			var desc = tl.Layout.GetFontDescription();
			if (ctx == null || desc == null)
				return baseline;

			using var metrics = ctx.GetMetrics(desc, Pango.Language.GetDefault());
			if (metrics == null)
				return baseline;
			var strike = metrics.GetStrikethroughPosition();
			return baseline - strike / (double)Pango.Constants.SCALE;
		}

		public override void Dispose(object backend)
		{
			((IDisposable)backend).Dispose();
		}

		static Pango.Alignment ToPangoAlignment(Alignment alignment)
		{
			switch (alignment) {
				case Alignment.Center:
					return Pango.Alignment.Center;
				case Alignment.End:
					return Pango.Alignment.Right;
				default:
					return Pango.Alignment.Left;
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
