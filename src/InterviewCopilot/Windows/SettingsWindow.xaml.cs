using System.Windows;
using InterviewCopilot.Services;

namespace InterviewCopilot.Windows;

public partial class SettingsWindow : Window
{
    private readonly ISettingsStore _settings;
    private readonly ISecretStore _secrets;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = new JsonSettingsStore();
        _secrets = new DpapiSecretStore();
        LoadState();
    }

    private void LoadState()
    {
        var s = _settings.Load();
        ChunkSizeBox.Text = s.ChunkSizeMs.ToString();
        UseSilero.IsChecked = s.EnableSileroVad;
        KeywordsBox.Text = string.Join(", ", s.Keywords ?? Array.Empty<string>());
        CompanyBox.Text = s.CompanyBlurb ?? string.Empty;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var s = _settings.Load();
        if (int.TryParse(ChunkSizeBox.Text, out var chunk)) s.ChunkSizeMs = Math.Clamp(chunk, 250, 2000);
        s.EnableSileroVad = UseSilero.IsChecked == true;
        s.Keywords = (KeywordsBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        s.CompanyBlurb = CompanyBox.Text ?? string.Empty;
        _settings.Save(s);
        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            _secrets.SaveSecret("OpenAI:ApiKey", ApiKeyBox.Password.Trim());
        }
        MessageBox.Show(this, "Saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void OnTestKey(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show(this, "Enter a key to test.");
            return;
        }
        // Placeholder: in MVP we just validate non-empty length
        if (key.Length < 10)
        {
            MessageBox.Show(this, "Key looks too short.", "API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(this, "Key format looks OK.", "API Key", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

