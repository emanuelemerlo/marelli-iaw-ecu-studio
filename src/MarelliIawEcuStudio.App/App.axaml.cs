using Avalonia;
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
            desktop.MainWindow = window;
            desktop.ShutdownRequested += async (_, _) => await vm.DisposeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
