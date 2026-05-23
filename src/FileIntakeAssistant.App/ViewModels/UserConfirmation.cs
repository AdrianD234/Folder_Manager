using System.Windows;

namespace FileIntakeAssistant.App.ViewModels;

public interface IUserConfirmationService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);
}

public sealed class MessageBoxUserConfirmationService : IUserConfirmationService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
