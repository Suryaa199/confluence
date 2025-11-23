using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using InterviewCopilot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace InterviewCopilot.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _liveQuestion = "";
    private string _liveAnswer = "";
    private string _audioStatus = "Idle";
    private string _asrStatus = "Idle";
    private string _llmStatus = "Idle";
    private Services.Orchestrator? _orchestrator;
    private Windows.OverlayWindow? _overlay;
    private bool _paused;
    private Services.AudioOptions? _lastOptions;

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

    public ObservableCollection<DeviceItem> AudioEndpoints { get; } = new();
    private DeviceItem? _selectedAudioEndpoint;
    public DeviceItem? SelectedAudioEndpoint { get => _selectedAudioEndpoint; set { _selectedAudioEndpoint = value; OnPropertyChanged(); } }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ToggleOverlayCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand TestLevelsCommand { get; }
    public ICommand CopyAnswerCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand ToggleClickThroughCommand { get; }
    public ICommand PauseCommand { get; }

    public MainViewModel()
    {
        StartCommand = new RelayCommand(_ => Start());
        StopCommand = new RelayCommand(_ => Stop());
        ToggleOverlayCommand = new RelayCommand(_ => ToggleOverlay());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        TestLevelsCommand = new RelayCommand(async _ => await TestLevelsAsync(), _ => _orchestrator is null);
        CopyAnswerCommand = new RelayCommand(_ => CopyAnswer(), _ => !string.IsNullOrEmpty(LiveAnswer));
        RegenerateCommand = new RelayCommand(async _ => await RegenerateAsync(), _ => _orchestrator is not null || !string.IsNullOrEmpty(LiveQuestion));
        Services.AppServices.Audio.OnLevel += v => Level = v;
        ToggleClickThroughCommand = new RelayCommand(_ => ToggleClickThrough());
        PauseCommand = new RelayCommand(async _ => await TogglePauseAsync());
        RefreshEndpoints();
    }

    private async void Start()
    {
        AudioStatus = "Capturing";
        AsrStatus = "Listening";
        LlmStatus = "Ready";
        LiveQuestion = "";
        LiveAnswer = "";
        FollowUps.Clear();

        _orchestrator = Services.AppServices.CreateOrchestrator();
        _orchestrator.OnTranscript += text =>
        {
            LiveQuestion += (LiveQuestion.Length > 0 ? " " : "") + text;
        };
        _orchestrator.OnAnswerToken += tok =>
        {
            LiveAnswer += tok;
        };
        _orchestrator.OnFollowUps += list =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                FollowUps.Clear();
                foreach (var f in list) FollowUps.Add(f);
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
            EndpointId = SelectedAudioEndpoint?.Id
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
            _overlay = new Windows.OverlayWindow();
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
            _overlay = new Windows.OverlayWindow();
            _overlay.Owner = System.Windows.Application.Current.MainWindow;
            _overlay.Show();
        }
        _overlay.ToggleClickThrough();
    }

    private void OpenSettings()
    {
        var w = new Windows.SettingsWindow();
        w.Owner = System.Windows.Application.Current.MainWindow;
        w.ShowDialog();
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
