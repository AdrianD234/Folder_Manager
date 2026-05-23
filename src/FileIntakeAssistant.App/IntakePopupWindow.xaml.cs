using System.ComponentModel;
using System.Windows;
using FileIntakeAssistant.App.ViewModels;

namespace FileIntakeAssistant.App;

public partial class IntakePopupWindow : Window
{
    private readonly IntakeCandidatePopupViewModel _viewModel;
    private bool _dismissInProgress;

    public IntakePopupWindow(IntakeCandidatePopupViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsComplete || _dismissInProgress)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _dismissInProgress = true;
        try
        {
            await _viewModel.SkipAsync("Candidate popup was closed without saving.").ConfigureAwait(true);
        }
        finally
        {
            _dismissInProgress = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        base.OnClosed(e);
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        Close();
    }
}
