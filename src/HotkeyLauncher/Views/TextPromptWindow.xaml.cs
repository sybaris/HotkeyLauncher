using System.Windows;
using Wpf.Ui.Controls;

namespace HotkeyLauncher.Views;

/// <summary>Small dialog asking for one line of text.</summary>
public partial class TextPromptWindow : FluentWindow
{
    private TextPromptWindow()
    {
        InitializeComponent();
    }

    /// <summary>Returns the text entered, or null if cancelled.</summary>
    public static string? Ask(Window? owner, string title, string label, string initialValue)
    {
        var window = new TextPromptWindow { Owner = owner, Title = title };
        window.TitleBar.Title = title;
        window.LabelText.Text = label;
        window.InputBox.Text = initialValue;
        window.Loaded += (_, _) =>
        {
            window.InputBox.Focus();
            window.InputBox.SelectAll();
        };
        return window.ShowDialog() == true ? window.InputBox.Text : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
