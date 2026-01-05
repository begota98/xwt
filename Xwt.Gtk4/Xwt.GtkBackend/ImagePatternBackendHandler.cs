using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class GtkImagePatternBackendHandler : ImagePatternBackendHandler
	{
		public override object Create(ImageDescription img)
		{
			return new ImagePatternBackend {
				Image = img
			};
		}

		public override void Dispose(object img)
		{
			var pb = img as ImagePatternBackend;
			pb?.Dispose();
		}
	}

	class ImagePatternBackend
	{
		public ImageDescription Image;
		Cairo.Pattern pattern;

		public Cairo.Pattern GetPattern(ApplicationContext context, double scaleFactor)
		{
			return pattern;
		}

		public void Dispose()
		{
			pattern?.Dispose();
			pattern = null;
		}
	}
}
