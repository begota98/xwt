using System;
using System.Collections.Generic;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class GtkDesktopBackend : DesktopBackend
	{
		public override Point GetMouseLocation()
		{
			var display = Gdk.Display.GetDefault();
			var seat = display?.GetDefaultSeat();
			var device = seat?.GetPointer();
			if (device == null)
				return Point.Zero;
			double x;
			double y;
			device.GetSurfaceAtPosition(out x, out y);
			return new Point(x, y);
		}

		public override IEnumerable<object> GetScreens()
		{
			var display = Gdk.Display.GetDefault();
			var monitors = display?.GetMonitors();
			if (monitors == null)
				yield break;

			var count = monitors.GetNItems();
			for (uint i = 0; i < count; i++) {
				var ptr = monitors.GetItem(i);
				if (ptr == IntPtr.Zero)
					continue;
				var handle = new Gdk.Internal.MonitorHandle(ptr, false);
				yield return new Gdk.Monitor(handle);
			}
		}

		public override bool IsPrimaryScreen(object backend)
		{
			var monitor = backend as Gdk.Monitor;
			if (monitor == null)
				return true;
			var display = Gdk.Display.GetDefault();
			var monitors = display?.GetMonitors();
			if (monitors == null || monitors.GetNItems() == 0)
				return true;
			var ptr = monitors.GetItem(0);
			if (ptr == IntPtr.Zero)
				return true;
			var primary = new Gdk.Monitor(new Gdk.Internal.MonitorHandle(ptr, false));
			return MonitorMatches(monitor, primary);
		}

		public override Rectangle GetScreenBounds(object backend)
		{
			var monitor = backend as Gdk.Monitor;
			if (monitor == null)
				return Rectangle.Zero;
			var rect = monitor.Geometry;
			return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
		}

		public override Rectangle GetScreenVisibleBounds(object backend)
		{
			return GetScreenBounds(backend);
		}

		public override string GetScreenDeviceName(object backend)
		{
			var monitor = backend as Gdk.Monitor;
			if (monitor == null)
				return string.Empty;
			return monitor.GetConnector() ?? monitor.Model ?? string.Empty;
		}

		public override double GetScaleFactor(object backend)
		{
			var monitor = backend as Gdk.Monitor;
			if (monitor == null)
				return 1d;
			return monitor.ScaleFactor;
		}

		static bool MonitorMatches(Gdk.Monitor a, Gdk.Monitor b)
		{
			if (a == null || b == null)
				return false;
			if (!string.IsNullOrEmpty(a.GetConnector()) && a.GetConnector() == b.GetConnector())
				return true;
			var rectA = a.Geometry;
			var rectB = b.Geometry;
			return rectA.X == rectB.X && rectA.Y == rectB.Y && rectA.Width == rectB.Width && rectA.Height == rectB.Height;
		}
	}
}
