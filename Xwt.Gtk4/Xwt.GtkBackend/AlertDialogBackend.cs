using System;
using System.Collections.Generic;
using System.Linq;
using Xwt;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class AlertDialogBackend : IAlertDialogBackend
	{
		ApplicationContext context;

		public bool ApplyToAll { get; private set; }

		public void Initialize(ApplicationContext context)
		{
			this.context = context;
		}

		public Command Run(WindowFrame transientFor, MessageDescription message)
		{
			ApplyToAll = false;
			if (!RequiresCustomDialog(message))
				return RunSimpleDialog(transientFor, message);

			return RunCustomDialog(transientFor, message);
		}

		Command RunSimpleDialog(WindowFrame transientFor, MessageDescription message)
		{
			var dialog = new Gtk.AlertDialog();
			GetDialogText(message, out var primaryText, out var secondaryText);
			dialog.SetMessage(primaryText ?? string.Empty);
			dialog.SetDetail(secondaryText ?? string.Empty);
			dialog.SetModal(true);

			var buttons = message.Buttons.Count > 0
				? message.Buttons.Select(b => b.Label ?? string.Empty).ToArray()
				: new[] { Command.Ok.Label };
			dialog.SetButtons(buttons);

			var defaultIndex = message.DefaultButton >= 0 ? message.DefaultButton : buttons.Length - 1;
			if (defaultIndex >= 0 && defaultIndex < buttons.Length)
				dialog.SetDefaultButton(defaultIndex);

			var parent = transientFor != null ? ((WindowFrameBackend)Toolkit.CurrentEngine.GetSafeBackend(transientFor)).Window as Gtk.Window : null;
			var loop = GLib.MainLoop.New(null, false);
			int resultIndex = -1;

			dialog.ChooseAsync(parent).ContinueWith(task => {
				resultIndex = task.Result;
				GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT_IDLE, new GLib.SourceFunc(() => {
					loop.Quit();
					return false;
				}));
			});

			loop.Run();

			if (message.Buttons.Count == 0)
				return Command.Ok;
			if (resultIndex < 0 || resultIndex >= message.Buttons.Count)
				return message.Buttons.FirstOrDefault();
			return message.Buttons[resultIndex];
		}

		Command RunCustomDialog(WindowFrame transientFor, MessageDescription message)
		{
			int resultIndex = -1;

			GetDialogText(message, out var primaryText, out var secondaryText);

			var window = Gtk.Window.New();
			window.Modal = true;
			window.Title = message.Title ?? string.Empty;
			GtkEngine.Application?.AddWindow(window);

			var parent = transientFor != null ? ((WindowFrameBackend)Toolkit.CurrentEngine.GetSafeBackend(transientFor)).Window as Gtk.Window : null;
			if (parent != null)
				window.TransientFor = parent;

			var mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 12);
			mainBox.MarginStart = 12;
			mainBox.MarginEnd = 12;
			mainBox.MarginTop = 12;
			mainBox.MarginBottom = 12;
			mainBox.Hexpand = true;
			mainBox.Vexpand = true;
			mainBox.Show();

			var contentBox = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
			contentBox.Hexpand = true;
			contentBox.Vexpand = true;
			contentBox.Show();
			mainBox.Append(contentBox);

			var messageRow = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
			messageRow.Hexpand = true;
			messageRow.Show();

			var textBox = Gtk.Box.New(Gtk.Orientation.Vertical, 4);
			textBox.Hexpand = true;
			textBox.Show();

			if (!string.IsNullOrEmpty(primaryText)) {
				var primaryLabel = Gtk.Label.New(string.Empty);
				primaryLabel.Wrap = true;
				primaryLabel.Xalign = 0;
				primaryLabel.UseMarkup = true;
				primaryLabel.Label_ = $"<span weight=\"bold\" size=\"larger\">{EscapeMarkup(primaryText)}</span>";
				primaryLabel.Show();
				textBox.Append(primaryLabel);
			}

			if (!string.IsNullOrEmpty(secondaryText)) {
				var secondaryLabel = Gtk.Label.New(secondaryText);
				secondaryLabel.Wrap = true;
				secondaryLabel.Xalign = 0;
				secondaryLabel.Show();
				textBox.Append(secondaryLabel);
			}

			var iconWidget = CreateIconWidget(message.Icon);
			if (iconWidget != null) {
				iconWidget.MarginTop = 2;
				messageRow.Append(iconWidget);
			}

			messageRow.Append(textBox);
			contentBox.Append(messageRow);

			var optionButtons = new List<(AlertOption option, Gtk.CheckButton button)>();
			if (message.Options != null) {
				foreach (var option in message.Options) {
					var check = Gtk.CheckButton.New();
					check.Label = option.Text ?? string.Empty;
					check.Active = option.Value;
					check.Show();
					contentBox.Append(check);
					optionButtons.Add((option, check));
				}
			}

			Gtk.CheckButton applyCheck = null;
			if (message.AllowApplyToAll) {
				applyCheck = Gtk.CheckButton.New();
				applyCheck.Label = Application.TranslationCatalog.GetString("Apply to all");
				applyCheck.Active = false;
				applyCheck.Show();
				contentBox.Append(applyCheck);
			}

			var buttonBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
			buttonBox.Halign = Gtk.Align.End;
			buttonBox.Show();

			var buttons = message.Buttons.Count > 0
				? message.Buttons.Select(b => b.Label ?? string.Empty).ToArray()
				: new[] { Command.Ok.Label };

			for (int i = 0; i < buttons.Length; i++) {
				var button = Gtk.Button.New();
				button.Label = buttons[i];
				var command = message.Buttons.Count > i ? message.Buttons[i] : null;
				if (command?.Icon != null) {
					var buttonIcon = CreateButtonIconWidget(command.Icon);
					if (buttonIcon != null) {
						var label = Gtk.Label.New(buttons[i]);
						label.UseUnderline = true;
						label.Show();
						var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
						box.Append(buttonIcon);
						box.Append(label);
						box.Show();
						button.Child = box;
					}
				}
				int index = i;
				button.OnClicked += (sender, args) => {
					resultIndex = index;
					window.Close();
				};
				var defaultIndex = message.DefaultButton >= 0 ? message.DefaultButton : buttons.Length - 1;
				if (defaultIndex == i) {
					button.ReceivesDefault = true;
					button.GrabFocus();
				}
				button.Show();
				buttonBox.Append(button);
			}

			mainBox.Append(buttonBox);
			window.Child = mainBox;

			var loop = GLib.MainLoop.New(null, false);
			window.OnCloseRequest += (sender, args) => {
				window.Hide();
				loop.Quit();
				return true;
			};

			window.Present();
			loop.Run();
			window.Dispose();

			foreach (var (option, button) in optionButtons)
				option.Value = button.Active;
			ApplyToAll = applyCheck?.Active ?? false;

			if (message.Buttons.Count == 0)
				return Command.Ok;
			if (resultIndex < 0 || resultIndex >= message.Buttons.Count)
				return message.Buttons.FirstOrDefault();
			return message.Buttons[resultIndex];
		}

		public void Dispose()
		{
		}

		static bool RequiresCustomDialog(MessageDescription message)
		{
			if (message == null)
				return false;
			if (message.AllowApplyToAll)
				return true;
			if (message.Options != null && message.Options.Count > 0)
				return true;
			if (message.Icon != null)
				return true;
			if (message.Buttons != null && message.Buttons.Any(b => b?.Icon != null))
				return true;
			return false;
		}

		static void GetDialogText(MessageDescription message, out string primaryText, out string secondaryText)
		{
			if (message == null) {
				primaryText = string.Empty;
				secondaryText = string.Empty;
				return;
			}

			if (string.IsNullOrEmpty(message.Text)) {
				primaryText = message.SecondaryText ?? string.Empty;
				secondaryText = string.Empty;
				return;
			}

			primaryText = message.Text ?? string.Empty;
			secondaryText = message.SecondaryText ?? string.Empty;
		}

		Gtk.Widget CreateIconWidget(Xwt.Drawing.Image icon)
		{
			if (icon == null || context == null)
				return null;
			var desc = icon.ToImageDescription(context);
			if (!(desc.Backend is GtkImage gtkImage))
				return null;

			Gtk.Image imageWidget = null;
			if (!string.IsNullOrEmpty(gtkImage.IconName)) {
				imageWidget = Gtk.Image.NewFromIconName(gtkImage.IconName);
				imageWidget.PixelSize = 48;
			} else if (gtkImage.Pixbuf != null) {
				imageWidget = Gtk.Image.NewFromPaintable(Gdk.Texture.NewForPixbuf(gtkImage.Pixbuf));
				imageWidget.PixelSize = 48;
			}

			if (imageWidget == null) {
				var fallback = gtkImage.CreateWidget(context, desc);
				fallback.Show();
				return fallback;
			}

			imageWidget.Halign = Gtk.Align.Start;
			imageWidget.Valign = Gtk.Align.Start;
			imageWidget.Show();
			return imageWidget;
		}

		Gtk.Widget CreateButtonIconWidget(Xwt.Drawing.Image icon)
		{
			if (icon == null || context == null)
				return null;
			var desc = icon.ToImageDescription(context);
			if (!(desc.Backend is GtkImage gtkImage))
				return null;

			Gtk.Image imageWidget = null;
			if (!string.IsNullOrEmpty(gtkImage.IconName)) {
				imageWidget = Gtk.Image.NewFromIconName(gtkImage.IconName);
				imageWidget.PixelSize = 16;
			} else if (gtkImage.Pixbuf != null) {
				imageWidget = Gtk.Image.NewFromPaintable(Gdk.Texture.NewForPixbuf(gtkImage.Pixbuf));
				imageWidget.PixelSize = 16;
			}

			if (imageWidget == null) {
				var fallback = gtkImage.CreateWidget(context, desc);
				fallback.Show();
				return fallback;
			}

			imageWidget.Show();
			return imageWidget;
		}

		static string EscapeMarkup(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;
			return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
		}
	}
}
