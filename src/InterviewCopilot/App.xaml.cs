using System.Windows;
using System.Windows.Threading;

namespace InterviewCopilot;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        Services.LogService.Initialize();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var details = e.Exception.ToString();
        MessageBox.Show("Unexpected error:\n" + details, "Interview Copilot", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
