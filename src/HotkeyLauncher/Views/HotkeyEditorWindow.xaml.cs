using System.Windows;
using System.Windows.Input;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.ViewModels;
using Wpf.Ui.Controls;

namespace HotkeyLauncher.Views;

public partial class HotkeyEditorWindow : FluentWindow
{
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
        switch (KeyCapture.Read(e, out var modifiers, out var virtualKey))
        {
            case CaptureAction.PassThrough:
                return;
            case CaptureAction.Leave:
                Keyboard.ClearFocus();
                break;
            case CaptureAction.Clear:
                ViewModel.ClearHotkeyCommand.Execute(null);
                break;
            case CaptureAction.Capture:
                ViewModel.Capture(modifiers, virtualKey);
                break;
        }

        e.Handled = true;
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
