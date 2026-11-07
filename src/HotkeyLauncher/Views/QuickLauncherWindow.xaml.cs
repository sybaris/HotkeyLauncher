using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HotkeyLauncher.ViewModels;
using Forms = System.Windows.Forms;

namespace HotkeyLauncher.Views;

/// <summary>Floating search box, centered on the screen of the mouse, hidden as soon as it loses the focus.</summary>
public partial class QuickLauncherWindow : Window
{
    private readonly QuickLauncherViewModel _viewModel;

    public QuickLauncherWindow(QuickLauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Hide();
    }

    public void ShowCentered()
    {
        _viewModel.Reset();
        Show();
        UpdateLayout();

        // Physical pixels: works whatever the DPI of each screen
        var handle = new WindowInteropHelper(this).Handle;
        var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        GetWindowRect(handle, out var rect);
        var width = rect.Right - rect.Left;
        SetWindowPos(handle, IntPtr.Zero, area.Left + (area.Width - width) / 2, area.Top + area.Height / 5, 0, 0, SWP_NOSIZE | SWP_NOZORDER);

        Activate();
        SetForegroundWindow(handle);
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                break;
            case Key.Down:
                _viewModel.MoveSelection(1);
                break;
            case Key.Up:
                _viewModel.MoveSelection(-1);
                break;
            case Key.Enter:
                _viewModel.LaunchSelected();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
