using System.Windows;
using System.Windows.Input;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.ViewModels;
using Wpf.Ui.Controls;

namespace HotkeyLauncher.Views;

public partial class HotkeyEditorWindow : FluentWindow
{
    private static readonly Key[] ModifierKeys =
    [
        Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
        Key.ImeProcessed, Key.DeadCharProcessed, Key.None,
    ];

    public HotkeyEditorWindow(EditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // The window sizes itself to its content but never exceeds the screen (a scrollbar appears only then)
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 20);
        viewModel.CloseRequested += (_, saved) => DialogResult = saved;

        // The on-screen keyboard follows the Windows keyboard layout, also when it is changed while the window is open
        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;
        Closed += (_, _) => InputLanguageManager.Current.InputLanguageChanged -= OnInputLanguageChanged;

        // New shortcut: start with the target; existing one: start in the hotkey capture box
        Loaded += (_, _) =>
        {
            if (viewModel.IsNew && string.IsNullOrEmpty(viewModel.Target)) TargetBox.Focus();
            else CaptureBox.Focus();
        };
    }

    private EditorViewModel ViewModel => (EditorViewModel)DataContext;

    // Raised before the thread layout is effectively switched: refresh once the change is applied
    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs e) =>
        Dispatcher.BeginInvoke(ViewModel.RefreshKeyboardLayout, System.Windows.Threading.DispatcherPriority.Background);

    private void CaptureBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CaptureBox.Focus();
        e.Handled = true;
    }

    /// <summary>Records the combination typed while the capture box has the focus.</summary>
    private void CaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,         // keys pressed with Alt
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        var modifiers = HotkeyModifiers.None;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyModifiers.Win;

        // Keep the usual keyboard navigation when no modifier is pressed
        if (modifiers == HotkeyModifiers.None && key is Key.Tab or Key.Escape)
        {
            if (key == Key.Escape)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;
        if (ModifierKeys.Contains(key)) return;

        if (modifiers == HotkeyModifiers.None && key is Key.Back or Key.Delete)
        {
            ViewModel.ClearHotkeyCommand.Execute(null);
            return;
        }

        ViewModel.Capture(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    // A file or folder dropped anywhere on the window becomes the target
    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedTarget(e.Data) != null ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (GetDroppedTarget(e.Data) is not { } target) return;
        ViewModel.Target = target;
        e.Handled = true;
    }

    private static string? GetDroppedTarget(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) return files[0];
        if (data.GetData(DataFormats.UnicodeText) is string text && TargetResolver.IsUrl(text.Trim())) return text.Trim();
        return null;
    }
}
