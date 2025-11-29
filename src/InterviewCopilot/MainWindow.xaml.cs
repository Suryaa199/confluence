using System.Windows;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using InterviewCopilot.ViewModels;

namespace InterviewCopilot;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            DataContext = new MainViewModel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Interview Copilot could not start:\n" + ex.Message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string exe = System.IO.Path.Combine(baseDir, "InterviewCopilot.exe");
            string path = File.Exists(exe) ? exe : (System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? exe);
            var t = File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now;
            string shortHash = "";
            try
            {
                using var sha = SHA256.Create();
                if (File.Exists(path))
                {
                    using var fs = File.OpenRead(path);
                    var hash = sha.ComputeHash(fs);
                    var sb = new StringBuilder();
                    for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
                    shortHash = sb.ToString();
                }
            }
            catch { }
            this.Title = string.IsNullOrEmpty(shortHash)
                ? $"Interview Copilot — Build {t:yyyy-MM-dd HH:mm}"
                : $"Interview Copilot — {shortHash} — {t:yyyy-MM-dd HH:mm}";
        }
        catch { }
    }
}
