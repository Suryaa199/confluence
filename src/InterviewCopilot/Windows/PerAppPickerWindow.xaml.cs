using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using NAudio.CoreAudioApi;

namespace InterviewCopilot.Windows;

public partial class PerAppPickerWindow : Window
{
    private record SessionItem(string ProcessName, string WindowTitle, float Level);

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
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager?.Sessions;
            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    string proc = string.Empty;
                    string title = string.Empty;
                    try
                    {
                        var pi = s.GetType().GetProperty("Process");
                        if (pi != null)
                        {
                            var p = pi.GetValue(s) as Process;
                            if (p != null)
                            {
                                proc = p.ProcessName ?? string.Empty;
                                try { title = p.MainWindowTitle ?? string.Empty; } catch { }
                            }
                        }
                    }
                    catch { }
                    float lvl = 0f;
                    try { lvl = s.AudioMeterInformation?.MasterPeakValue ?? 0f; } catch { }
                    if (!string.IsNullOrEmpty(proc)) items.Add(new SessionItem(proc, title, lvl));
                }
            }
            List.ItemsSource = items.OrderByDescending(x => x.Level).ToList();
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
            SelectedText.Text = item.ProcessName;
            // persist to settings
            var store = new InterviewCopilot.Services.JsonSettingsStore();
            var s = store.Load();
            s.PreferredProcessName = item.ProcessName;
            store.Save(s);
            MessageBox.Show(this, $"Per-app preference set to {item.ProcessName}. Start capture to apply.");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

