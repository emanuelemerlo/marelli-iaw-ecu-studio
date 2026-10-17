using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MarelliIawEcuStudio.App.Services;
using MarelliIawEcuStudio.App.Views;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Presentation.Services;
using MarelliIawEcuStudio.Presentation.ViewModels;

namespace MarelliIawEcuStudio.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var registry = DefinitionRegistry.Load(DefinitionRegistry.DefaultUserDirectory);
            var vm = new MainViewModel(registry, new DefaultTransportFactory(), new JsonSettingsStore(JsonSettingsStore.DefaultPath),
                new DialogService(window), new AvaloniaDispatcher());
            window.DataContext = vm;
            RestorePlacement(window, vm);
            desktop.MainWindow = window;
            if (desktop.Args is [.., "--report-theme", var reportFile])
                ReportThemeAndExit(desktop, window, reportFile);
            desktop.ShutdownRequested += async (_, _) => await vm.DisposeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Diagnostics (<c>--report-theme file</c>): once the window is shown, writes the
    /// theme the operating system reports and the one the window uses, then exits.
    /// CI runs it with the system set to dark; users can run it when the theme looks wrong.
    /// The output goes to a file because the Windows build has no console.
    /// </summary>
    private void ReportThemeAndExit(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, string file)
    {
        window.Opened += async (_, _) =>
        {
            await Task.Delay(500);
            var platform = PlatformSettings?.GetColorValues().ThemeVariant.ToString() ?? "unknown";
            File.WriteAllText(file,
                $"platform={platform}\nrequested={RequestedThemeVariant}\napplication={ActualThemeVariant}\nwindow={window.ActualThemeVariant}\n");
            desktop.Shutdown();
        };
    }

    /// <summary>
    /// Opens the window maximised the first time, so that everything fits the
    /// screen whatever its size; later as the user left it. A saved size that
    /// does not fit the current screen (another computer, a smaller monitor)
    /// falls back to maximised.
    /// </summary>
    private static void RestorePlacement(MainWindow window, MainViewModel vm)
    {
        if (vm.WindowMaximized || vm.WindowWidth is not { } width || vm.WindowHeight is not { } height)
        {
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.Width = width;
            window.Height = height;
            window.Opened += (_, _) =>
            {
                if (window.Screens.ScreenFromWindow(window) is { } screen
                    && (width * screen.Scaling > screen.WorkingArea.Width || height * screen.Scaling > screen.WorkingArea.Height))
                    window.WindowState = WindowState.Maximized;
            };
        }
        window.Closing += (_, _) =>
            vm.RememberWindow(window.WindowState == WindowState.Maximized, window.Bounds.Width, window.Bounds.Height);
    }
}
