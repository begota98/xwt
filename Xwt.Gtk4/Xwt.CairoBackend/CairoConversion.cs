using System;
using Xwt.Drawing;

namespace Xwt.CairoBackend
{
	public static class CairoConversion
	{
		public static void SelectFont (this Cairo.Context ctx, Font font)
		{
			Cairo.FontSlant slant;
			switch (font.Style) {
			case FontStyle.Oblique: slant = Cairo.FontSlant.Oblique; break;
			case FontStyle.Italic: slant = Cairo.FontSlant.Italic; break;
			default: slant = Cairo.FontSlant.Normal; break;
			}
			
			Cairo.FontWeight w = font.Weight >= FontWeight.Bold ? Cairo.FontWeight.Bold : Cairo.FontWeight.Normal;
			
			ctx.SelectFontFace (font.Family, slant, w);
		}

		public static void RoundedRectangle(this Cairo.Context cr, double x, double y, double w, double h, double r)
		{
			if(r < 0.0001) {
				cr.Rectangle(x, y, w, h);
				return;
			}

			cr.MoveTo(x + r, y);
			cr.Arc(x + w - r, y + r, r, Math.PI * 1.5, Math.PI * 2);
			cr.Arc(x + w - r, y + h - r, r, 0, Math.PI * 0.5);
			cr.Arc(x + r, y + h - r, r, Math.PI * 0.5, Math.PI);
			cr.Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
		}
	}
}

