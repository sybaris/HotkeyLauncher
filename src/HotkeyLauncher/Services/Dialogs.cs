using System.Windows;
using System.Windows.Controls;
using static HotkeyLauncher.Localization.Localizer;
using UiMessageBox = Wpf.Ui.Controls.MessageBox;
using UiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace HotkeyLauncher.Services;

/// <summary>Fluent-styled message boxes.</summary>
public static class Dialogs
{
    public static Task<UiMessageBoxResult> ShowAsync(string title, string message, string? primary, string? secondary, string close)
    {
        var box = new UiMessageBox
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
            CloseButtonText = close,
            PrimaryButtonText = primary ?? string.Empty,
            SecondaryButtonText = secondary ?? string.Empty,
            IsPrimaryButtonEnabled = primary != null,
            IsSecondaryButtonEnabled = secondary != null,
        };

        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != box)
            ?? (Application.Current.MainWindow is { IsVisible: true } main ? main : null);
        if (owner != null)
        {
            box.Owner = owner;
            box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return box.ShowDialogAsync();
    }

    public static Task<UiMessageBoxResult> InfoAsync(string title, string message) => ShowAsync(title, message, null, null, T("Common_Ok"));
}
