using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;
using HotkeyLauncher.Core;

namespace HotkeyLauncher.Localization;

/// <summary>
/// Gives access to the translated strings (Resources/Strings*.resx) and lets the UI switch language
/// without restarting: every binding on the indexer is refreshed when the language changes.
/// To add a language: add a column to tools/strings.tsv, run tools/make_resx.py, add an entry in
/// <see cref="Languages"/> and in SatelliteResourceLanguages (Directory.Build.props).
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("HotkeyLauncher.Resources.Strings", typeof(Localizer).Assembly);

    public static Localizer Instance { get; } = new();

    /// <summary>Available UI languages (code, native name).</summary>
    public static IReadOnlyList<(string Code, string Name)> Languages { get; } =
        [("en", "English"), ("fr", "Français"), ("es", "Español"), ("de", "Deutsch")];

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public string this[string key] => Resources.GetString(key, Culture) ?? $"[{key}]";

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args) => string.Format(Instance.Culture, Instance[key], args);

    /// <param name="code">Language code, or empty to follow the Windows display language.</param>
    public void SetLanguage(string code)
    {
        if (string.IsNullOrEmpty(code)) code = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        if (!Languages.Any(l => l.Code == code)) code = "en";

        Culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        KeyNames.CtrlName = this["Key_Ctrl"];
        KeyNames.AltName = this["Key_Alt"];
        KeyNames.ShiftName = this["Key_Shift"];
        KeyNames.WinName = this["Key_Win"];

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>XAML usage: Text="{l:Tr Main_Title}". The text follows language changes.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public TrExtension() : this(string.Empty)
    {
    }

    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
