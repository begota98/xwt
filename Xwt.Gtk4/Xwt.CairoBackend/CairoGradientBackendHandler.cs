using System;
using Xwt.Backends;

namespace Xwt.CairoBackend
{
	public class CairoGradientBackendHandler: GradientBackendHandler
	{
		public override object CreateLinear (double x0, double y0, double x1, double y1)
		{
			return new Cairo.LinearGradient (x0, y0, x1, y1);
		}

		public override void Dispose (object backend)
		{
			((IDisposable)backend).Dispose ();
		}

		public override object CreateRadial (double cx0, double cy0, double radius0, double cx1, double cy1, double radius1)
		{
			return new Cairo.RadialGradient (cx0, cy0, radius0, cx1, cy1, radius1);
		}

		public override void AddColorStop (object backend, double position, Xwt.Drawing.Color color)
		{
			Cairo.Gradient g = (Cairo.Gradient) backend;
			g.AddColorStopRgba (position, color.Red, color.Green, color.Blue, color.Alpha);
		}

		public override bool DisposeHandleOnUiThread {
			get {
				return true;
			}
		}
	}
}
