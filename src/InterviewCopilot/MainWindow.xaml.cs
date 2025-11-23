using System.Windows;
using InterviewCopilot.ViewModels;

namespace InterviewCopilot;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}

