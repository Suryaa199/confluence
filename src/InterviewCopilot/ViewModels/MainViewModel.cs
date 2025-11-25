using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Windows.Threading;
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
    private string _vadStatus = string.Empty;
    private bool _isCapturing;
    private bool _isInterviewView;
    private bool _isCheatView;
    private bool _isStoriesView;
    private bool _isSettingsView;
    private string _cheatSheetText = string.Empty;
    private double _peakLevel;
    private string _storyQuery = string.Empty;
    private int? _storyDaysFilter;
    private readonly StringBuilder _answerBuffer = new();
    private readonly DispatcherTimer _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
    private bool _isAnswerStreaming;
    private bool _speakAnswersEnabled;

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
    public string VadStatus { get => _vadStatus; set { _vadStatus = value; OnPropertyChanged(); } }
    public bool IsCapturing { get => _isCapturing; set { _isCapturing = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleCaptureLabel)); } }
    public string ToggleCaptureLabel => IsCapturing ? "Stop Listening" : "Start Listening";
    public bool IsInterviewView { get => _isInterviewView; set { _isInterviewView = value; OnPropertyChanged(); } }
    public bool IsCheatView { get => _isCheatView; set { _isCheatView = value; OnPropertyChanged(); } }
    public bool IsStoriesView { get => _isStoriesView; set { _isStoriesView = value; OnPropertyChanged(); } }
    public bool IsSettingsView { get => _isSettingsView; set { _isSettingsView = value; OnPropertyChanged(); } }
    public double PeakLevel { get => _peakLevel; set { _peakLevel = value; OnPropertyChanged(); } }
    public string StoryQuery { get => _storyQuery; set { _storyQuery = value; OnPropertyChanged(); } }
    public int? StoryDaysFilter { get => _storyDaysFilter; set { _storyDaysFilter = value; OnPropertyChanged(); } }
    public bool IsAnswerStreaming { get => _isAnswerStreaming; set { _isAnswerStreaming = value; OnPropertyChanged(); } }
    public bool SpeakAnswersEnabled { get => _speakAnswersEnabled; set { _speakAnswersEnabled = value; OnPropertyChanged(); } }

    // Provider selectors
    public ObservableCollection<string> LlmProviders { get; } = new(new[] { "OpenAI", "Ollama" });
    private string _selectedLlmProvider = "OpenAI";
    public string SelectedLlmProvider { get => _selectedLlmProvider; set { if (_selectedLlmProvider != value) { _selectedLlmProvider = value; OnPropertyChanged(); ApplyLlmProvider(value); } } }

    public ObservableCollection<string> AsrProviders { get; } = new(new[] { "OpenAI", "Local" });
    private string _selectedAsrProvider = "OpenAI";
    public string SelectedAsrProvider { get => _selectedAsrProvider; set { if (_selectedAsrProvider != value) { _selectedAsrProvider = value; OnPropertyChanged(); ApplyAsrProvider(value); } } }

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
    public ICommand TakeFollowUpCommand { get; }
    public ICommand CopyTranscriptCommand { get; }
    public ICommand ToggleSpeakAnswersCommand { get; }
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
        TakeFollowUpCommand = new RelayCommand(async p =>
        {
            if (p is string f && !string.IsNullOrWhiteSpace(f))
            {
                LiveQuestion = (LiveQuestion?.Length > 0 ? LiveQuestion + " " : string.Empty) + f;
                await RegenerateAsync();
            }
        });
        CopyTranscriptCommand = new RelayCommand(_ => { try { System.Windows.Clipboard.SetText(LiveQuestion ?? string.Empty); } catch { } });
        ToggleSpeakAnswersCommand = new RelayCommand(_ => ToggleSpeakAnswers());
        RefreshEndpoints();
        RefreshStatus();
        SetView("interview");
        _flushTimer.Tick += (s, e) => FlushAnswerBuffer();
        SpeakAnswersEnabled = Services.AppServices.LoadSettings().SpeakAnswers;

        // Initialize provider selectors from settings
        var s = Services.AppServices.LoadSettings();
        SelectedLlmProvider = string.Equals(s.LlmProvider, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama" : "OpenAI";
        SelectedAsrProvider = string.Equals(s.AsrProvider, "Local", StringComparison.OrdinalIgnoreCase) ? "Local" : "OpenAI";
    }

    private void RefreshStatus()
    {
        var s = Services.AppServices.LoadSettings();
        var hasKey = Services.AppServices.HasOpenAiKey();
        var llmProv = string.Equals(s.LlmProvider, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama" : "OpenAI";
        var asrProv = string.Equals(s.AsrProvider, "Local", StringComparison.OrdinalIgnoreCase) ? "Local" : "OpenAI";
        var llmKeyPart = llmProv == "OpenAI" ? (hasKey ? "(key available)" : "(key missing)") : string.Empty;
        var asrKeyPart = (asrProv == "OpenAI" && llmProv != "OpenAI") ? (hasKey ? "(key available)" : "(key missing)") : string.Empty;
        ApiStatus = $"LLM: {llmProv} {llmKeyPart} | ASR: {asrProv} {asrKeyPart}".Trim();
        OnboardingVisible = (llmProv == "OpenAI" || asrProv == "OpenAI") && !hasKey;
        var vad = Services.AppServices.Vad;
        var isSilero = vad?.GetType().Name?.Contains("Silero", StringComparison.OrdinalIgnoreCase) == true && vad.Enabled;
        VadStatus = isSilero ? "VAD: Silero" : "VAD: Energy";
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
            lock (_answerBuffer)
            {
                _answerBuffer.Append(tok);
            }
            if (!_flushTimer.IsEnabled) _flushTimer.Start();
            IsAnswerStreaming = true;
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
                IsAnswerStreaming = false;
            });
        };
        _orchestrator.OnAsrError += msg => AsrStatus = "Error: " + msg;
        _orchestrator.OnLlmError += msg => LlmStatus = "Error: " + msg;

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

        // Auto-open overlay when starting listening
        App.Current.Dispatcher.Invoke(() =>
        {
            if (_overlay is null || !_overlay.IsVisible)
            {
                _overlay = new InterviewCopilot.Windows.OverlayWindow();
                _overlay.Owner = System.Windows.Application.Current.MainWindow;
                _overlay.Show();
            }
        });
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
        lock (_answerBuffer) _answerBuffer.Clear();
        IsAnswerStreaming = true;
        try
        {
            await foreach (var token in Services.AppServices.Llm.StreamAnswerAsync(q, ctx, CancellationToken.None))
            {
                lock (_answerBuffer) _answerBuffer.Append(token);
                if (!_flushTimer.IsEnabled) _flushTimer.Start();
            }
            IsAnswerStreaming = false;
        }
        catch (Exception ex)
        {
            LiveAnswer += $"\n[LLM error: {ex.Message}]";
            IsAnswerStreaming = false;
        }
    }

    private void FlushAnswerBuffer()
    {
        string? toApply = null;
        lock (_answerBuffer)
        {
            if (_answerBuffer.Length > 0)
            {
                toApply = _answerBuffer.ToString();
                _answerBuffer.Clear();
            }
        }
        if (toApply is null)
        {
            _flushTimer.Stop();
            return;
        }
        LiveAnswer += toApply;
        _overlay?.SetAnswer(LiveAnswer);
    }

    public ObservableCollection<string> Presets { get; } = new(new[] { "Cloud", "Local Low-Latency", "Safe" });
    private string? _selectedPreset;
    public string? SelectedPreset { get => _selectedPreset; set { _selectedPreset = value; OnPropertyChanged(); if (!string.IsNullOrEmpty(value)) ApplyPreset(value); } }

    private void ApplyPreset(string preset)
    {
        var s = Services.AppServices.LoadSettings();
        switch (preset)
        {
            case "Cloud":
                s.LlmProvider = "OpenAI";
                s.AsrProvider = "OpenAI";
                s.ChunkSizeMs = 750;
                s.EnableSileroVad = false;
                break;
            case "Local Low-Latency":
                s.LlmProvider = "Ollama";
                s.AsrProvider = "Local";
                s.ChunkSizeMs = 500;
                s.EnableSileroVad = true;
                s.SileroWindowMs = 30;
                s.SileroThreshold = 0.55f;
                s.TtsUseCommunications = true;
                break;
            case "Safe":
                s.LlmProvider = "OpenAI";
                s.AsrProvider = "OpenAI";
                s.ChunkSizeMs = 1000;
                s.EnableSileroVad = true;
                s.SileroWindowMs = 40;
                s.SileroThreshold = 0.6f;
                break;
        }
        new Services.JsonSettingsStore().Save(s);
        Services.AppServices.ReloadAiClients();
        RefreshStatus();
        SpeakAnswersEnabled = Services.AppServices.LoadSettings().SpeakAnswers;
    }

    private void ToggleSpeakAnswers()
    {
        var store = new Services.JsonSettingsStore();
        var s = store.Load();
        s.SpeakAnswers = !s.SpeakAnswers;
        store.Save(s);
        SpeakAnswersEnabled = s.SpeakAnswers;
    }

    private void ApplyLlmProvider(string provider)
    {
        var store = new Services.JsonSettingsStore();
        var s = store.Load();
        s.LlmProvider = provider;
        store.Save(s);
        Services.AppServices.ReloadAiClients();
        RefreshStatus();
    }

    private void ApplyAsrProvider(string provider)
    {
        var store = new Services.JsonSettingsStore();
        var s = store.Load();
        s.AsrProvider = provider;
        store.Save(s);
        Services.AppServices.ReloadAiClients();
        RefreshStatus();
    }

    private static string BuildContextFromSettings()
    {
        var settings = Services.AppServices.LoadSettings();
        var context = string.Empty;
        if (settings.Keywords is { Length: > 0 }) context += "Keywords: " + string.Join(", ", settings.Keywords) + "\n";
        if (!string.IsNullOrWhiteSpace(settings.CompanyBlurb)) context += "Company: " + settings.CompanyBlurb + "\n";
        if (!string.IsNullOrWhiteSpace(settings.ResumeText)) context += "Resume: " + settings.ResumeText + "\n";
        if (!string.IsNullOrWhiteSpace(settings.JobDescText)) context += "JobDesc: " + settings.JobDescText + "\n";
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
