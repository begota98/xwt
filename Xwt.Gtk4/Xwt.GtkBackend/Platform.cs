using System;
using System.Runtime.InteropServices;
using System.IO;

namespace Xwt.GtkBackend
{
	public static class Platform
	{
		public readonly static bool IsWindows;
		public readonly static bool IsMac;

		static Platform()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac();
		}

		static bool IsRunningOnMac()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal(8192);
				if (uname(buf) == 0) {
					string os = Marshal.PtrToStringAnsi(buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal(buf);
			}
			return false;
		}

		[DllImport("libc")]
		static extern int uname(IntPtr buf);
	}
}
