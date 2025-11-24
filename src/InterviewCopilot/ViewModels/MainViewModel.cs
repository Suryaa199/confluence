using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using InterviewCopilot.Services;

namespace InterviewCopilot.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _liveQuestion = "";
    private string _liveAnswer = "";
    private string _audioStatus = "Idle";
    private string _asrStatus = "Idle";
    private string _llmStatus = "Idle";
    private Services.Orchestrator? _orchestrator;
    private InterviewCopilot.Windows.OverlayWindow? _overlay;
    private bool _paused;
    private Services.AudioOptions? _lastOptions;
    private bool _onboardingVisible;
    private string _apiStatus = "Unknown";
    private bool _isCapturing;
    private bool _isInterviewView;
    private bool _isCheatView;
    private bool _isStoriesView;
    private bool _isSettingsView;
    private string _cheatSheetText = string.Empty;
    private double _peakLevel;
    private string _storyQuery = string.Empty;
    private int? _storyDaysFilter;

    public string LiveQuestion { get => _liveQuestion; set { _liveQuestion = value; OnPropertyChanged(); } }
    public string LiveAnswer { get => _liveAnswer; set { _liveAnswer = value; OnPropertyChanged(); } }
    public string AudioStatus { get => _audioStatus; set { _audioStatus = value; OnPropertyChanged(); } }
    public string AsrStatus { get => _asrStatus; set { _asrStatus = value; OnPropertyChanged(); } }
    public string LlmStatus { get => _llmStatus; set { _llmStatus = value; OnPropertyChanged(); } }

    public ObservableCollection<string> FollowUps { get; } = new();

    public ObservableCollection<string> AudioSources { get; } = new(new[] { "PerApp", "System", "Microphone" });
    private string _selectedAudioSource = "System";
    public string SelectedAudioSource { get => _selectedAudioSource; set { _selectedAudioSource = value; OnPropertyChanged(); RefreshEndpoints(); } }

    public ObservableCollection<string> DevicePreferences { get; } = new(new[] { "Default", "Communications" });
    private string _selectedDevicePreference = "Communications";
    public string SelectedDevicePreference { get => _selectedDevicePreference; set { _selectedDevicePreference = value; OnPropertyChanged(); } }

    public ObservableCollection<string> SessionHints { get; } = new(new[] { "None", "Teams", "Zoom", "Meet", "Browser" });
    private string _selectedSessionHint = "None";
    public string SelectedSessionHint { get => _selectedSessionHint; set { _selectedSessionHint = value; OnPropertyChanged(); } }

    private double _level;
    public double Level { get => _level; set { _level = value; OnPropertyChanged(); } }
    public bool OnboardingVisible { get => _onboardingVisible; set { _onboardingVisible = value; OnPropertyChanged(); } }
    public string ApiStatus { get => _apiStatus; set { _apiStatus = value; OnPropertyChanged(); } }
    public bool IsCapturing { get => _isCapturing; set { _isCapturing = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleCaptureLabel)); } }
    public string ToggleCaptureLabel => IsCapturing ? "Stop Listening" : "Start Listening";
    public bool IsInterviewView { get => _isInterviewView; set { _isInterviewView = value; OnPropertyChanged(); } }
    public bool IsCheatView { get => _isCheatView; set { _isCheatView = value; OnPropertyChanged(); } }
    public bool IsStoriesView { get => _isStoriesView; set { _isStoriesView = value; OnPropertyChanged(); } }
    public bool IsSettingsView { get => _isSettingsView; set { _isSettingsView = value; OnPropertyChanged(); } }
    public double PeakLevel { get => _peakLevel; set { _peakLevel = value; OnPropertyChanged(); } }
    public string StoryQuery { get => _storyQuery; set { _storyQuery = value; OnPropertyChanged(); } }
    public int? StoryDaysFilter { get => _storyDaysFilter; set { _storyDaysFilter = value; OnPropertyChanged(); } }

    public ObservableCollection<DeviceItem> AudioEndpoints { get; } = new();
    private DeviceItem? _selectedAudioEndpoint;
    public DeviceItem? SelectedAudioEndpoint { get => _selectedAudioEndpoint; set { _selectedAudioEndpoint = value; OnPropertyChanged(); } }

    public ICommand ToggleCaptureCommand { get; }
    public ICommand ToggleOverlayCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand TestLevelsCommand { get; }
    public ICommand CopyAnswerCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand ToggleClickThroughCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand OpenPerAppPickerCommand { get; }
    public ICommand SaveKeyCommand { get; }
    public ICommand ShowInterviewCommand { get; }
    public ICommand ShowCheatCommand { get; }
    public ICommand ShowStoriesCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand StorySearchCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand ClearFilterCommand { get; }
    private string _openAiKeyInput = string.Empty;
    public ObservableCollection<string> StorySearchResults { get; } = new();
    public string CheatSheetText { get => _cheatSheetText; set { _cheatSheetText = value; OnPropertyChanged(); } }
    public string OpenAiKeyInput { get => _openAiKeyInput; set { _openAiKeyInput = value; OnPropertyChanged(); } }

    public MainViewModel()
    {
        ToggleCaptureCommand = new RelayCommand(_ => ToggleCapture());
        ToggleOverlayCommand = new RelayCommand(_ => ToggleOverlay());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        TestLevelsCommand = new RelayCommand(async _ => await TestLevelsAsync(), _ => _orchestrator is null);
        CopyAnswerCommand = new RelayCommand(_ => CopyAnswer(), _ => !string.IsNullOrEmpty(LiveAnswer));
        RegenerateCommand = new RelayCommand(async _ => await RegenerateAsync(), _ => _orchestrator is not null || !string.IsNullOrEmpty(LiveQuestion));
        Services.AppServices.Audio.OnLevel += v => { Level = v; PeakLevel = Math.Max(v, PeakLevel * 0.9); };
        ToggleClickThroughCommand = new RelayCommand(_ => ToggleClickThrough());
        PauseCommand = new RelayCommand(async _ => await TogglePauseAsync());
        OpenPerAppPickerCommand = new RelayCommand(_ => OpenPerAppPicker());
        SaveKeyCommand = new RelayCommand(_ => SaveKey());
        ShowInterviewCommand = new RelayCommand(_ => SetView("interview"));
        ShowCheatCommand = new RelayCommand(_ => SetView("cheat"));
        ShowStoriesCommand = new RelayCommand(_ => SetView("stories"));
        ShowSettingsCommand = new RelayCommand(_ => SetView("settings"));
        StorySearchCommand = new RelayCommand(async _ => await StorySearchAsync());
        SetFilterCommand = new RelayCommand(async d => { if (d is string s && int.TryParse(s, out var days)) StoryDaysFilter = days; await StorySearchAsync(); });
        ClearFilterCommand = new RelayCommand(async _ => { StoryDaysFilter = null; await StorySearchAsync(); });
        RefreshEndpoints();
        RefreshStatus();
        SetView("interview");
    }

    private void RefreshStatus()
    {
        var s = Services.AppServices.LoadSettings();
        var isLocal = string.Equals(s.LlmProvider, "Ollama", StringComparison.OrdinalIgnoreCase) || string.Equals(s.AsrProvider, "Local", StringComparison.OrdinalIgnoreCase);
        var hasKey = Services.AppServices.HasOpenAiKey();
        ApiStatus = isLocal ? "Local providers configured" : (hasKey ? "OpenAI key saved" : "OpenAI key missing");
        OnboardingVisible = !isLocal && !hasKey;
    }

    private async void Start()
    {
        RefreshStatus();
        AudioStatus = "Capturing";
        AsrStatus = "Listening";
        LlmStatus = "Ready";
        LiveQuestion = "";
        LiveAnswer = "";
        FollowUps.Clear();
        IsCapturing = true;

        _orchestrator = Services.AppServices.CreateOrchestrator();
        _orchestrator.OnTranscript += text =>
        {
            LiveQuestion += (LiveQuestion.Length > 0 ? " " : "") + text;
        };
        _orchestrator.OnAnswerToken += tok =>
        {
            LiveAnswer += tok;
            _overlay?.SetAnswer(LiveAnswer);
        };
        _orchestrator.OnFollowUps += list =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                FollowUps.Clear();
                foreach (var f in list) FollowUps.Add(f);
                // Save story when follow-ups arrive (answer considered complete)
                _ = Services.AppServices.Stories.SaveAsync(LiveQuestion, LiveAnswer, DateTimeOffset.Now);
                var s = Services.AppServices.LoadSettings();
                if (s.SpeakAnswers)
                {
                    _ = Services.AppServices.Tts.SpeakAsync(LiveAnswer, CancellationToken.None);
                }
            });
        };

        var options = new Services.AudioOptions
        {
            Source = SelectedAudioSource switch
            {
                "PerApp" => Services.AudioSourceKind.PerApp,
                "Microphone" => Services.AudioSourceKind.Microphone,
                _ => Services.AudioSourceKind.System
            },
            Device = SelectedDevicePreference == "Communications" ? Services.DevicePreference.Communications : Services.DevicePreference.Default,
            Session = SelectedSessionHint switch
            {
                "Teams" => Services.SessionHint.Teams,
                "Zoom" => Services.SessionHint.Zoom,
                "Meet" => Services.SessionHint.Meet,
                "Browser" => Services.SessionHint.Browser,
                _ => Services.SessionHint.None
            },
            EndpointId = SelectedAudioEndpoint?.Id,
            PreferredProcessName = Services.AppServices.LoadSettings().PreferredProcessName
        };
        _lastOptions = options;
        await _orchestrator.StartAsync(options);
    }

    private async void Stop()
    {
        AudioStatus = "Stopped";
        AsrStatus = "Idle";
        LlmStatus = "Idle";
        if (_orchestrator is not null)
        {
            await _orchestrator.StopAsync();
            _orchestrator = null;
        }
        IsCapturing = false;
    }

    private void ToggleCapture()
    {
        if (IsCapturing) Stop(); else Start();
    }

    private async Task TestLevelsAsync()
    {
        // Simple 3-second capture to display levels only
        try
        {
            var options = new Services.AudioOptions
            {
                Source = SelectedAudioSource switch
                {
                    "PerApp" => Services.AudioSourceKind.PerApp,
                    "Microphone" => Services.AudioSourceKind.Microphone,
                    _ => Services.AudioSourceKind.System
                },
                Device = SelectedDevicePreference == "Communications" ? Services.DevicePreference.Communications : Services.DevicePreference.Default,
                EndpointId = SelectedAudioEndpoint?.Id
            };
            await Services.AppServices.Audio.StartAsync(options);
            await Task.Delay(3000);
        }
        finally
        {
            await Services.AppServices.Audio.StopAsync();
        }
    }

    private void CopyAnswer()
    {
        try { System.Windows.Clipboard.SetText(LiveAnswer ?? string.Empty); } catch { }
    }

    private async Task RegenerateAsync()
    {
        var ctx = BuildContextFromSettings();
        var q = string.IsNullOrWhiteSpace(LiveQuestion) ? "Give a concise summary of my strengths." : LiveQuestion;
        LiveAnswer = string.Empty;
        try
        {
            await foreach (var token in Services.AppServices.Llm.StreamAnswerAsync(q, ctx, CancellationToken.None))
            {
                LiveAnswer += token;
            }
        }
        catch (Exception ex)
        {
            LiveAnswer += $"\n[LLM error: {ex.Message}]";
        }
    }

    private static string BuildContextFromSettings()
    {
        var settings = Services.AppServices.LoadSettings();
        var context = string.Empty;
        if (settings.Keywords is { Length: > 0 }) context += "Keywords: " + string.Join(", ", settings.Keywords) + "\n";
        if (!string.IsNullOrWhiteSpace(settings.CompanyBlurb)) context += "Company: " + settings.CompanyBlurb + "\n";
        return context;
    }

    private async Task TogglePauseAsync()
    {
        if (_orchestrator is null) return;
        if (!_paused)
        {
            await Services.AppServices.Audio.StopAsync();
            _paused = true;
            AudioStatus = "Paused";
        }
        else
        {
            if (_lastOptions is not null)
            {
                await Services.AppServices.Audio.StartAsync(_lastOptions);
                AudioStatus = "Capturing";
            }
            _paused = false;
        }
    }

    private void RefreshEndpoints()
    {
        AudioEndpoints.Clear();
        var src = SelectedAudioSource switch
        {
            "PerApp" => Services.AudioSourceKind.PerApp,
            "Microphone" => Services.AudioSourceKind.Microphone,
            _ => Services.AudioSourceKind.System
        };
        foreach (var ep in Services.AppServices.Audio.ListEndpoints(src))
        {
            AudioEndpoints.Add(new DeviceItem { Id = ep.Id, Name = ep.Name });
        }
        SelectedAudioEndpoint = AudioEndpoints.FirstOrDefault();
    }

    public class DeviceItem
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public override string ToString() => Name;
    }

    private void ToggleOverlay()
    {
        if (_overlay is null)
        {
            _overlay = new InterviewCopilot.Windows.OverlayWindow();
            _overlay.Owner = System.Windows.Application.Current.MainWindow;
            _overlay.Show();
        }
        else
        {
            if (_overlay.IsVisible) _overlay.Hide(); else _overlay.Show();
        }
    }

    private void ToggleClickThrough()
    {
        if (_overlay is null)
        {
            _overlay = new InterviewCopilot.Windows.OverlayWindow();
            _overlay.Owner = System.Windows.Application.Current.MainWindow;
            _overlay.Show();
        }
        _overlay.ToggleClickThrough();
    }

    private void OpenSettings()
    {
        var w = new InterviewCopilot.Windows.SettingsWindow();
        w.Owner = System.Windows.Application.Current.MainWindow;
        w.ShowDialog();
    }

    private void OpenPerAppPicker()
    {
        var w = new InterviewCopilot.Windows.PerAppPickerWindow();
        w.Owner = System.Windows.Application.Current.MainWindow;
        w.ShowDialog();
    }

    private void SaveKey()
    {
        try
        {
            var key = (OpenAiKeyInput ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key)) return;
            var secrets = new Services.DpapiSecretStore();
            secrets.SaveSecret("OpenAI:ApiKey", key);
            OpenAiKeyInput = string.Empty;
            RefreshStatus();
        }
        catch { }
    }

    private void SetView(string v)
    {
        IsInterviewView = v == "interview";
        IsCheatView = v == "cheat";
        IsStoriesView = v == "stories";
        IsSettingsView = v == "settings";
        if (IsCheatView) LoadCheatSheet();
        if (IsStoriesView) _ = StorySearchAsync();
    }

    private void LoadCheatSheet()
    {
        var s = Services.AppServices.LoadSettings();
        CheatSheetText = s.CheatSheet ?? string.Empty;
    }

    private async Task StorySearchAsync()
    {
        StorySearchResults.Clear();
        var list = await Services.AppServices.Stories.SearchAsync(StoryQuery ?? string.Empty);
        if (StoryDaysFilter.HasValue)
        {
            var cutoff = DateTimeOffset.Now.AddDays(-StoryDaysFilter.Value);
            list = list.Where(x => x.At >= cutoff).ToList();
        }
        foreach (var it in list.OrderByDescending(x => x.At))
        {
            var snippet = it.Answer.Length > 80 ? it.Answer.Substring(0, 80) + "..." : it.Answer;
            StorySearchResults.Add($"{it.At:u} | {it.Question} -> {snippet}");
        }
    }

    private void SetView(string v)
    {
        IsInterviewView = v == "interview";
        IsCheatView = v == "cheat";
        IsStoriesView = v == "stories";
        IsSettingsView = v == "settings";
        if (IsCheatView) LoadCheatSheet();
        if (IsStoriesView) _ = StorySearchAsync();
    }

    private void LoadCheatSheet()
    {
        var s = Services.AppServices.LoadSettings();
        _cheatSheetText = s.CheatSheet ?? string.Empty;
        OnPropertyChanged(nameof(CheatSheetText));
    }

    private async Task StorySearchAsync()
    {
        StorySearchResults.Clear();
        var list = await Services.AppServices.Stories.SearchAsync("");
        foreach (var it in list.OrderByDescending(x => x.At))
        {
            var snippet = it.Answer.Length > 80 ? it.Answer.Substring(0, 80) + "..." : it.Answer;
            StorySearchResults.Add($"{it.At:u} | {it.Question} -> {snippet}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
}
