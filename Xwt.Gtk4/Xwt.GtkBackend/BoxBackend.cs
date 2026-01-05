using System;
using System.Collections.Generic;
using Gsk;
using Graphene;
using Gtk.Internal;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class BoxBackend : WidgetBackend, IBoxBackend
	{
		Gtk.Box container;
		readonly Dictionary<Gtk.Widget, Rectangle> allocations = new Dictionary<Gtk.Widget, Rectangle>();
		Gtk.CustomLayout layout;
		readonly Gtk.CustomRequestModeFunc requestModeFunc;
		readonly Gtk.CustomMeasureFunc measureFunc;
		readonly Gtk.CustomAllocateFunc allocateFunc;
		readonly CustomRequestModeFuncCallHandler requestModeHandler;
		readonly CustomMeasureFuncCallHandler measureHandler;
		readonly CustomAllocateFuncCallHandler allocateHandler;
		int allocatedWidth = -1;
		int allocatedHeight = -1;
		bool isAllocating;

		public BoxBackend()
		{
			container = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
			requestModeFunc = HandleRequestMode;
			measureFunc = HandleMeasure;
			allocateFunc = HandleAllocate;
			requestModeHandler = new CustomRequestModeFuncCallHandler(requestModeFunc);
			measureHandler = new CustomMeasureFuncCallHandler(measureFunc);
			allocateHandler = new CustomAllocateFuncCallHandler(allocateFunc);
			var layoutHandle = CustomLayout.New(requestModeHandler.NativeCallback, measureHandler.NativeCallback, allocateHandler.NativeCallback);
			layout = new Gtk.CustomLayout(new CustomLayoutHandle(layoutHandle, true));
			container.LayoutManager = layout;
			Widget = container;
			Widget.Hexpand = true;
			Widget.Vexpand = true;
			Widget.Show();
		}

		public void Add(IWidgetBackend widget)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			container.Append(child);
			WidgetBackend.ApplyChildPlacement(widget);
			container.QueueResize();
		}

		public void Remove(IWidgetBackend widget)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			container.Remove(child);
			allocations.Remove(child);
			container.QueueResize();
		}

		public void SetAllocation(IWidgetBackend[] widgets, Rectangle[] rect)
		{
			allocations.Clear();
			for (int i = 0; i < widgets.Length; i++) {
				var child = ((IGtkWidgetBackend)widgets[i]).Widget;
				var r = rect[i];
				allocations[child] = r;
			}
			if (!isAllocating)
				container.QueueAllocate();
		}

		public override Size Size {
			get {
				if (allocatedWidth >= 0 && allocatedHeight >= 0)
					return new Size(allocatedWidth, allocatedHeight);
				return base.Size;
			}
		}

		void SetAllocatedSize(int width, int height)
		{
			allocatedWidth = width;
			allocatedHeight = height;
		}

		void AllocateChildren()
		{
			var child = container.GetFirstChild();
			while (child != null) {
				if (allocations.TryGetValue(child, out var rect)) {
					var width = (int)rect.Width;
					var height = (int)rect.Height;
					Gsk.Transform transform = null;
					if (rect.X != 0 || rect.Y != 0) {
						var point = new Graphene.Point {
							X = (float)rect.X,
							Y = (float)rect.Y
						};
						transform = Gsk.Transform.New().Translate(point);
					}
					child.Allocate(width, height, -1, transform);
					transform?.Dispose();
				}
				child = child.GetNextSibling();
			}
		}

		Gtk.SizeRequestMode HandleRequestMode(Gtk.Widget widget)
		{
			return Gtk.SizeRequestMode.HeightForWidth;
		}

		void HandleMeasure(Gtk.Widget widget, Gtk.Orientation orientation, int forSize, out int minimum, out int natural, out int minimumBaseline, out int naturalBaseline)
		{
			if (Frontend == null) {
				minimum = natural = 0;
				minimumBaseline = naturalBaseline = -1;
				return;
			}

			var widthConstraint = SizeConstraint.Unconstrained;
			var heightConstraint = SizeConstraint.Unconstrained;
			if (orientation == Gtk.Orientation.Horizontal) {
				if (forSize >= 0)
					heightConstraint = SizeConstraint.WithSize(forSize);
			} else {
				if (forSize >= 0)
					widthConstraint = SizeConstraint.WithSize(forSize);
			}

			var size = Frontend.Surface.GetPreferredSize(widthConstraint, heightConstraint, true);
			int target = orientation == Gtk.Orientation.Horizontal
				? (int)size.Width
				: (int)size.Height;
			minimum = natural = target;
			minimumBaseline = naturalBaseline = -1;
		}

		void HandleAllocate(Gtk.Widget widget, int width, int height, int baseline)
		{
			SetAllocatedSize(width, height);
			if (Frontend == null)
				return;
			isAllocating = true;
			Frontend.Surface.Reallocate();
			isAllocating = false;
			AllocateChildren();
		}
	}
}
