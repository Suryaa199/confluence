using System.Windows;
using Microsoft.Win32;
using System.Net.Http;
using System.Linq;
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
        VadMinBox.Text = s.VadMinVoiceMs.ToString();
        VadMaxBox.Text = s.VadMaxSilenceMs.ToString();
        ChatModelBox.SelectedItem = null;
        foreach (var item in ChatModelBox.Items)
        {
            if ((item as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == s.ChatModel)
            { ChatModelBox.SelectedItem = item; break; }
        }
        if (ChatModelBox.SelectedItem == null) ChatModelBox.SelectedIndex = 0;
        AsrModelBox.Text = s.AsrModel ?? "whisper-1";
        ResumeBox.Text = s.ResumeText ?? string.Empty;
        JdBox.Text = s.JobDescText ?? string.Empty;
        CheatBox.Text = s.CheatSheet ?? string.Empty;
        PreferredProcBox.Text = s.PreferredProcessName ?? string.Empty;
        SpeakAnswersBox.IsChecked = s.SpeakAnswers;
        TtsCommBox.IsChecked = s.TtsUseCommunications;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var s = _settings.Load();
        if (int.TryParse(ChunkSizeBox.Text, out var chunk)) s.ChunkSizeMs = Math.Clamp(chunk, 250, 2000);
        s.EnableSileroVad = UseSilero.IsChecked == true;
        if (int.TryParse(VadMinBox.Text, out var vmin)) s.VadMinVoiceMs = Math.Clamp(vmin, 50, 2000);
        if (int.TryParse(VadMaxBox.Text, out var vmax)) s.VadMaxSilenceMs = Math.Clamp(vmax, 200, 3000);
        s.Keywords = (KeywordsBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        s.CompanyBlurb = CompanyBox.Text ?? string.Empty;
        s.ChatModel = (ChatModelBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? s.ChatModel;
        s.AsrModel = AsrModelBox.Text ?? s.AsrModel;
        s.ResumeText = ResumeBox.Text ?? string.Empty;
        s.JobDescText = JdBox.Text ?? string.Empty;
        s.CheatSheet = CheatBox.Text ?? string.Empty;
        s.PreferredProcessName = PreferredProcBox.Text ?? string.Empty;
        s.SpeakAnswers = SpeakAnswersBox.IsChecked == true;
        s.TtsUseCommunications = TtsCommBox.IsChecked == true;
        _settings.Save(s);
        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            _secrets.SaveSecret("OpenAI:ApiKey", ApiKeyBox.Password.Trim());
        }
        MessageBox.Show(this, "Saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private async void OnSearchStories(object sender, RoutedEventArgs e)
    {
        var q = StorySearchBox.Text ?? string.Empty;
        var list = await InterviewCopilot.Services.AppServices.Stories.SearchAsync(q);
        StoryList.ItemsSource = list.Select(x => $"{x.At:u} | {x.Question} -> {x.Answer.Substring(0, Math.Min(80, x.Answer.Length))}...");
    }

    private void OnPickApp(object sender, RoutedEventArgs e)
    {
        var w = new PerAppPickerWindow();
        w.Owner = this;
        w.ShowDialog();
        var s = _settings.Load();
        PreferredProcBox.Text = s.PreferredProcessName ?? string.Empty;
    }
    private void OnTestKey(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show(this, "Enter a key to test.");
            return;
        }
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            var res = http.GetAsync("https://api.openai.com/v1/models").GetAwaiter().GetResult();
            if (res.IsSuccessStatusCode)
                MessageBox.Show(this, "Key is valid.", "API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(this, $"Key test failed: {(int)res.StatusCode} {res.ReasonPhrase}", "API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Key test error: {ex.Message}", "API Key", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnLoadResume(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text Files|*.txt|All Files|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            try { ResumeBox.Text = System.IO.File.ReadAllText(dlg.FileName); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        }
    }

    private void OnLoadJd(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text Files|*.txt|All Files|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            try { JdBox.Text = System.IO.File.ReadAllText(dlg.FileName); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        }
    }

    private async void OnGenerateCheatSheet(object sender, RoutedEventArgs e)
    {
        var s = _settings.Load();
        string ctx = "";
        if (s.Keywords is { Length: > 0 }) ctx += "Keywords: " + string.Join(", ", s.Keywords) + "\n";
        if (!string.IsNullOrWhiteSpace(s.CompanyBlurb)) ctx += "Company: " + s.CompanyBlurb + "\n";
        if (!string.IsNullOrWhiteSpace(s.ResumeText)) ctx += "Resume: " + s.ResumeText + "\n";
        if (!string.IsNullOrWhiteSpace(s.JobDescText)) ctx += "JobDesc: " + s.JobDescText + "\n";
        CheatBox.Text = "Generating cheat sheet...";
        try
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var tok in InterviewCopilot.Services.AppServices.Llm.StreamAnswerAsync(
                "Generate a concise company-role cheat sheet with bullet points and key talking points.", ctx, System.Threading.CancellationToken.None))
            {
                sb.Append(tok);
                CheatBox.Text = sb.ToString();
            }
            s.CheatSheet = sb.ToString();
            _settings.Save(s);
        }
        catch (Exception ex)
        {
            CheatBox.Text = "Error: " + ex.Message;
        }
    }
}
