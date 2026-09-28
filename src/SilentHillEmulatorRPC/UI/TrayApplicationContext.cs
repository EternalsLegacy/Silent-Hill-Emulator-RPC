using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
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

    private readonly IProfileManager ProfileManager;
    private readonly IRpcStateTracker StateTracker;
    private readonly Action RequestShutdown;
    private readonly ILogger<TrayApplicationContext> Logger;
    private readonly SynchronizationContext? SyncContext;

    private NotifyIcon? NotifyIconInstance;
    private ContextMenuStrip? ContextMenu;
    private ToolStripMenuItem? StatusMenuItem;
    private readonly Dictionary<string, ToolStripMenuItem> GameMenuItems = new(StringComparer.OrdinalIgnoreCase);
    private bool PreventMenuClose;

    #endregion

    #region Constructor

    public TrayApplicationContext(
        IProfileManager ProfileManager,
        IRpcStateTracker StateTracker,
        Action RequestShutdown,
        ILogger<TrayApplicationContext> Logger)
    {
        this.ProfileManager = ProfileManager;
        this.StateTracker = StateTracker;
        this.RequestShutdown = RequestShutdown;
        this.Logger = Logger;
        SyncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        InitializeTrayIcon();

        this.StateTracker.StateUpdated += OnRpcStateUpdated;
        this.ProfileManager.ProfileToggled += OnProfileToggledExternal;
    }

    #endregion

    #region Initialization

    private void InitializeTrayIcon()
    {
        ContextMenu = new ContextMenuStrip();
        BuildContextMenu();

        Icon AppIcon = LoadApplicationIcon();

        NotifyIconInstance = new NotifyIcon
        {
            Icon = AppIcon,
            ContextMenuStrip = ContextMenu,
            Text = TruncateTooltip("Silent Hill Discord RPC - Ready"),
            Visible = true
        };

        NotifyIconInstance.MouseClick += OnNotifyIconMouseClick;
        NotifyIconInstance.DoubleClick += (Sender, Args) => OpenConfigurationFile();
    }

    private void BuildContextMenu()
    {
        if (ContextMenu == null) return;
        ContextMenu.Items.Clear();
        GameMenuItems.Clear();

        ToolStripMenuItem TitleItem = new ToolStripMenuItem("Silent Hill Discord RPC")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        ContextMenu.Items.Add(TitleItem);

        StatusMenuItem = new ToolStripMenuItem("⚪ Status: Waiting for game...")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Italic)
        };
        ContextMenu.Items.Add(StatusMenuItem);

        ContextMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem ToggleHeaderItem = new ToolStripMenuItem("Game Detection:")
        {
            Enabled = false,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        ContextMenu.Items.Add(ToggleHeaderItem);

        IReadOnlyList<GameProfile> Profiles = ProfileManager.GetProfiles();
        foreach (GameProfile Profile in Profiles)
        {
            ToolStripMenuItem Item = new ToolStripMenuItem(Profile.DisplayName)
            {
                Checked = Profile.Enabled,
                CheckOnClick = true
            };

            string Id = Profile.Identifier;
            Item.Click += (Sender, Args) =>
            {
                bool IsChecked = Item.Checked;
                ProfileManager.SetProfileEnabled(Id, IsChecked);
            };

            GameMenuItems[Id] = Item;
            ContextMenu.Items.Add(Item);
        }

        ContextMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem ConfigItem = new ToolStripMenuItem("⚙️ Open Configuration (appsettings.json)", null, (Sender, Args) => OpenConfigurationFile());
        ContextMenu.Items.Add(ConfigItem);

        ToolStripMenuItem AssetsItem = new ToolStripMenuItem("📁 Open Assets Folder (img)", null, (Sender, Args) => OpenAssetsFolder());
        ContextMenu.Items.Add(AssetsItem);

        ContextMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem ExitItem = new ToolStripMenuItem("❌ Exit", null, (Sender, Args) => ExitApplication());
        ContextMenu.Items.Add(ExitItem);

        ContextMenu.ItemClicked += (Sender, E) =>
        {
            if (E.ClickedItem is ToolStripMenuItem MenuItem && GameMenuItems.ContainsValue(MenuItem))
            {
                PreventMenuClose = true;
            }
        };

        ContextMenu.Closing += (Sender, E) =>
        {
            if (E.CloseReason == ToolStripDropDownCloseReason.ItemClicked && PreventMenuClose)
            {
                E.Cancel = true;
                PreventMenuClose = false;
            }
        };
    }

    #endregion

    #region Event Handlers

    private void OnNotifyIconMouseClick(object? Sender, MouseEventArgs E)
    {
        if (E.Button == MouseButtons.Left && NotifyIconInstance != null)
        {
            try
            {
                MethodInfo? Method = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Method?.Invoke(NotifyIconInstance, null);
            }
            catch
            {
                // Fallback: standard right-click triggers menu
            }
        }
    }

    private void OnRpcStateUpdated(ServiceState State, GameMatchResult? Match)
    {
        SyncContext?.Post(_ =>
        {
            if (NotifyIconInstance == null || StatusMenuItem == null) return;

            switch (State)
            {
                case ServiceState.ActiveGame when Match != null:
                    StatusMenuItem.Text = $"🟢 Active: {Match.Profile.DisplayName}";
                    NotifyIconInstance.Text = TruncateTooltip($"Silent Hill RPC - Active: {Match.Profile.DisplayName}");
                    break;

                case ServiceState.Terminating:
                    StatusMenuItem.Text = "🟡 Terminating session...";
                    NotifyIconInstance.Text = TruncateTooltip("Silent Hill Discord RPC - Terminating...");
                    break;

                default:
                    StatusMenuItem.Text = "⚪ Status: Waiting for game...";
                    NotifyIconInstance.Text = TruncateTooltip("Silent Hill Discord RPC - Ready");
                    break;
            }
        }, null);
    }

    private void OnProfileToggledExternal(string Identifier, bool IsEnabled)
    {
        SyncContext?.Post(_ =>
        {
            if (GameMenuItems.TryGetValue(Identifier, out ToolStripMenuItem? Item))
            {
                Item.Checked = IsEnabled;
            }
        }, null);
    }

    #endregion

    #region Action Methods

    private void OpenConfigurationFile()
    {
        try
        {
            string? ConfigPath = FindAppSettingsPath();
            if (!string.IsNullOrWhiteSpace(ConfigPath) && File.Exists(ConfigPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ConfigPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("The file appsettings.json was not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception Ex)
        {
            Logger.LogError(Ex, "Failed to open configuration file.");
        }
    }

    private void OpenAssetsFolder()
    {
        try
        {
            string? Folder = FindAssetsPath();
            if (!string.IsNullOrWhiteSpace(Folder) && Directory.Exists(Folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Folder,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("The assets folder 'img' was not found.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception Ex)
        {
            Logger.LogError(Ex, "Failed to open assets folder.");
        }
    }

    private void ExitApplication()
    {
        Logger.LogInformation("Exit requested from System Tray menu.");

        if (NotifyIconInstance != null)
        {
            NotifyIconInstance.Visible = false;
            NotifyIconInstance.Dispose();
            NotifyIconInstance = null;
        }

        RequestShutdown();
        ExitThread();
    }

    #endregion

    #region Helper Methods

    private static Icon LoadApplicationIcon()
    {
        string IcoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(IcoPath))
        {
            try
            {
                return new Icon(IcoPath);
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
        using Bitmap Bmp = new Bitmap(32, 32);
        using (Graphics G = Graphics.FromImage(Bmp))
        {
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.Clear(Color.Transparent);

            using SolidBrush Brush = new SolidBrush(Color.FromArgb(180, 20, 20));
            G.FillEllipse(Brush, 2, 2, 28, 28);

            using Pen OutlinePen = new Pen(Color.White, 2f);
            G.DrawEllipse(OutlinePen, 6, 6, 20, 20);
        }

        return Icon.FromHandle(Bmp.GetHicon());
    }

    private static string TruncateTooltip(string Text)
    {
        if (Text.Length <= 63)
            return Text;

        return Text[..60] + "...";
    }

    private static string? FindAppSettingsPath()
    {
        string ExeDir = AppContext.BaseDirectory;
        string Direct = Path.Combine(ExeDir, "appsettings.json");
        if (File.Exists(Direct)) return Direct;

        string Cwd = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(Cwd)) return Cwd;

        string Src = Path.Combine(Directory.GetCurrentDirectory(), "src", "SilentHillEmulatorRPC", "appsettings.json");
        if (File.Exists(Src)) return Src;

        return Direct;
    }

    private static string? FindAssetsPath()
    {
        string ExeDir = AppContext.BaseDirectory;
        string Direct = Path.Combine(ExeDir, "img");
        if (Directory.Exists(Direct)) return Direct;

        string SrcImg = Path.Combine(Directory.GetCurrentDirectory(), "src", "img");
        if (Directory.Exists(SrcImg)) return SrcImg;

        string ParentImg = Path.Combine(Directory.GetCurrentDirectory(), "img");
        if (Directory.Exists(ParentImg)) return ParentImg;

        return null;
    }

    #endregion

    #region Disposal

    protected override void Dispose(bool Disposing)
    {
        if (Disposing)
        {
            if (NotifyIconInstance != null)
            {
                NotifyIconInstance.Visible = false;
                NotifyIconInstance.Dispose();
                NotifyIconInstance = null;
            }

            ContextMenu?.Dispose();
        }

        base.Dispose(Disposing);
    }

    #endregion
}
