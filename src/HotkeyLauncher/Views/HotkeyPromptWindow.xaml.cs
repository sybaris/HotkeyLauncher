using System.Windows;
using System.Windows.Input;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using Wpf.Ui.Controls;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher.Views;

/// <summary>Asks for one key combination (used for the quick launcher hotkey).</summary>
public partial class HotkeyPromptWindow : FluentWindow
{
    private readonly IReadOnlyList<HotkeyEntry> _entries;
    private readonly Func<Hotkey, bool> _isAvailable;

    public HotkeyPromptWindow(Hotkey current, IReadOnlyList<HotkeyEntry> entries, Func<Hotkey, bool> isAvailable)
    {
        InitializeComponent();
        _entries = entries;
        _isAvailable = isAvailable;
        Hotkey = current;
        Loaded += (_, _) => CaptureBox.Focus();
        Refresh();
    }

    public Hotkey Hotkey { get; private set; }

    private void CaptureBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CaptureBox.Focus();
        e.Handled = true;
    }

    private void CaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var action = KeyCapture.Read(e, out var modifiers, out var virtualKey);
        if (action is CaptureAction.PassThrough or CaptureAction.Leave) return;
        e.Handled = true;
        if (action != CaptureAction.Capture) return;

        Hotkey = new Hotkey(modifiers, virtualKey);
        Refresh();
    }

    private void Refresh()
    {
        KeyCaps.ItemsSource = KeyNames.Parts(Hotkey);

        string status;
        var canSave = false;
        if (!Hotkey.IsComplete)
        {
            status = T("Editor_StatusNeedsModifier");
        }
        else if (EntryValidator.FindOwner(Hotkey, _entries) is { } owner)
        {
            status = F("Editor_StatusUsedBy", owner.Name);
        }
        else if (ReservedHotkeys.IsReserved(Hotkey))
        {
            status = T("Editor_StatusReserved");
        }
        else if (!_isAvailable(Hotkey))
        {
            status = T("Editor_StatusTaken");
        }
        else
        {
            status = T("Editor_StatusAvailable");
            canSave = true;
        }

        StatusText.Text = status;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(canSave ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush");
        OkButton.IsEnabled = canSave;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
