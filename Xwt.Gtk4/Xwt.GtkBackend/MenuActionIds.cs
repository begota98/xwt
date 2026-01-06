using System.Threading;

namespace Xwt.GtkBackend
{
	static class MenuActionIds
	{
		static int nextId;

		public static int NextId()
		{
			return Interlocked.Increment(ref nextId);
		}
	}
}
