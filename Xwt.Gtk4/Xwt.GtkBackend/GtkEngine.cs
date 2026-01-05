using System;
using System.IO;
using System.Reflection;
using Xwt;
using Xwt.Backends;
using GLib;
using GLib.Internal;

namespace Xwt.GtkBackend
{
	public class GtkEngine : ToolkitEngineBackend
	{
		internal static Gtk.Application Application { get; private set; }

		GtkPlatformBackend platformBackend;

		public override void InitializeApplication()
		{
			if (Application == null) {
				Application = Gtk.Application.New("org.xwt.gtk4", Gio.ApplicationFlags.FlagsNone);
				Application.OnActivate += (sender, args) => { };
				if (!Application.IsRegistered)
					Application.Register(null);
			}
		}

		public override void InitializeBackends()
		{
			RegisterBackend<ContextBackendHandler, Xwt.CairoBackend.CairoContextBackendHandler>();
			RegisterBackend<GradientBackendHandler, Xwt.CairoBackend.CairoGradientBackendHandler>();
			RegisterBackend<DrawingPathBackendHandler, Xwt.CairoBackend.CairoContextBackendHandler>();
			RegisterBackend<TextLayoutBackendHandler, GtkTextLayoutBackendHandler>();
			RegisterBackend<FontBackendHandler, GtkFontBackendHandler>();
			RegisterBackend<ImageBackendHandler, GtkImageBackendHandler>();
			RegisterBackend<ImageBuilderBackendHandler, GtkImageBuilderBackendHandler>();
			RegisterBackend<ImagePatternBackendHandler, GtkImagePatternBackendHandler>();
			RegisterBackend<ClipboardBackend, GtkClipboardBackend>();
			RegisterBackend<DesktopBackend, GtkDesktopBackend>();
			RegisterBackend<KeyboardHandler, GtkKeyboardHandler>();

			RegisterBackend<IWindowBackend, WindowBackend>();
			RegisterBackend<IBoxBackend, BoxBackend>();
			RegisterBackend<IPanedBackend, PanedBackend>();
			RegisterBackend<ICanvasBackend, CanvasBackend>();
			RegisterBackend<ICustomWidgetBackend, CustomWidgetBackend>();
			RegisterBackend<IEmbeddedWidgetBackend, EmbeddedWidgetBackend>();
			RegisterBackend<IDesignerSurfaceBackend, DesignerSurfaceBackend>();
			RegisterBackend<ILabelBackend, LabelBackend>();
			RegisterBackend<IButtonBackend, ButtonBackend>();
			RegisterBackend<IToggleButtonBackend, ToggleButtonBackend>();
			RegisterBackend<ITextEntryBackend, TextEntryBackend>();
			RegisterBackend<IComboBoxBackend, ComboBoxBackend>();
			RegisterBackend<IComboBoxEntryBackend, ComboBoxEntryBackend>();
			RegisterBackend<IScrollViewBackend, ScrollViewBackend>();
			RegisterBackend<IMenuBackend, MenuBackend>();
			RegisterBackend<IMenuItemBackend, MenuItemBackend>();
			RegisterBackend<ICheckBoxMenuItemBackend, CheckBoxMenuItemBackend>();
			RegisterBackend<IRadioButtonMenuItemBackend, RadioButtonMenuItemBackend>();
			RegisterBackend<ISeparatorMenuItemBackend, SeparatorMenuItemBackend>();
			RegisterBackend<IMenuButtonBackend, MenuButtonBackend>();
			RegisterBackend<ISpinButtonBackend, SpinButtonBackend>();
			RegisterBackend<ISliderBackend, SliderBackend>();
			RegisterBackend<IScrollbarBackend, ScrollbarBackend>();
			RegisterBackend<IScrollAdjustmentBackend, ScrollAdjustmentBackend>();
			RegisterBackend<IProgressBarBackend, ProgressBarBackend>();
			RegisterBackend<IFrameBackend, FrameBackend>();
			RegisterBackend<ISeparatorBackend, SeparatorBackend>();
			RegisterBackend<INotebookBackend, NotebookBackend>();
			RegisterBackend<IImageViewBackend, ImageViewBackend>();
			RegisterBackend<ILinkLabelBackend, LinkLabelBackend>();
			RegisterBackend<ISpinnerBackend, SpinnerBackend>();
			RegisterBackend<IExpanderBackend, ExpanderBackend>();
			RegisterBackend<ICheckBoxBackend, CheckBoxBackend>();
			RegisterBackend<IRadioButtonBackend, RadioButtonBackend>();
			RegisterBackend<IPasswordEntryBackend, PasswordEntryBackend>();
			RegisterBackend<ISearchTextEntryBackend, SearchTextEntryBackend>();
			RegisterBackend<ICalendarBackend, CalendarBackend>();
			RegisterBackend<IDatePickerBackend, DatePickerBackend>();
			RegisterBackend<IColorSelectorBackend, ColorSelectorBackend>();
			RegisterBackend<IColorPickerBackend, ColorPickerBackend>();
			RegisterBackend<IFontSelectorBackend, FontSelectorBackend>();
			RegisterBackend<IListBoxBackend, ListBoxBackend>();
			RegisterBackend<IDialogBackend, DialogBackend>();
			RegisterBackend<IAlertDialogBackend, AlertDialogBackend>();
			RegisterBackend<IOpenFileDialogBackend, OpenFileDialogBackend>();
			RegisterBackend<ISaveFileDialogBackend, SaveFileDialogBackend>();
			RegisterBackend<ISelectFolderDialogBackend, SelectFolderDialogBackend>();
			RegisterBackend<ISelectColorDialogBackend, SelectColorDialogBackend>();
			RegisterBackend<ISelectFontDialogBackend, SelectFontDialogBackend>();
			RegisterBackend<IFileSelectorBackend, FileSelectorBackend>();
			RegisterBackend<IFolderSelectorBackend, FolderSelectorBackend>();
			RegisterBackend<IAccessibleBackend, AccessibleBackend>();
			RegisterBackend<IListStoreBackend, DefaultListStoreBackend>();
			RegisterBackend<ITreeStoreBackend, TreeStoreBackend>();
			RegisterBackend<IListViewBackend, ListViewBackend>();
			RegisterBackend<ITreeViewBackend, TreeViewBackend>();
			RegisterBackend<IPopoverBackend, PopoverBackend>();
			RegisterBackend<IPopupWindowBackend, PopupWindowBackend>();
			RegisterBackend<IUtilityWindowBackend, UtilityWindowBackend>();
			RegisterBackend<ISegmentedButtonBackend, SegmentedButtonBackend>();
			RegisterBackend<IRichTextViewBackend, RichTextViewBackend>();
			RegisterBackend<IStatusIconBackend, StatusIconBackend>();
			RegisterBackend<IWebViewBackend, WebViewBackend>();

			string typeName = null;
			string asmName = null;
			if (Platform.IsMac) {
				typeName = "Xwt.Gtk.Mac.MacPlatformBackend";
				asmName = "Xwt.Gtk.Mac";
			} else if (Platform.IsWindows) {
				typeName = "Xwt.Gtk.Windows.WindowsPlatformBackend";
				asmName = "Xwt.Gtk.Windows";
			}

			if (typeName != null) {
				var loc = Path.GetDirectoryName(GetType().Assembly.Location);
				loc = Path.Combine(loc, asmName + ".dll");

				Assembly asm = null;
				try {
					asm = File.Exists(loc) ? Assembly.LoadFrom(loc) : Assembly.Load(asmName);
				} catch {
				}

				Type platformType = asm != null ? asm.GetType(typeName) : null;
				if (platformType != null) {
					platformBackend = (GtkPlatformBackend)Activator.CreateInstance(platformType);
					platformBackend.Initialize(this);
				}
			}
		}

