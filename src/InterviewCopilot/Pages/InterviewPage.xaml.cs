using System.Windows.Controls;
using InterviewCopilot.Services;

namespace InterviewCopilot.Pages;

public partial class InterviewPage : Page
{
    public InterviewPage()
    {
        InitializeComponent();
        var s = AppServices.LoadSettings();
        CheatText.Text = s.CheatSheet ?? "";
    }
}
