using System.Reflection;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Models;
using static HotkeyLauncher.Localization.Localizer;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace HotkeyLauncher.Services;

/// <summary>
/// Notification-area icon, like RapidApp:
/// left click = menu to launch the shortcuts with the mouse, right click = application menu,
/// double click = main window.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _appMenu = new();
    private readonly Forms.ContextMenuStrip _launchMenu = new();
    private readonly Forms.Timer _singleClickTimer;
    private readonly Drawing.Icon _enabledIcon;
    private readonly Drawing.Icon _disabledIcon;
    private readonly Forms.ToolStripTextBox _searchBox = new() { AutoSize = false, Width = 240 };
    private readonly Forms.ToolStripSeparator _searchSeparator = new();
    private Action? _notificationClick;
    private AppSettings? _settings;
    private bool _hotkeysActive;

    public TrayService()
    {
        var size = Forms.SystemInformation.SmallIconSize;
        _enabledIcon = LoadIcon("HotkeyLauncher.tray.ico", size);
        _disabledIcon = LoadIcon("HotkeyLauncher.tray-disabled.ico", size);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _enabledIcon,
            Text = "HotkeyLauncher",
            ContextMenuStrip = _appMenu,
        };

        // A double click starts with a single click: wait to know which one it is
        _singleClickTimer = new Forms.Timer { Interval = Forms.SystemInformation.DoubleClickTime };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            ShowLaunchMenu();
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) _singleClickTimer.Start();
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            _singleClickTimer.Stop();
            OpenRequested?.Invoke(this, EventArgs.Empty);
        };
        _notifyIcon.BalloonTipClicked += (_, _) => _notificationClick?.Invoke();
        _notifyIcon.BalloonTipClosed += (_, _) => _notificationClick = null;

        // Search box at the top of the launch menu: typing filters the shortcuts
        _searchBox.TextBox.PlaceholderText = "🔍";
        _searchBox.TextChanged += (_, _) => FillLaunchItems();
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Forms.Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var first = _launchMenu.Items.OfType<Forms.ToolStripMenuItem>().FirstOrDefault(i => i.Tag is Guid);
                if (first?.Tag is Guid id)
                {
                    _launchMenu.Close();
                    LaunchRequested?.Invoke(this, id);
                }
            }
            else if (e.KeyCode == Forms.Keys.Down)
            {
                e.SuppressKeyPress = true;
                _launchMenu.Items.OfType<Forms.ToolStripMenuItem>().FirstOrDefault(i => i.Enabled)?.Select();
            }
        };
        _launchMenu.Opened += (_, _) => _searchBox.Focus();
        _launchMenu.Closed += (_, _) =>
        {
            if (_searchBox.Text.Length > 0) _searchBox.Text = string.Empty;
        };
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<Guid>? LaunchRequested;

    public void Show() => _notifyIcon.Visible = true;

    /// <summary>Rebuilds both menus (after a change of the shortcuts, of the language or of the theme).</summary>
    public void Update(AppSettings settings, bool hotkeysActive, bool darkTheme)
    {
        _notifyIcon.Icon = hotkeysActive ? _enabledIcon : _disabledIcon;
        var tooltip = hotkeysActive ? "HotkeyLauncher" : $"HotkeyLauncher – {T("Tray_Disabled")}";
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        var renderer = new MenuRenderer(darkTheme);
        BuildLaunchMenu(settings, hotkeysActive);
        BuildAppMenu(hotkeysActive);
        _launchMenu.Renderer = renderer;
        _appMenu.Renderer = renderer;
    }

    /// <param name="onClick">Action run when the user clicks the notification.</param>
    public void ShowNotification(string title, string text, bool isError = false, Action? onClick = null)
    {
        _notificationClick = onClick;
        _notifyIcon.ShowBalloonTip(5000, title, text, isError ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _singleClickTimer.Dispose();
        _appMenu.Dispose();
        _launchMenu.Dispose();
        _enabledIcon.Dispose();
        _disabledIcon.Dispose();
    }

    private void BuildLaunchMenu(AppSettings settings, bool hotkeysActive)
    {
        _settings = settings;
        _hotkeysActive = hotkeysActive;
        _launchMenu.ShowImageMargin = true;
        _launchMenu.ImageScalingSize = Forms.SystemInformation.SmallIconSize;
        _searchBox.TextBox.PlaceholderText = T("Tray_Search");
        FillLaunchItems();
    }

    /// <summary>Search box, then either the full menu (groups) or the search results.</summary>
    private void FillLaunchItems()
    {
        if (_settings == null) return;
        var settings = _settings;
        var hotkeysActive = _hotkeysActive;

        _launchMenu.SuspendLayout();

        // Keep the search box (it has the focus while typing), replace everything else
        for (var i = _launchMenu.Items.Count - 1; i >= 0; i--)
        {
            var item = _launchMenu.Items[i];
            if (item == _searchBox || item == _searchSeparator) continue;
            _launchMenu.Items.RemoveAt(i);
            DisposeItem(item);
        }

        if (settings.Hotkeys.Count > 0 && !_launchMenu.Items.Contains(_searchBox))
        {
            _launchMenu.Items.Insert(0, _searchBox);
            _launchMenu.Items.Insert(1, _searchSeparator);
        }
        else if (settings.Hotkeys.Count == 0)
        {
            _launchMenu.Items.Remove(_searchBox);
            _launchMenu.Items.Remove(_searchSeparator);
        }

        if (_searchBox.Text.Trim().Length > 0)
        {
            var results = QuickSearch.Search(settings.Hotkeys, _searchBox.Text, 15);
            foreach (var entry in results) _launchMenu.Items.Add(CreateEntryItem(entry, hotkeysActive));
            if (results.Count == 0) _launchMenu.Items.Add(new Forms.ToolStripMenuItem(T("Tray_NoResult")) { Enabled = false });
            _launchMenu.ResumeLayout();
            return;
        }

        foreach (var group in settings.Groups)
        {
            var groupItem = new Forms.ToolStripMenuItem(Escape(group));
            groupItem.DropDown.ImageScalingSize = Forms.SystemInformation.SmallIconSize;
            foreach (var entry in settings.Hotkeys.Where(h => h.Group == group))
            {
                groupItem.DropDownItems.Add(CreateEntryItem(entry, hotkeysActive));
            }

            _launchMenu.Items.Add(groupItem);
        }

        var ungrouped = settings.Hotkeys.Where(h => h.Group.Length == 0).ToList();
        if (ungrouped.Count > 0 && settings.Groups.Count > 0) _launchMenu.Items.Add(new Forms.ToolStripSeparator());
        foreach (var entry in ungrouped) _launchMenu.Items.Add(CreateEntryItem(entry, hotkeysActive));

        if (settings.Hotkeys.Count == 0)
        {
            _launchMenu.Items.Add(new Forms.ToolStripMenuItem(T("Tray_NoShortcut")) { Enabled = false });
        }

        _launchMenu.ResumeLayout();
    }

    private Forms.ToolStripMenuItem CreateEntryItem(HotkeyEntry entry, bool hotkeysActive)
    {
        var item = new Forms.ToolStripMenuItem(Escape(entry.Name), IconService.GetMenuBitmap(entry.Target));
        if (hotkeysActive && entry.IsEnabled && entry.Hotkey.IsComplete) item.ShortcutKeyDisplayString = KeyNames.Format(entry.Hotkey);
        var id = entry.Id;
        item.Tag = id;
        item.Click += (_, _) => LaunchRequested?.Invoke(this, id);
        return item;
    }

    private void BuildAppMenu(bool hotkeysActive)
    {
        _appMenu.Items.Clear();
        _appMenu.ShowImageMargin = false;
        _appMenu.ShowCheckMargin = true;

        var open = new Forms.ToolStripMenuItem(T("Tray_Open")) { Font = new Drawing.Font(_appMenu.Font, Drawing.FontStyle.Bold) };
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var toggle = new Forms.ToolStripMenuItem(T("Tray_HotkeysActive")) { Checked = hotkeysActive };
        toggle.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);

        var exit = new Forms.ToolStripMenuItem(T("Tray_Exit"));
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _appMenu.Items.AddRange([open, toggle, new Forms.ToolStripSeparator(), exit]);
    }

    private void ShowLaunchMenu()
    {
        // NotifyIcon.ShowContextMenu positions the menu correctly near the taskbar and gives it the focus
        var showContextMenu = typeof(Forms.NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (showContextMenu == null)
        {
            _launchMenu.Show(Forms.Cursor.Position);
            return;
        }

        _notifyIcon.ContextMenuStrip = _launchMenu;
        try
        {
            showContextMenu.Invoke(_notifyIcon, null);
        }
        finally
        {
            _notifyIcon.ContextMenuStrip = _appMenu;
        }
    }

    /// <summary>Disposes a menu item without disposing its icon, which belongs to the <see cref="IconService"/> cache.</summary>
    private static void DisposeItem(Forms.ToolStripItem item)
    {
        if (item is Forms.ToolStripMenuItem menuItem)
        {
            foreach (Forms.ToolStripItem child in menuItem.DropDownItems.Cast<Forms.ToolStripItem>().ToList()) DisposeItem(child);
        }

        item.Image = null;
        item.Dispose();
    }

    // "&" would be interpreted as a mnemonic
    private static string Escape(string text) => text.Replace("&", "&&");

    private static Drawing.Icon LoadIcon(string resourceName, Drawing.Size size)
    {
        using var stream = typeof(TrayService).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing resource {resourceName}");
        return new Drawing.Icon(stream, size);
    }

    /// <summary>Menus following the light / dark theme of the application.</summary>
    private sealed class MenuRenderer(bool dark) : Forms.ToolStripProfessionalRenderer(new MenuColors(dark))
    {
        private readonly Drawing.Color _text = dark ? Drawing.Color.FromArgb(240, 240, 240) : Drawing.Color.FromArgb(28, 28, 28);
        private readonly Drawing.Color _disabledText = dark ? Drawing.Color.FromArgb(130, 130, 130) : Drawing.Color.FromArgb(150, 150, 150);

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _text : _disabledText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            using var pen = new Drawing.Pen(_text, 1.6f);
            e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, [
                new Drawing.PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.5f),
                new Drawing.PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
                new Drawing.PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f),
            ]);
        }
    }

    private sealed class MenuColors(bool dark) : Forms.ProfessionalColorTable
    {
        private readonly Drawing.Color _background = dark ? Drawing.Color.FromArgb(44, 44, 44) : Drawing.Color.FromArgb(249, 249, 249);
        private readonly Drawing.Color _hover = dark ? Drawing.Color.FromArgb(61, 61, 61) : Drawing.Color.FromArgb(232, 232, 232);
        private readonly Drawing.Color _border = dark ? Drawing.Color.FromArgb(70, 70, 70) : Drawing.Color.FromArgb(220, 220, 220);

        public override Drawing.Color ToolStripDropDownBackground => _background;
        public override Drawing.Color ImageMarginGradientBegin => _background;
        public override Drawing.Color ImageMarginGradientMiddle => _background;
        public override Drawing.Color ImageMarginGradientEnd => _background;
        public override Drawing.Color MenuBorder => _border;
        public override Drawing.Color MenuItemBorder => _hover;
        public override Drawing.Color MenuItemSelected => _hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => _hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => _hover;
        public override Drawing.Color MenuItemPressedGradientBegin => _hover;
        public override Drawing.Color MenuItemPressedGradientEnd => _hover;
        public override Drawing.Color CheckBackground => _background;
        public override Drawing.Color CheckSelectedBackground => _hover;
        public override Drawing.Color CheckPressedBackground => _hover;
        public override Drawing.Color SeparatorDark => _border;
        public override Drawing.Color SeparatorLight => _border;
    }
}
