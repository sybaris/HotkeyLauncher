using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        AppController.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    // ---- Reordering by drag & drop ----

    private Point _dragStart;
    private HotkeyItemViewModel? _dragCandidate;

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Buttons and switches of the card keep their normal behavior
        _dragCandidate = IsInsideButton(e.OriginalSource as DependencyObject) ? null : (sender as FrameworkElement)?.DataContext as HotkeyItemViewModel;
        _dragStart = e.GetPosition(this);
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate == null || e.LeftButton != MouseButtonState.Pressed || !ViewModel.IsReorderEnabled) return;
        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(HotkeyItemViewModel), item), DragDropEffects.Move);
        foreach (var other in ViewModel.Items) other.DropIndicator = DropPosition.None;
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(HotkeyItemViewModel)) is not HotkeyItemViewModel dragged) return; // files: handled by the window
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: HotkeyItemViewModel target } element || target == dragged)
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        target.DropIndicator = e.GetPosition(element).Y < element.ActualHeight / 2 ? DropPosition.Before : DropPosition.After;
    }

    private void Card_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyItemViewModel target }) target.DropIndicator = DropPosition.None;
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(HotkeyItemViewModel)) is not HotkeyItemViewModel dragged) return;
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: HotkeyItemViewModel target } element) return;
        var before = e.GetPosition(element).Y < element.ActualHeight / 2;
        target.DropIndicator = DropPosition.None;
        Dispatcher.BeginInvoke(() => ViewModel.MoveTo(dragged, target, before));
    }

    private void GroupHeader_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(HotkeyItemViewModel))) return;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void GroupHeader_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(HotkeyItemViewModel)) is not HotkeyItemViewModel dragged) return;
        if (sender is not FrameworkElement { DataContext: CollectionViewGroup group }) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(() => ViewModel.MoveToGroup(dragged, group.Name as string ?? string.Empty));
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is ButtonBase) return true;
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    // Drop a program, a document, a folder or a web link on the window to create a shortcut
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(HotkeyItemViewModel)))
        {
            e.Effects = DragDropEffects.None; // a shortcut dragged outside of the list
            e.Handled = true;
            return;
        }

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
