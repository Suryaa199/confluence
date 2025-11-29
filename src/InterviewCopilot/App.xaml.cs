using System.Windows;
using System.Windows.Threading;

namespace InterviewCopilot;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Unexpected error: " + e.Exception.Message, "Interview Copilot", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
