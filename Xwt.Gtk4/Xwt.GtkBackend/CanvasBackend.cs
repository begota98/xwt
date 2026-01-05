using System;
using System.Collections.Generic;
using Xwt.Backends;
using Xwt.Drawing;
using Xwt.CairoBackend;

namespace Xwt.GtkBackend
{
	public class CanvasBackend : WidgetBackend, ICanvasBackend
	{
		readonly Gtk.Overlay overlay;
		readonly Gtk.DrawingArea drawingArea;
		readonly Gtk.Fixed fixedContainer;
		readonly Dictionary<Gtk.Widget, Rectangle> children = new Dictionary<Gtk.Widget, Rectangle>();
		ICanvasEventSink canvasEventSink;

		public CanvasBackend()
		{
			drawingArea = Gtk.DrawingArea.New();
			fixedContainer = Gtk.Fixed.New();
			overlay = Gtk.Overlay.New();

			overlay.Child = drawingArea;
			overlay.AddOverlay(fixedContainer);

			overlay.Hexpand = true;
			overlay.Vexpand = true;
			drawingArea.Hexpand = true;
			drawingArea.Vexpand = true;
			fixedContainer.Hexpand = true;
			fixedContainer.Vexpand = true;

			drawingArea.SetDrawFunc(HandleDraw);

			drawingArea.Visible = true;
			fixedContainer.Visible = true;
			overlay.Visible = true;

			Widget = overlay;
		}

		protected new ICanvasEventSink EventSink => canvasEventSink;

		public override void Initialize(IWidgetEventSink sink)
		{
			base.Initialize(sink);
			canvasEventSink = (ICanvasEventSink)sink;
		}

		public void QueueDraw()
		{
			drawingArea.QueueDraw();
		}

		public void QueueDraw(Rectangle rect)
		{
			drawingArea.QueueDraw();
		}

		public void AddChild(IWidgetBackend widget, Rectangle bounds)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			children[child] = bounds;
			fixedContainer.Put(child, bounds.X, bounds.Y);
			child.SetSizeRequest((int)Math.Max(0, bounds.Width), (int)Math.Max(0, bounds.Height));
			child.Visible = true;
		}

		public void SetChildBounds(IWidgetBackend widget, Rectangle bounds)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			if (!children.ContainsKey(child))
				throw new InvalidOperationException("Widget is not a child of this canvas.");
			children[child] = bounds;
			fixedContainer.Move(child, bounds.X, bounds.Y);
			child.SetSizeRequest((int)Math.Max(0, bounds.Width), (int)Math.Max(0, bounds.Height));
		}

		public void RemoveChild(IWidgetBackend widget)
		{
			var child = ((IGtkWidgetBackend)widget).Widget;
			if (!children.Remove(child))
				throw new InvalidOperationException("Widget is not a child of this canvas.");
			fixedContainer.Remove(child);
		}

		void HandleDraw(Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
		{
			if (EventSink == null)
				return;
			var context = new CairoContextBackend(area.ScaleFactor) {
				Context = cr
			};
			var rect = new Rectangle(0, 0, width, height);
			if (ApplicationContext != null)
				ApplicationContext.InvokeUserCode(() => EventSink.OnDraw(context, rect));
			else
				EventSink.OnDraw(context, rect);
		}
	}
}
