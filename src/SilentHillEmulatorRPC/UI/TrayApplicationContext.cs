using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.State;

namespace SilentHillEmulatorRPC.UI;

/// <summary>
/// Windows System Tray Application Context managing the NotifyIcon,
/// dynamic game detection checkboxes, and application lifecycle.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    #region Fields

    private readonly IProfileManager _profileManager;
    private readonly IRpcStateTracker _stateTracker;
    private readonly Action _requestShutdown;
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly SynchronizationContext? _syncContext;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _statusMenuItem;
    private readonly Dictionary<string, ToolStripMenuItem> _gameMenuItems = new(StringComparer.OrdinalIgnoreCase);
    private bool _preventMenuClose;

    #endregion

    #region Constructor

    public TrayApplicationContext(
        IProfileManager profileManager,
        IRpcStateTracker stateTracker,
        Action requestShutdown,
        ILogger<TrayApplicationContext> logger)
    {
        _profileManager = profileManager;
        _stateTracker = stateTracker;
        _requestShutdown = requestShutdown;
        _logger = logger;
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        InitializeTrayIcon();

        _stateTracker.StateUpdated += OnRpcStateUpdated;
        _profileManager.ProfileToggled += OnProfileToggledExternal;
    }

    #endregion

    #region Initialization

    private void InitializeTrayIcon()
    {
        _contextMenu = new ContextMenuStrip();
        BuildContextMenu();

        var icon = LoadApplicationIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            ContextMenuStrip = _contextMenu,
            Text = TruncateTooltip("Silent Hill Discord RPC - Ready"),
            Visible = true
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.DoubleClick += (s, e) => OpenConfigurationFile();
    }

    private void BuildContextMenu()
    {
        if (_contextMenu == null) return;
        _contextMenu.Items.Clear();
        _gameMenuItems.Clear();

        // Header Title
        var titleItem = new ToolStripMenuItem("Silent Hill Discord RPC")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        _contextMenu.Items.Add(titleItem);

        // Status Label
        _statusMenuItem = new ToolStripMenuItem("⚪ Status: Waiting for game...")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Italic)
        };
        _contextMenu.Items.Add(_statusMenuItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Header for Game Detection Toggles
        var toggleHeaderItem = new ToolStripMenuItem("Game Detection:")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        _contextMenu.Items.Add(toggleHeaderItem);

        // Dynamic Checkboxes for each game profile
        var profiles = _profileManager.GetProfiles();
        foreach (var profile in profiles)
        {
            var item = new ToolStripMenuItem(profile.DisplayName)
            {
                Checked = profile.Enabled,
                CheckOnClick = true
            };

            var id = profile.Identifier;
            item.Click += (sender, args) =>
            {
                var isChecked = item.Checked;
                _profileManager.SetProfileEnabled(id, isChecked);
            };

            _gameMenuItems[id] = item;
            _contextMenu.Items.Add(item);
        }

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Open Configuration
        var configItem = new ToolStripMenuItem("⚙️ Open Configuration (appsettings.json)", null, (s, e) => OpenConfigurationFile());
        _contextMenu.Items.Add(configItem);

        // Open Assets Folder
        var assetsItem = new ToolStripMenuItem("📁 Open Assets Folder (img)", null, (s, e) => OpenAssetsFolder());
        _contextMenu.Items.Add(assetsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("❌ Exit", null, (s, e) => ExitApplication());
        _contextMenu.Items.Add(exitItem);

        // Prevent context menu from auto-closing when toggling checkboxes
        _contextMenu.ItemClicked += (sender, e) =>
        {
            if (e.ClickedItem is ToolStripMenuItem menuItem && _gameMenuItems.ContainsValue(menuItem))
            {
                _preventMenuClose = true;
            }
        };

        _contextMenu.Closing += (sender, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && _preventMenuClose)
            {
                e.Cancel = true;
                _preventMenuClose = false;
            }
        };
    }

    #endregion

    #region Event Handlers

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            try
            {
                var method = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_notifyIcon, null);
            }
            catch
            {
                // Fallback: standard right-click triggers menu
            }
        }
    }

    private void OnRpcStateUpdated(ServiceState state, GameMatchResult? match)
    {
        _syncContext?.Post(_ =>
        {
            if (_notifyIcon == null || _statusMenuItem == null) return;

            switch (state)
            {
                case ServiceState.ActiveGame when match != null:
                    _statusMenuItem.Text = $"🟢 Active: {match.Profile.DisplayName}";
                    _notifyIcon.Text = TruncateTooltip($"Silent Hill RPC - Active: {match.Profile.DisplayName}");
                    break;

                case ServiceState.Terminating:
                    _statusMenuItem.Text = "🟡 Terminating session...";
                    _notifyIcon.Text = TruncateTooltip("Silent Hill Discord RPC - Terminating...");
                    break;

                default:
                    _statusMenuItem.Text = "⚪ Status: Waiting for game...";
                    _notifyIcon.Text = TruncateTooltip("Silent Hill Discord RPC - Ready");
                    break;
            }
        }, null);
    }

    private void OnProfileToggledExternal(string identifier, bool isEnabled)
    {
        _syncContext?.Post(_ =>
        {
            if (_gameMenuItems.TryGetValue(identifier, out var item))
            {
                item.Checked = isEnabled;
            }
        }, null);
    }

    #endregion

    #region Action Methods

    private void OpenConfigurationFile()
    {
        try
        {
            var configPath = FindAppSettingsPath();
            if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("The file appsettings.json was not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open configuration file.");
        }
    }

    private void OpenAssetsFolder()
    {
        try
        {
            var folder = FindAssetsPath();
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("The assets folder 'img' was not found.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open assets folder.");
        }
    }

    private void ExitApplication()
    {
        _logger.LogInformation("Exit requested from System Tray menu.");

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _requestShutdown();
        ExitThread();
    }

    #endregion

    #region Helper Methods

    private static Icon LoadApplicationIcon()
    {
        var icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                return new Icon(icoPath);
            }
            catch
            {
                // Fallback to generated icon
            }
        }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(180, 20, 20));
            g.FillEllipse(brush, 2, 2, 28, 28);

            using var pen = new Pen(Color.White, 2f);
            g.DrawEllipse(pen, 6, 6, 20, 20);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static string TruncateTooltip(string text)
    {
        // Windows NotifyIcon tooltip is limited to 63 characters
        if (text.Length <= 63)
            return text;

        return text[..60] + "...";
    }

    private static string? FindAppSettingsPath()
    {
        var exeDir = AppContext.BaseDirectory;
        var direct = Path.Combine(exeDir, "appsettings.json");
        if (File.Exists(direct)) return direct;

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(cwd)) return cwd;

        var src = Path.Combine(Directory.GetCurrentDirectory(), "src", "SilentHillEmulatorRPC", "appsettings.json");
        if (File.Exists(src)) return src;

        return direct;
    }

    private static string? FindAssetsPath()
    {
        var exeDir = AppContext.BaseDirectory;
        var direct = Path.Combine(exeDir, "img");
        if (Directory.Exists(direct)) return direct;

        var srcImg = Path.Combine(Directory.GetCurrentDirectory(), "src", "img");
        if (Directory.Exists(srcImg)) return srcImg;

        var parentImg = Path.Combine(Directory.GetCurrentDirectory(), "img");
        if (Directory.Exists(parentImg)) return parentImg;

        return null;
    }

    #endregion

    #region Disposal

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _contextMenu?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion
}
