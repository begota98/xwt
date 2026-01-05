using System;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class GtkPlatformBackend
	{
		public virtual void Initialize(ToolkitEngineBackend toolkit)
		{
		}

		public virtual Type GetBackendImplementationType(Type backendType)
		{
			return null;
		}
	}
}
