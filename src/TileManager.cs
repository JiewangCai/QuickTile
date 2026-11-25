using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickTile
{
    public class TileManager : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly List<TileForm> _tileForms;
        private readonly Icon _appIcon;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _miTraySettings;
        private readonly ToolStripMenuItem _miTrayLockSize;
        private readonly ToolStripMenuItem _miTrayLockPos;
        private readonly ToolStripMenuItem _miTrayExit;
        private readonly Timer _topMostTimer;
        private readonly Timer _saveTimer;

        private ManagerForm _managerForm;
        private int _modalDialogDepth;
        private bool _exiting;

        public TileManager(AppConfig config)
        {
            _config = config ?? ConfigStore.CreateDefaultConfig();
            Lang.SetLanguage(_config.Language);
            if (_config.Profiles == null || _config.Profiles.Count == 0)
            {
                AppConfig fallback = ConfigStore.CreateDefaultConfig();
                _config.Profiles = fallback.Profiles;
                _config.ActiveProfileId = fallback.ActiveProfileId;
            }

            _tileForms = new List<TileForm>();
            _appIcon = CreateProgrammaticIcon();

            _trayMenu = new ContextMenuStrip();

            _miTraySettings = new ToolStripMenuItem(Lang.T("设置", "Settings"));
            _miTraySettings.Font = new Font(_miTraySettings.Font, FontStyle.Bold);
            _miTraySettings.Click += delegate { ShowManagerForm(null); };

            _miTrayLockSize = new ToolStripMenuItem(Lang.T("锁定大小", "Lock Size"));
            _miTrayLockSize.Click += delegate { SetLockSize(!_config.LockSize); };

            _miTrayLockPos = new ToolStripMenuItem(Lang.T("锁定位置", "Lock Position"));
            _miTrayLockPos.Click += delegate { SetLockPosition(!_config.LockPosition); };

            _miTrayExit = new ToolStripMenuItem(Lang.T("退出程序", "Exit"));
            _miTrayExit.Click += delegate { ExitApplication(); };

            _trayMenu.Items.Add(_miTraySettings);
            _trayMenu.Items.Add(_miTrayLockSize);
            _trayMenu.Items.Add(_miTrayLockPos);
            _trayMenu.Items.Add(_miTrayExit);
            _trayMenu.Opening += delegate { SyncTrayMenuChecks(); };

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _appIcon;
            _notifyIcon.Text = "QuickTile";
            _notifyIcon.ContextMenuStrip = _trayMenu;
            _notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowManagerForm(null);
                }
            };
            _notifyIcon.DoubleClick += delegate { ShowManagerForm(null); };
            _notifyIcon.Visible = true;

            _topMostTimer = new Timer();
            _topMostTimer.Interval = 2000;
            _topMostTimer.Tick += OnTopMostTimerTick;

            _saveTimer = new Timer();
            _saveTimer.Interval = 250;
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SaveConfigSafe();
            };

            SpawnInitialTiles();
            SyncTrayMenuChecks();
            _topMostTimer.Start();
        }

        public void BeginModalSuppression()
        {
            _modalDialogDepth++;
        }

        public void EndModalSuppression()
        {
            if (_modalDialogDepth > 0)
            {
                _modalDialogDepth--;
            }
        }

        public void AddNewTile()
        {
            TileProfile active = _config.GetActiveProfile();
            if (active == null)
            {
                return;
            }
            if (active.Tiles == null)
            {
                active.Tiles = new List<TileConfig>();
            }

            TileConfig created = CreateNewTileTemplate(active.Tiles);
            if (string.IsNullOrEmpty(created.Id))
            {
                created.Id = Guid.NewGuid().ToString("N");
            }

            active.Tiles.Add(created);
            TileForm form = CreateAndWireTileForm(created);
            _tileForms.Add(form);
            form.Show();
            if (_config.AlwaysOnTop && form.IsHandleCreated)
            {
                NativeMethods.SetTopMostNoActivate(form.Handle, true);
            }

            SaveConfigImmediate();
            ShowManagerForm(created.Id);
        }

        public void DeleteTile(string tileId)
        {
            List<TileConfig> activeTiles = _config.GetActiveTiles();
            if (activeTiles.Count <= 1)
            {
                return;
            }

            int index = FindTileConfigIndex(activeTiles, tileId);
            if (index < 0 || activeTiles.Count <= 1)
            {
                return;
            }

            activeTiles.RemoveAt(index);
            string nextSelectId = null;
            if (activeTiles.Count > 0)
            {
                int nextIdx = Math.Min(index, activeTiles.Count - 1);
                nextSelectId = activeTiles[nextIdx].Id;
            }

            for (int i = _tileForms.Count - 1; i >= 0; i--)
            {
                TileForm form = _tileForms[i];
                if (string.Equals(form.TileId, tileId, StringComparison.Ordinal))
                {
                    _tileForms.RemoveAt(i);
                    if (!form.IsDisposed)
                    {
                        form.Close();
                        form.Dispose();
                    }
                }
            }

            SaveConfigImmediate();
            RefreshManagerIfCreated(nextSelectId);
        }

        public void UpdateTileLive(TileConfig updated)
        {
            if (updated == null)
            {
                return;
            }

            List<TileConfig> activeTiles = _config.GetActiveTiles();
            int index = FindTileConfigIndex(activeTiles, updated.Id);
            if (index < 0)
            {
                return;
            }

            updated.Size = TileConfig.ClampSize(updated.Size);
            updated.Opacity = TileConfig.ClampOpacity(updated.Opacity);
            updated.ClickHoldMs = TileConfig.ClampClickHoldMs(updated.ClickHoldMs);
            updated.SyncLegacyModifiers();
            activeTiles[index] = updated;

            TileForm form = FindTileForm(updated.Id);
            if (form != null && !form.IsDisposed)
            {
                form.ApplyState(updated, _config.LockPosition, _config.LockSize, _config.AlwaysOnTop);
            }

            ScheduleDebouncedSave();
        }

        public void SetLockPosition(bool locked)
        {
            if (_config.LockPosition == locked)
            {
                return;
            }

            _config.LockPosition = locked;
            ApplyModeFlagsToForms();
            SyncTrayMenuChecks();
            SaveConfigImmediate();
            RefreshManagerIfCreated(null);
        }

        public void SetLockSize(bool locked)
        {
            if (_config.LockSize == locked)
            {
                return;
            }

            _config.LockSize = locked;
            ApplyModeFlagsToForms();
            SyncTrayMenuChecks();
            SaveConfigImmediate();
            RefreshManagerIfCreated(null);
        }

        public void SetAlwaysOnTop(bool alwaysOnTop)
        {
            if (_config.AlwaysOnTop == alwaysOnTop)
            {
                return;
            }

            _config.AlwaysOnTop = alwaysOnTop;
            ApplyModeFlagsToForms();
            SaveConfigImmediate();
            RefreshManagerIfCreated(null);
        }

        public void SetLanguage(string langCode)
        {
            string normalized = string.Equals(langCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
            if (string.Equals(_config.Language, normalized, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Lang.CurrentCode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _config.Language = normalized;
            Lang.SetLanguage(normalized);

            _miTraySettings.Text = Lang.T("设置", "Settings");
            _miTrayLockSize.Text = Lang.T("锁定大小", "Lock Size");
            _miTrayLockPos.Text = Lang.T("锁定位置", "Lock Position");
            _miTrayExit.Text = Lang.T("退出程序", "Exit");

            for (int i = 0; i < _tileForms.Count; i++)
            {
                TileForm form = _tileForms[i];
                if (form != null && !form.IsDisposed)
                {
                    form.RefreshLanguage();
                }
            }

            if (_managerForm != null && !_managerForm.IsDisposed)
            {
                _managerForm.ApplyLanguage();
                _managerForm.RefreshTiles(_config, null);
            }

            SaveConfigImmediate();
        }

        public void SwitchProfile(string profileId)
        {
            TileProfile target = FindProfile(profileId);
            if (target == null || string.Equals(_config.ActiveProfileId, target.Id, StringComparison.Ordinal))
            {
                return;
            }

            _config.ActiveProfileId = target.Id;
            if (target.Tiles == null)
            {
                target.Tiles = new List<TileConfig>();
            }
            if (target.Tiles.Count == 0)
            {
                target.Tiles.Add(ConfigStore.CreateSingleFallbackTile());
            }

            CloseAndClearTileForms();
            SpawnInitialTiles();
            SaveConfigImmediate();
            RefreshManagerIfCreated(target.Tiles[0].Id);
        }

        public void AddProfile()
        {
            if (_config.Profiles == null)
            {
                _config.Profiles = new List<TileProfile>();
            }

            int num = _config.Profiles.Count + 1;
            string name = string.Format(Lang.T("方案 {0}", "Profile {0}"), num);
            while (ProfileNameExists(name))
            {
                num++;
                name = string.Format(Lang.T("方案 {0}", "Profile {0}"), num);
            }

            TileProfile profile = new TileProfile();
            profile.Id = Guid.NewGuid().ToString("N");
            profile.Name = name;
            profile.Tiles.Add(ConfigStore.CreateSingleFallbackTile());

            _config.Profiles.Add(profile);
            _config.ActiveProfileId = profile.Id;

            CloseAndClearTileForms();
            SpawnInitialTiles();
            SaveConfigImmediate();
            RefreshManagerIfCreated(profile.Tiles[0].Id);
        }

        public void RenameProfile(string profileId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            TileProfile target = FindProfile(profileId);
            if (target == null)
            {
                return;
            }

            target.Name = newName.Trim();
            SaveConfigImmediate();
            RefreshManagerIfCreated(null);
        }

        public void DeleteProfile(string profileId)
        {
            if (_config.Profiles == null || _config.Profiles.Count <= 1)
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                if (_config.Profiles[i] != null && string.Equals(_config.Profiles[i].Id, profileId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0 || _config.Profiles.Count <= 1)
            {
                return;
            }

            bool deletingActive = string.Equals(_config.Profiles[index].Id, _config.ActiveProfileId, StringComparison.Ordinal);
            _config.Profiles.RemoveAt(index);

            string selectTileId = null;
            if (deletingActive)
            {
                int nextIdx = Math.Min(index, _config.Profiles.Count - 1);
                TileProfile nextProfile = _config.Profiles[nextIdx];
                _config.ActiveProfileId = nextProfile.Id;
                if (nextProfile.Tiles == null)
                {
                    nextProfile.Tiles = new List<TileConfig>();
                }
                if (nextProfile.Tiles.Count == 0)
                {
                    nextProfile.Tiles.Add(ConfigStore.CreateSingleFallbackTile());
                }
                selectTileId = nextProfile.Tiles[0].Id;

                CloseAndClearTileForms();
                SpawnInitialTiles();
            }

            SaveConfigImmediate();
            RefreshManagerIfCreated(selectTileId);
        }

        public void ShowManagerForm(string selectTileId)
        {
            if (_managerForm == null || _managerForm.IsDisposed)
            {
                _managerForm = new ManagerForm(this, _appIcon);
            }

            _managerForm.RefreshTiles(_config, selectTileId);
            if (!_managerForm.Visible)
            {
                _managerForm.Show();
            }
            if (_managerForm.WindowState == FormWindowState.Minimized)
            {
                _managerForm.WindowState = FormWindowState.Normal;
            }
            _managerForm.BringToFront();
            _managerForm.Activate();
        }

        public void ExitApplication()
        {
            if (_exiting)
            {
                return;
            }
            _exiting = true;

            SaveConfigImmediate();
            _topMostTimer.Stop();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }

            if (_managerForm != null && !_managerForm.IsDisposed)
            {
                _managerForm.ForceClose();
                _managerForm.Dispose();
                _managerForm = null;
            }

            CloseAndClearTileForms();
            ExitThread();
        }

        private void ApplyModeFlagsToForms()
        {
            for (int i = 0; i < _tileForms.Count; i++)
            {
                TileForm form = _tileForms[i];
                if (form != null && !form.IsDisposed)
                {
                    form.SetModeFlags(_config.LockPosition, _config.LockSize, _config.AlwaysOnTop);
                }
            }
        }

        private void CloseAndClearTileForms()
        {
            for (int i = _tileForms.Count - 1; i >= 0; i--)
            {
                TileForm form = _tileForms[i];
                if (form != null && !form.IsDisposed)
                {
                    form.Close();
                    form.Dispose();
                }
            }
            _tileForms.Clear();
        }

        private void SpawnInitialTiles()
        {
            List<TileConfig> activeTiles = _config.GetActiveTiles();
            for (int i = 0; i < activeTiles.Count; i++)
            {
                TileConfig tile = activeTiles[i];
                if (string.IsNullOrEmpty(tile.Id))
                {
                    tile.Id = Guid.NewGuid().ToString("N");
                }
                TileForm form = CreateAndWireTileForm(tile);
                _tileForms.Add(form);
                form.Show();
            }
        }

        private TileForm CreateAndWireTileForm(TileConfig tile)
        {
            TileForm form = new TileForm(tile, _config.LockPosition, _config.LockSize, _config.AlwaysOnTop);
            string capturedId = tile.Id;

            form.TileGeometryLiveChanged += delegate
            {
                if (_managerForm != null && !_managerForm.IsDisposed && _managerForm.Visible)
                {
                    _managerForm.NotifyTileGeometryChanged(form.Config);
                }
            };
            form.TileChanged += delegate
            {
                SaveConfigImmediate();
                if (_managerForm != null && !_managerForm.IsDisposed)
                {
                    _managerForm.NotifyTileGeometryChanged(form.Config);
                }
            };
            form.OpenSettingsRequested += delegate { ShowManagerForm(capturedId); };
            form.ToggleLockSizeRequested += delegate { SetLockSize(!_config.LockSize); };
            form.ToggleLockPositionRequested += delegate { SetLockPosition(!_config.LockPosition); };
            form.ExitRequested += delegate { ExitApplication(); };
            form.ContextMenuOpened += delegate { BeginModalSuppression(); };
            form.ContextMenuClosed += delegate { EndModalSuppression(); };

            return form;
        }

        private void OnTopMostTimerTick(object sender, EventArgs e)
        {
            if (!_config.AlwaysOnTop || _modalDialogDepth > 0 || _exiting)
            {
                return;
            }

            for (int i = 0; i < _tileForms.Count; i++)
            {
                TileForm form = _tileForms[i];
                if (form != null && !form.IsDisposed && form.Visible && form.IsHandleCreated)
                {
                    NativeMethods.SetTopMostNoActivate(form.Handle, true);
                }
            }
        }

        private static TileConfig CreateNewTileTemplate(List<TileConfig> activeTiles)
        {
            TileConfig tile = new TileConfig();
            tile.Label = Lang.T("新方块", "New Tile");
            tile.ModLCtrl = true;
            tile.SyncLegacyModifiers();
            tile.VirtualKey = (int)Keys.C;

            if (TilePalettes.Presets != null && TilePalettes.Presets.Length > 0)
            {
                int count = activeTiles != null ? activeTiles.Count : 0;
                TilePaletteItem palette = TilePalettes.Presets[count % TilePalettes.Presets.Length];
                tile.BackColorHex = palette.BackHex;
                tile.AccentColorHex = palette.AccentHex;
                tile.TextColorHex = palette.TextHex;
            }

            Rectangle wa = (Screen.PrimaryScreen != null)
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1920, 1080);

            if (activeTiles != null && activeTiles.Count > 0)
            {
                TileConfig last = activeTiles[activeTiles.Count - 1];
                tile.Size = TileConfig.ClampSize(last.Size);
                tile.X = last.X;
                tile.Y = last.Y + tile.Size + 10;
                if (tile.Y + tile.Size > wa.Bottom - 16)
                {
                    tile.X = last.X - tile.Size - 10;
                    tile.Y = wa.Top + 40;
                }
            }
            else
            {
                tile.Size = 68;
                tile.X = wa.Right - tile.Size - 24;
                tile.Y = wa.Top + Math.Max(20, (wa.Height - tile.Size) / 2);
            }

            return tile;
        }

        private TileProfile FindProfile(string profileId)
        {
            if (_config.Profiles == null || string.IsNullOrEmpty(profileId))
            {
                return null;
            }
            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                if (_config.Profiles[i] != null && string.Equals(_config.Profiles[i].Id, profileId, StringComparison.Ordinal))
                {
                    return _config.Profiles[i];
                }
            }
            return null;
        }

        private bool ProfileNameExists(string name)
        {
            if (_config.Profiles == null)
            {
                return false;
            }
            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                if (_config.Profiles[i] != null && string.Equals(_config.Profiles[i].Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static int FindTileConfigIndex(List<TileConfig> tiles, string tileId)
        {
            if (tiles == null || string.IsNullOrEmpty(tileId))
            {
                return -1;
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                if (string.Equals(tiles[i].Id, tileId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private TileForm FindTileForm(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
            {
                return null;
            }
            for (int i = 0; i < _tileForms.Count; i++)
            {
                if (string.Equals(_tileForms[i].TileId, tileId, StringComparison.Ordinal))
                {
                    return _tileForms[i];
                }
            }
            return null;
        }

        private void SyncTrayMenuChecks()
        {
            _miTrayLockSize.Checked = _config.LockSize;
            _miTrayLockPos.Checked = _config.LockPosition;
        }

        private void RefreshManagerIfCreated(string selectTileId)
        {
            if (_managerForm != null && !_managerForm.IsDisposed)
            {
                _managerForm.RefreshTiles(_config, selectTileId);
            }
        }

        private void ScheduleDebouncedSave()
        {
            if (_exiting)
            {
                return;
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveConfigImmediate()
        {
            _saveTimer.Stop();
            SaveConfigSafe();
        }

        private void SaveConfigSafe()
        {
            try
            {
                ConfigStore.Save(_config);
            }
            catch
            {
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon CreateProgrammaticIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
                using (Pen borderPen = new Pen(Color.FromArgb(88, 166, 255), 1.5f))
                {
                    g.FillRectangle(bgBrush, 1, 1, 30, 30);
                    g.DrawRectangle(borderPen, 1, 1, 29, 29);
                }

                using (SolidBrush tileBrush = new SolidBrush(Color.FromArgb(220, 232, 248)))
                using (SolidBrush accentBrush = new SolidBrush(Color.FromArgb(88, 166, 255)))
                {
                    g.FillRectangle(tileBrush, 5, 5, 9, 9);
                    g.FillRectangle(accentBrush, 18, 5, 9, 9);
                    g.FillRectangle(tileBrush, 5, 18, 9, 9);
                    g.FillRectangle(tileBrush, 18, 18, 9, 9);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tmp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_saveTimer != null)
                {
                    _saveTimer.Stop();
                    _saveTimer.Dispose();
                }
                if (_topMostTimer != null)
                {
                    _topMostTimer.Stop();
                    _topMostTimer.Dispose();
                }
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                if (_trayMenu != null)
                {
                    _trayMenu.Dispose();
                }
                if (_managerForm != null && !_managerForm.IsDisposed)
                {
                    _managerForm.ForceClose();
                    _managerForm.Dispose();
                }
                CloseAndClearTileForms();
                if (_appIcon != null)
                {
                    _appIcon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