		public override void RunApplication()
		{
			Application?.RunWithSynchronizationContext(null);
		}

		public override void ExitApplication()
		{
			Application?.Quit();
		}

		public override void Dispose()
		{
			GtkTextLayoutBackendHandler.DisposeResources();
			base.Dispose();
		}

		public override void InvokeAsync(Action action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
				action();
				return false;
			}));
		}


		public override object TimerInvoke(Func<bool> action, System.TimeSpan timeSpan)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			if (timeSpan.TotalMilliseconds < 0)
				throw new ArgumentException("Timer period must be >=0", nameof(timeSpan));

			return GLib.Functions.TimeoutAdd(GLib.Constants.PRIORITY_DEFAULT, (uint)timeSpan.TotalMilliseconds, new GLib.SourceFunc(() => action()));
		}

		public override void CancelTimerInvoke(object id)
		{
			if (id == null)
				throw new ArgumentNullException(nameof(id));
			GLib.Functions.SourceRemove((uint)id);
		}

		public override object GetNativeWidget(Widget w)
		{
			var wb = (IGtkWidgetBackend)Toolkit.GetBackend(w);
			return wb.Widget;
		}

		public override object GetNativeWindow(IWindowFrameBackend backend)
		{
			return backend?.Window;
		}

		public override IWindowFrameBackend GetBackendForWindow(object nativeWindow)
		{
			var win = new WindowFrameBackend();
			win.Window = (Gtk.Window)nativeWindow;
			return win;
		}

		public override void DispatchPendingEvents()
		{
			var context = GLib.Functions.MainContextDefault();
			if (context == null)
				return;
			int n = 1000;
			while (context.Pending() && --n > 0)
				context.Iteration(false);
		}

		public override bool HasNativeParent(Widget w)
		{
			var wb = (IGtkWidgetBackend)Toolkit.GetBackend(w);
			return wb.Widget?.Parent != null;
		}

		protected override Type GetBackendImplementationType(Type backendType)
		{
			if (platformBackend != null) {
				var bt = platformBackend.GetBackendImplementationType(backendType);
				if (bt != null)
					return bt;
			}
			return base.GetBackendImplementationType(backendType);
		}
	}

	public interface IGtkWidgetBackend
	{
		Gtk.Widget Widget { get; }
	}
}
