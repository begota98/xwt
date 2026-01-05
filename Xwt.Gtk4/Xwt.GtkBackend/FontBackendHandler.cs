using System;
using System.Collections.Generic;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class GtkFontBackendHandler : FontBackendHandler
	{
		public override object GetSystemDefaultFont()
		{
			return Create("Sans", 10, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
		}

		public override IEnumerable<string> GetInstalledFonts()
		{
			return Array.Empty<string>();
		}

		public override IEnumerable<KeyValuePair<string, object>> GetAvailableFamilyFaces(string family)
		{
			return Array.Empty<KeyValuePair<string, object>>();
		}

		public override object Create(string fontName, double size, FontStyle style, FontWeight weight, FontStretch stretch)
		{
			var desc = Pango.FontDescription.New();
			desc.SetFamily(fontName ?? "Sans");
			desc.SetSize((int)(size * Pango.Constants.SCALE));
			desc.SetStyle(ToPangoStyle(style));
			desc.SetWeight((Pango.Weight)(int)weight);
			desc.SetStretch(ToPangoStretch(stretch));
			return desc;
		}

		public override bool RegisterFontFromFile(string fontPath)
		{
			return false;
		}

		public override object Copy(object handle)
		{
			var fd = (Pango.FontDescription)handle;
			return fd.Copy();
		}

		public override object SetSize(object handle, double size)
		{
			var fd = (Pango.FontDescription)handle;
			fd.SetSize((int)(size * Pango.Constants.SCALE));
			return fd;
		}

		public override object SetFamily(object handle, string family)
		{
			var fd = (Pango.FontDescription)handle;
			fd.SetFamily(family ?? "Sans");
			return fd;
		}

		public override object SetStyle(object handle, FontStyle style)
		{
			var fd = (Pango.FontDescription)handle;
			fd.SetStyle(ToPangoStyle(style));
			return fd;
		}

		public override object SetWeight(object handle, FontWeight weight)
		{
			var fd = (Pango.FontDescription)handle;
			fd.SetWeight((Pango.Weight)(int)weight);
			return fd;
		}

		public override object SetStretch(object handle, FontStretch stretch)
		{
			var fd = (Pango.FontDescription)handle;
			fd.SetStretch(ToPangoStretch(stretch));
			return fd;
		}

		public override double GetSize(object handle)
		{
			var fd = (Pango.FontDescription)handle;
			return fd.GetSize() / (double)Pango.Constants.SCALE;
		}

		public override string GetFamily(object handle)
		{
			return ((Pango.FontDescription)handle).GetFamily() ?? string.Empty;
		}

		public override FontStyle GetStyle(object handle)
		{
			var fd = (Pango.FontDescription)handle;
			return FromPangoStyle(fd.GetStyle());
		}

		public override FontWeight GetWeight(object handle)
		{
			var fd = (Pango.FontDescription)handle;
			return (FontWeight)(int)fd.GetWeight();
		}

		public override FontStretch GetStretch(object handle)
		{
			var fd = (Pango.FontDescription)handle;
			return FromPangoStretch(fd.GetStretch());
		}

		static Pango.Style ToPangoStyle(FontStyle style)
		{
			switch (style) {
				case FontStyle.Oblique:
					return Pango.Style.Oblique;
				case FontStyle.Italic:
					return Pango.Style.Italic;
				default:
					return Pango.Style.Normal;
			}
		}

		static FontStyle FromPangoStyle(Pango.Style style)
		{
			switch (style) {
				case Pango.Style.Oblique:
					return FontStyle.Oblique;
				case Pango.Style.Italic:
					return FontStyle.Italic;
				default:
					return FontStyle.Normal;
			}
		}

		static Pango.Stretch ToPangoStretch(FontStretch stretch)
		{
			switch (stretch) {
				case FontStretch.UltraCondensed:
					return Pango.Stretch.UltraCondensed;
				case FontStretch.ExtraCondensed:
					return Pango.Stretch.ExtraCondensed;
				case FontStretch.Condensed:
					return Pango.Stretch.Condensed;
				case FontStretch.SemiCondensed:
					return Pango.Stretch.SemiCondensed;
				case FontStretch.SemiExpanded:
					return Pango.Stretch.SemiExpanded;
				case FontStretch.Expanded:
					return Pango.Stretch.Expanded;
				case FontStretch.ExtraExpanded:
					return Pango.Stretch.ExtraExpanded;
				case FontStretch.UltraExpanded:
					return Pango.Stretch.UltraExpanded;
				default:
					return Pango.Stretch.Normal;
			}
		}

		static FontStretch FromPangoStretch(Pango.Stretch stretch)
		{
			switch (stretch) {
				case Pango.Stretch.UltraCondensed:
					return FontStretch.UltraCondensed;
				case Pango.Stretch.ExtraCondensed:
					return FontStretch.ExtraCondensed;
				case Pango.Stretch.Condensed:
					return FontStretch.Condensed;
				case Pango.Stretch.SemiCondensed:
					return FontStretch.SemiCondensed;
				case Pango.Stretch.SemiExpanded:
					return FontStretch.SemiExpanded;
				case Pango.Stretch.Expanded:
					return FontStretch.Expanded;
				case Pango.Stretch.ExtraExpanded:
					return FontStretch.ExtraExpanded;
				case Pango.Stretch.UltraExpanded:
					return FontStretch.UltraExpanded;
				default:
					return FontStretch.Normal;
			}
		}
	}
}
