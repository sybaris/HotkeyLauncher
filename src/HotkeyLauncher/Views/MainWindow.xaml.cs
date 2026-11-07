using System.Windows;
using System.Windows.Controls;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.ViewModels;
using Wpf.Ui.Controls;

namespace HotkeyLauncher.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    // Drop a program, a document, a folder or a web link on the window to create a shortcut
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedTarget(e.Data) != null ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (GetDroppedTarget(e.Data) is { } target)
        {
            e.Handled = true;
            // Let the drag & drop operation end before opening a modal window
            Dispatcher.BeginInvoke(() => ViewModel.AddFromTarget(target));
        }
    }

    private static string? GetDroppedTarget(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) return files[0];
        if (data.GetData(DataFormats.UnicodeText) is string text && TargetResolver.IsUrl(text.Trim())) return text.Trim();
        return null;
    }
}
