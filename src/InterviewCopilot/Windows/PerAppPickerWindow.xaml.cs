using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using NAudio.CoreAudioApi;

namespace InterviewCopilot.Windows;

public partial class PerAppPickerWindow : Window
{
    private record SessionItem(string DeviceName, string EndpointId, string ProcessName, string WindowTitle, float Level);

    public PerAppPickerWindow()
    {
        InitializeComponent();
        RefreshList();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshList();

    private void RefreshList()
    {
        try
        {
            var items = new List<SessionItem>();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                var sessionManager = device.AudioSessionManager2 ?? device.AudioSessionManager;
                var sessions = sessionManager?.Sessions;
                if (sessions == null) continue;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    string proc = string.Empty;
                    string title = string.Empty;
                    try
                    {
                        int pid = 0;
                        if (session is AudioSessionControl2 session2)
                        {
                            pid = session2.ProcessID;
                        }
                        else
                        {
                            var pidProp = session.GetType().GetProperty("ProcessID");
                            if (pidProp?.GetValue(session) is int value) pid = value;
                        }
                        if (pid > 0)
                        {
                            using var p = Process.GetProcessById(pid);
                            proc = p.ProcessName ?? string.Empty;
                            try { title = p.MainWindowTitle ?? string.Empty; } catch { }
                        }
                        if (string.IsNullOrWhiteSpace(proc))
                        {
                            proc = session.DisplayName ?? "(System)";
                        }
                    }
                    catch { }
                    float lvl = 0f;
                    try { lvl = session.AudioMeterInformation?.MasterPeakValue ?? 0f; } catch { }
                    if (!string.IsNullOrEmpty(proc))
                    {
                        var deviceName = device.FriendlyName ?? string.Empty;
                        items.Add(new SessionItem(deviceName, device.ID, proc, title, lvl));
                    }
                }
            }
            var ordered = items
                .OrderByDescending(x => x.Level)
                .ThenBy(x => x.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List.ItemsSource = ordered;
            EmptyText.Visibility = ordered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Per-App Picker");
        }
    }

    private void OnUse(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is SessionItem item)
        {
            SelectedText.Text = $"{item.ProcessName} ({item.DeviceName})";
            // persist to settings
            var store = new InterviewCopilot.Services.JsonSettingsStore();
            var s = store.Load();
            s.PreferredProcessName = item.ProcessName;
            store.Save(s);
            MessageBox.Show(this, $"Per-app preference set to {item.ProcessName}. Make sure the capture audio source matches the {item.DeviceName} output device, then start listening.", "Per-App Picker");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
