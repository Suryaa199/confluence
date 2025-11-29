using System.Windows;
using System;
using System.Runtime.InteropServices;

namespace InterviewCopilot.Windows;

public partial class OverlayWindow : Window
{
    private bool _clickThrough;
    public OverlayWindow()
    {
        InitializeComponent();
        IsHitTestVisible = true;
    }

    public void SetAnswer(string text)
    {
        AnswerText.Text = text;
        AnswerScroll?.ScrollToBottom();
    }

    public void ToggleClickThrough()
    {
        _clickThrough = !_clickThrough;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (_clickThrough)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
