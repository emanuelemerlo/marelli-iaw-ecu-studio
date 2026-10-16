namespace MarelliIawEcuStudio.Presentation.Services;

/// <summary>Dialogs the view models need; implemented by the UI layer.</summary>
public interface IDialogService
{
    /// <summary>Asks the user to confirm an action. Returns true for OK.</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Asks for a byte value (0-255), starting from <paramref name="current"/>. Null when cancelled.</summary>
    Task<byte?> AskByteAsync(string title, byte current);

    /// <summary>Lets the user pick a log file to open. Null when cancelled.</summary>
    Task<string?> PickLogFileAsync(string? startFolder);

    Task ShowMessageAsync(string title, string message);
}

/// <summary>Runs code on the UI thread.</summary>
public interface IUiDispatcher
{
    void Post(Action action);

    Task<T> InvokeAsync<T>(Func<Task<T>> action);
}

/// <summary>Runs everything inline; for tests and console use.</summary>
public sealed class InlineDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();

    public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
}
