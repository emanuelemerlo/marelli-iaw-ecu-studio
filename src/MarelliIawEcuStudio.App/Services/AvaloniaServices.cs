using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.Services;

namespace MarelliIawEcuStudio.App.Services;

internal sealed class AvaloniaDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task<T> InvokeAsync<T>(Func<Task<T>> action) => Dispatcher.UIThread.InvokeAsync(action);
}

/// <summary>Modal dialogs built in code (confirmation, byte value, message) and the file picker.</summary>
internal sealed class DialogService(Window owner) : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, out var buttons);
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = Ui.Get("Cancel"), IsCancel = true, MinWidth = 90 };
        ok.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<byte?> AskByteAsync(string title, byte current)
    {
        var dialog = CreateDialog(title, null, out var buttons, out var body);
        var slider = new Slider { Minimum = 0, Maximum = 255, Value = current, TickFrequency = 1, IsSnapToTickEnabled = true, MinWidth = 320 };
        var number = new NumericUpDown { Minimum = 0, Maximum = 255, Value = current, Increment = 1, FormatString = "0", Width = 140 };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                number.Value = (decimal)slider.Value;
        };
        number.ValueChanged += (_, _) => slider.Value = (double)(number.Value ?? 0);
        body.Children.Add(slider);
        body.Children.Add(number);
        var ok = new Button { Content = Ui.Get("Apply"), IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = Ui.Get("Cancel"), IsCancel = true, MinWidth = 90 };
        ok.Click += (_, _) => dialog.Close((byte?)(byte)Math.Clamp((int)(number.Value ?? 0), 0, 255));
        cancel.Click += (_, _) => dialog.Close(null);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        return await dialog.ShowDialog<byte?>(owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, out var buttons);
        var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 90 };
        ok.Click += (_, _) => dialog.Close();
        buttons.Children.Add(ok);
        await dialog.ShowDialog(owner);
    }

    public async Task<string?> PickLogFileAsync(string? startFolder)
    {
        var storage = owner.StorageProvider;
        IStorageFolder? start = null;
        if (startFolder is not null && Directory.Exists(startFolder))
            start = await storage.TryGetFolderFromPathAsync(startFolder);
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Ui.Get("OpenLog"),
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static Window CreateDialog(string title, string? message, out StackPanel buttons) =>
        CreateDialog(title, message, out buttons, out _);

    private static Window CreateDialog(string title, string? message, out StackPanel buttons, out StackPanel body)
    {
        body = new StackPanel { Spacing = 12 };
        if (message is not null)
            body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 });
        buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var root = new DockPanel { Margin = new Avalonia.Thickness(20) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        buttons.Margin = new Avalonia.Thickness(0, 16, 0, 0);
        root.Children.Add(buttons);
        root.Children.Add(body);
        return new Window
        {
            Title = title,
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 360,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
    }
}
