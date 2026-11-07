using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher.Views;

/// <summary>null / empty string / false / 0 → Collapsed, anything else → Visible. Parameter "invert" reverses it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            string s => s.Length > 0,
            bool b => b,
            int i => i != 0,
            _ => true,
        };
        if (parameter as string == "invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Display name of a group header: entries without group are shown under "No group".</summary>
public sealed class GroupNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } name ? name : T("List_NoGroup");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
