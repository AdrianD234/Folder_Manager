using System.Windows;
using System.ComponentModel;
using FileIntakeAssistant.App.ViewModels;
using Microsoft.Win32;

namespace FileIntakeAssistant.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    public void ShowIntake()
    {
        MainTabs.SelectedItem = IntakeTab;
        ShowAndActivate();
    }

    public void ShowSearch()
    {
        MainTabs.SelectedItem = SearchTab;
        ShowAndActivate();
        SearchCommandTextBox.Focus();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Manual.FilePath = dialog.FileName;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }
}
