using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickTile
{
    public class ManagerForm : Form
    {
        private sealed class KeyItem
        {
            public readonly int VirtualKey;
            public readonly string DisplayName;

            public KeyItem(int virtualKey, string displayName)
            {
                VirtualKey = virtualKey;
                DisplayName = displayName;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class ProfileItem
        {
            public readonly string Id;
            public readonly string Name;

            public ProfileItem(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private sealed class PreviewPanel : Panel
        {
            public PreviewPanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);
                DoubleBuffered = true;
            }
        }

        private readonly TileManager _manager;
        private readonly NativeMethods.LowLevelKeyboardProc _llHookCallback;
        private IntPtr _llHookHandle;

        private AppConfig _currentConfig;
        private string _selectedTileId;
        private TileConfig _working;

        private bool _allowClose;
        private bool _suppressUiEvents;
        private bool _updatingHotkeyUi;
        private bool _isRecordingHotkey;
        private bool _columnsScaled;

        private bool _recLCtrl;
        private bool _recRCtrl;
        private bool _recLAlt;
        private bool _recRAlt;
        private bool _recLShift;
        private bool _recRShift;
        private bool _recLWin;
        private bool _recRWin;
        private int _swallowNextUpVk;

        private ComboBox _cmbProfile;
        private Button _btnAddProfile;
        private Button _btnRenameProfile;
        private Button _btnDeleteProfile;
        private CheckBox _chkTopMost;
        private CheckBox _chkLockPosition;
        private CheckBox _chkLockSize;

        private Button _btnAddTile;
        private Button _btnDeleteTile;
        private ListView _listTiles;

        private Panel _editorHost;
        private GroupBox _grpAppearance;
        private GroupBox _grpAction;

        private TextBox _txtLabel;
        private TrackBar _trkSize;
        private NumericUpDown _numSize;
        private NumericUpDown _numPosX;
        private NumericUpDown _numPosY;
        private TrackBar _trkOpacity;
        private Label _lblOpacityVal;
        private FlowLayoutPanel _palettePanel;
        private Button _btnCustomColor;
        private PreviewPanel _previewPanel;

        private RadioButton _rbHotkey;
        private RadioButton _rbAnchor;
        private Panel _hotkeyPanel;
        private Button _btnRecordHotkey;
        private Button _btnClearHotkey;
        private TextBox _txtHotkeyInput;
        private Button _btnApplyHotkeyText;
        private CheckBox _chkLCtrl;
        private CheckBox _chkLAlt;
        private CheckBox _chkLShift;
        private CheckBox _chkLWin;
        private CheckBox _chkRCtrl;
        private CheckBox _chkRAlt;
        private CheckBox _chkRShift;
        private CheckBox _chkRWin;
        private ComboBox _cmbVirtualKey;
        private Label _lblCurrentHotkey;

        private Panel _anchorPanel;
        private NumericUpDown _numAnchorX;
        private NumericUpDown _numAnchorY;
        private Button _btnPickScreen;
        private ComboBox _cmbClickType;
        private CheckBox _chkRestoreCursor;
        private NumericUpDown _numClickHoldMs;

        private Button _btnCloseSettings;
        private Button _btnExitApp;

        public ManagerForm(TileManager manager, Icon appIcon)
        {
            if (manager == null)
            {
                throw new ArgumentNullException("manager");
            }
            _manager = manager;
            _llHookCallback = OnLowLevelKeyboardEvent;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "QuickTile 设置";
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(780, 530);
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            InitializeControls();
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!_columnsScaled)
            {
                _columnsScaled = true;
                float scale = 1f;
                using (Graphics g = CreateGraphics())
                {
                    if (g.DpiX > 96f)
                    {
                        scale = g.DpiX / 96f;
                    }
                }
                if (scale > 1.05f && _listTiles != null && _listTiles.Columns.Count >= 3)
                {
                    _listTiles.Columns[0].Width = (int)Math.Round(78 * scale);
                    _listTiles.Columns[1].Width = (int)Math.Round(130 * scale);
                    _listTiles.Columns[2].Width = (int)Math.Round(54 * scale);
                }
            }
        }

        public void ForceClose()
        {
            _allowClose = true;
            StopHotkeyRecording();
            if (!IsDisposed)
            {
                Close();
            }
        }

        public void RefreshTiles(AppConfig config, string selectTileId)
        {
            if (config == null || IsDisposed)
            {
                return;
            }

            _currentConfig = config;
            string targetId = !string.IsNullOrEmpty(selectTileId) ? selectTileId : _selectedTileId;

            _suppressUiEvents = true;
            try
            {
                _chkTopMost.Checked = config.AlwaysOnTop;
                _chkLockPosition.Checked = config.LockPosition;
                _chkLockSize.Checked = config.LockSize;

                _cmbProfile.BeginUpdate();
                try
                {
                    _cmbProfile.Items.Clear();
                    TileProfile activeProfile = config.GetActiveProfile();
                    string activeProfileId = activeProfile != null ? activeProfile.Id : string.Empty;
                    int activeIdx = -1;

                    if (config.Profiles != null)
                    {
                        for (int i = 0; i < config.Profiles.Count; i++)
                        {
                            TileProfile p = config.Profiles[i];
                            if (p == null)
                            {
                                continue;
                            }
                            _cmbProfile.Items.Add(new ProfileItem(p.Id, p.Name));
                            if (string.Equals(p.Id, activeProfileId, StringComparison.Ordinal))
                            {
                                activeIdx = _cmbProfile.Items.Count - 1;
                            }
                        }
                    }

                    if (activeIdx >= 0)
                    {
                        _cmbProfile.SelectedIndex = activeIdx;
                    }
                    else if (_cmbProfile.Items.Count > 0)
                    {
                        _cmbProfile.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _cmbProfile.EndUpdate();
                }

                int profileCount = (config.Profiles != null) ? config.Profiles.Count : 0;
                _btnDeleteProfile.Enabled = (profileCount > 1);
                _btnRenameProfile.Enabled = (profileCount > 0);

                List<TileConfig> activeTiles = config.GetActiveTiles();

                _listTiles.BeginUpdate();
                try
                {
                    _listTiles.Items.Clear();
                    for (int i = 0; i < activeTiles.Count; i++)
                    {
                        TileConfig tile = activeTiles[i];
                        ListViewItem item = new ListViewItem(string.IsNullOrEmpty(tile.Label) ? "宏" : tile.Label);
                        item.SubItems.Add(tile.GetSubtitle());
                        item.SubItems.Add(string.Format("{0}px", TileConfig.ClampSize(tile.Size)));
                        item.Tag = tile.Id;

                        if (!string.IsNullOrEmpty(targetId) && string.Equals(tile.Id, targetId, StringComparison.Ordinal))
                        {
                            item.Selected = true;
                        }
                        _listTiles.Items.Add(item);
                    }

                    if (_listTiles.SelectedItems.Count == 0 && _listTiles.Items.Count > 0)
                    {
                        _listTiles.Items[0].Selected = true;
                    }
                }
                finally
                {
                    _listTiles.EndUpdate();
                }

                LoadSelectedTileIntoEditor();
                UpdateDeleteTileButtonState();
            }
            finally
            {
                _suppressUiEvents = false;
            }
        }

        public void NotifyTileGeometryChanged(TileConfig tile)
        {
            if (tile == null || IsDisposed)
            {
                return;
            }

            for (int i = 0; i < _listTiles.Items.Count; i++)
            {
                ListViewItem item = _listTiles.Items[i];
                string id = item.Tag as string;
                if (string.Equals(id, tile.Id, StringComparison.Ordinal))
                {
                    string sizeText = string.Format("{0}px", TileConfig.ClampSize(tile.Size));
                    if (item.SubItems.Count > 2 && !string.Equals(item.SubItems[2].Text, sizeText, StringComparison.Ordinal))
                    {
                        item.SubItems[2].Text = sizeText;
                    }
                    break;
                }
            }

            if (_working != null && string.Equals(_selectedTileId, tile.Id, StringComparison.Ordinal))
            {
                _suppressUiEvents = true;
                try
                {
                    int sz = TileConfig.ClampSize(tile.Size);
                    _working.X = tile.X;
                    _working.Y = tile.Y;
                    _working.Size = sz;

                    _numPosX.Value = ClampDecimal(_numPosX, tile.X);
                    _numPosY.Value = ClampDecimal(_numPosY, tile.Y);
                    _trkSize.Value = sz;
                    _numSize.Value = sz;
                    _previewPanel.Invalidate();
                }
                finally
                {
                    _suppressUiEvents = false;
                }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (_isRecordingHotkey)
            {
                StopHotkeyRecording();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                StopHotkeyRecording();
                Hide();
                return;
            }
            StopHotkeyRecording();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isRecordingHotkey && _llHookHandle == IntPtr.Zero)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                if (IsPureModifierKey(keyCode))
                {
                    return true;
                }

                bool lCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0;
                bool rCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0;
                if (!lCtrl && !rCtrl && (keyData & Keys.Control) != 0)
                {
                    lCtrl = true;
                }

                bool lAlt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU) & 0x8000) != 0;
                bool rAlt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0;
                if (!lAlt && !rAlt && (keyData & Keys.Alt) != 0)
                {
                    lAlt = true;
                }

                bool lShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LSHIFT) & 0x8000) != 0;
                bool rShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RSHIFT) & 0x8000) != 0;
                if (!lShift && !rShift && (keyData & Keys.Shift) != 0)
                {
                    lShift = true;
                }

                bool lWin = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0;
                bool rWin = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                CompleteRecordedHotkey(lCtrl, rCtrl, lAlt, rAlt, lShift, rShift, lWin, rWin, (int)keyCode);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private IntPtr OnLowLevelKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isDown = (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN);
                bool isUp = (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP);

                NativeMethods.KBDLLHOOKSTRUCT kb =
                    (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                int vk = (int)kb.vkCode;

                if (!_isRecordingHotkey)
                {
                    if (_swallowNextUpVk != 0 && vk == _swallowNextUpVk && isUp)
                    {
                        _swallowNextUpVk = 0;
                        return (IntPtr)1;
                    }
                    return NativeMethods.CallNextHookEx(_llHookHandle, nCode, wParam, lParam);
                }

                if (isDown || isUp)
                {
                    bool handledMod = UpdateRecordingModifierState(vk, kb.scanCode, kb.flags, isDown);
                    if (handledMod)
                    {
                        BeginInvoke(new MethodInvoker(UpdateRecordingHintText));
                        return (IntPtr)1;
                    }

                    if (isDown)
                    {
                        bool cLC = _recLCtrl;
                        bool cRC = _recRCtrl;
                        bool cLA = _recLAlt;
                        bool cRA = _recRAlt;
                        bool cLS = _recLShift;
                        bool cRS = _recRShift;
                        bool cLW = _recLWin;
                        bool cRW = _recRWin;

                        _swallowNextUpVk = vk;
                        _isRecordingHotkey = false;

                        BeginInvoke(new MethodInvoker(delegate
                        {
                            CompleteRecordedHotkey(cLC, cRC, cLA, cRA, cLS, cRS, cLW, cRW, vk);
                        }));
                        return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_llHookHandle, nCode, wParam, lParam);
        }

        private bool UpdateRecordingModifierState(int vk, uint scanCode, uint flags, bool isDown)
        {
            switch (vk)
            {
                case NativeMethods.VK_LCONTROL:
                    _recLCtrl = isDown;
                    return true;
                case NativeMethods.VK_RCONTROL:
                    _recRCtrl = isDown;
                    return true;
                case NativeMethods.VK_CONTROL:
                    if ((flags & NativeMethods.LLKHF_EXTENDED) != 0)
                    {
                        _recRCtrl = isDown;
                    }
                    else
                    {
                        _recLCtrl = isDown;
                    }
                    return true;
                case NativeMethods.VK_LMENU:
                    _recLAlt = isDown;
                    return true;
                case NativeMethods.VK_RMENU:
                    _recRAlt = isDown;
                    return true;
                case NativeMethods.VK_MENU:
                    if ((flags & NativeMethods.LLKHF_EXTENDED) != 0)
                    {
                        _recRAlt = isDown;
                    }
                    else
                    {
                        _recLAlt = isDown;
                    }
                    return true;
                case NativeMethods.VK_LSHIFT:
                    _recLShift = isDown;
                    return true;
                case NativeMethods.VK_RSHIFT:
                    _recRShift = isDown;
                    return true;
                case NativeMethods.VK_SHIFT:
                    if (scanCode == 0x36)
                    {
                        _recRShift = isDown;
                    }
                    else
                    {
                        _recLShift = isDown;
                    }
                    return true;
                case NativeMethods.VK_LWIN:
                    _recLWin = isDown;
                    return true;
                case NativeMethods.VK_RWIN:
                    _recRWin = isDown;
                    return true;
            }
            return false;
        }

        private void UpdateRecordingHintText()
        {
            if (!_isRecordingHotkey || IsDisposed)
            {
                return;
            }

            string held = NativeMethods.FormatHotkey(
                _recLCtrl, _recRCtrl,
                _recLAlt, _recRAlt,
                _recLShift, _recRShift,
                _recLWin, _recRWin,
                0);
            if (string.Equals(held, "未设置", StringComparison.Ordinal))
            {
                _btnRecordHotkey.Text = "按下快捷键...";
            }
            else
            {
                _btnRecordHotkey.Text = string.Format("{0}+...", held);
            }
        }

        private void CompleteRecordedHotkey(
            bool lCtrl,
            bool rCtrl,
            bool lAlt,
            bool rAlt,
            bool lShift,
            bool rShift,
            bool lWin,
            bool rWin,
            int vk)
        {
            StopHotkeyRecording();

            if (_working == null || IsDisposed)
            {
                return;
            }

            _working.ModLCtrl = lCtrl;
            _working.ModRCtrl = rCtrl;
            _working.ModLAlt = lAlt;
            _working.ModRAlt = rAlt;
            _working.ModLShift = lShift;
            _working.ModRShift = rShift;
            _working.ModLWin = lWin;
            _working.ModRWin = rWin;
            _working.SyncLegacyModifiers();
            _working.VirtualKey = vk;

            SyncHotkeyControlsFromWorking(true, null);
            PushWorkingTileLive();
        }

        private static bool IsPureModifierKey(Keys key)
        {
            return key == Keys.None ||
                key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                key == Keys.LWin || key == Keys.RWin;
        }

        private void InitializeControls()
        {
            // Top Bar: Profile CRUD + Lock/TopMost Flags
            Label lblProfile = new Label();
            lblProfile.Text = "方案:";
            lblProfile.AutoSize = true;
            lblProfile.Location = new Point(14, 17);
            Controls.Add(lblProfile);

            _cmbProfile = new ComboBox();
            _cmbProfile.Location = new Point(58, 13);
            _cmbProfile.Size = new Size(150, 24);
            _cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbProfile.SelectedIndexChanged += delegate
            {
                if (_suppressUiEvents) return;
                ProfileItem item = _cmbProfile.SelectedItem as ProfileItem;
                if (item != null)
                {
                    StopHotkeyRecording();
                    _manager.SwitchProfile(item.Id);
                }
            };
            Controls.Add(_cmbProfile);

            _btnAddProfile = CreateToolbarButton("新建", 214, 12, 54, 28);
            _btnAddProfile.Click += delegate
            {
                StopHotkeyRecording();
                _manager.AddProfile();
            };
            Controls.Add(_btnAddProfile);

            _btnRenameProfile = CreateToolbarButton("重命名", 272, 12, 64, 28);
            _btnRenameProfile.Click += OnRenameProfileClick;
            Controls.Add(_btnRenameProfile);

            _btnDeleteProfile = CreateToolbarButton("删除", 340, 12, 54, 28);
            _btnDeleteProfile.Click += delegate
            {
                ProfileItem item = _cmbProfile.SelectedItem as ProfileItem;
                if (item != null)
                {
                    StopHotkeyRecording();
                    _manager.DeleteProfile(item.Id);
                }
            };
            Controls.Add(_btnDeleteProfile);

            _chkTopMost = new CheckBox();
            _chkTopMost.Text = "始终置顶";
            _chkTopMost.AutoSize = true;
            _chkTopMost.Location = new Point(436, 16);
            _chkTopMost.CheckedChanged += delegate
            {
                if (_suppressUiEvents) return;
                _manager.SetAlwaysOnTop(_chkTopMost.Checked);
            };
            Controls.Add(_chkTopMost);

            _chkLockPosition = new CheckBox();
            _chkLockPosition.Text = "锁定位置";
            _chkLockPosition.AutoSize = true;
            _chkLockPosition.Location = new Point(536, 16);
            _chkLockPosition.CheckedChanged += delegate
            {
                if (_suppressUiEvents) return;
                _manager.SetLockPosition(_chkLockPosition.Checked);
            };
            Controls.Add(_chkLockPosition);

            _chkLockSize = new CheckBox();
            _chkLockSize.Text = "锁定大小";
            _chkLockSize.AutoSize = true;
            _chkLockSize.Location = new Point(636, 16);
            _chkLockSize.CheckedChanged += delegate
            {
                if (_suppressUiEvents) return;
                _manager.SetLockSize(_chkLockSize.Checked);
            };
            Controls.Add(_chkLockSize);

            // Left Pane: Tile Add/Delete + List
            _btnAddTile = CreateToolbarButton("+ 新增方块", 14, 52, 132, 28);
            _btnAddTile.Click += delegate
            {
                StopHotkeyRecording();
                _manager.AddNewTile();
            };
            Controls.Add(_btnAddTile);

            _btnDeleteTile = CreateToolbarButton("删除方块", 154, 52, 132, 28);
            _btnDeleteTile.Click += delegate
            {
                if (!string.IsNullOrEmpty(_selectedTileId))
                {
                    StopHotkeyRecording();
                    _manager.DeleteTile(_selectedTileId);
                }
            };
            Controls.Add(_btnDeleteTile);

            _listTiles = new ListView();
            _listTiles.Location = new Point(14, 88);
            _listTiles.Size = new Size(272, 418);
            _listTiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _listTiles.View = View.Details;
            _listTiles.FullRowSelect = true;
            _listTiles.GridLines = true;
            _listTiles.HideSelection = false;
            _listTiles.MultiSelect = false;

            _listTiles.Columns.Add("名称", 78);
            _listTiles.Columns.Add("触发内容", 130);
            _listTiles.Columns.Add("大小", 54);
            _listTiles.SelectedIndexChanged += delegate
            {
                if (_suppressUiEvents) return;
                StopHotkeyRecording();
                LoadSelectedTileIntoEditor();
                UpdateDeleteTileButtonState();
            };
            Controls.Add(_listTiles);

            // Right Pane: Inline Live Editor
            _editorHost = new Panel();
            _editorHost.Location = new Point(300, 52);
            _editorHost.Size = new Size(506, 454);
            _editorHost.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_editorHost);

            BuildAppearanceGroup();
            BuildActionGroup();

            // Bottom Bar
            _btnCloseSettings = new Button();
            _btnCloseSettings.Text = "关闭";
            _btnCloseSettings.Location = new Point(624, 516);
            _btnCloseSettings.Size = new Size(86, 30);
            _btnCloseSettings.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCloseSettings.Click += delegate
            {
                StopHotkeyRecording();
                Hide();
            };
            Controls.Add(_btnCloseSettings);

            _btnExitApp = new Button();
            _btnExitApp.Text = "退出程序";
            _btnExitApp.Location = new Point(718, 516);
            _btnExitApp.Size = new Size(88, 30);
            _btnExitApp.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnExitApp.Click += delegate
            {
                _manager.ExitApplication();
            };
            Controls.Add(_btnExitApp);
        }

        private void BuildAppearanceGroup()
        {
            _grpAppearance = new GroupBox();
            _grpAppearance.Text = "外观与位置";
            _grpAppearance.Location = new Point(0, 0);
            _grpAppearance.Size = new Size(506, 212);
            _grpAppearance.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _editorHost.Controls.Add(_grpAppearance);

            Label lblName = new Label();
            lblName.Text = "名称:";
            lblName.AutoSize = true;
            lblName.Location = new Point(12, 27);
            _grpAppearance.Controls.Add(lblName);

            _txtLabel = new TextBox();
            _txtLabel.Location = new Point(66, 24);
            _txtLabel.Size = new Size(196, 24);
            _txtLabel.TextChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                string txt = _txtLabel.Text.Trim();
                _working.Label = string.IsNullOrEmpty(txt) ? "宏" : txt;
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_txtLabel);

            Label lblSize = new Label();
            lblSize.Text = "大小:";
            lblSize.AutoSize = true;
            lblSize.Location = new Point(12, 63);
            _grpAppearance.Controls.Add(lblSize);

            _trkSize = new TrackBar();
            _trkSize.Location = new Point(62, 58);
            _trkSize.Size = new Size(134, 28);
            _trkSize.Minimum = 28;
            _trkSize.Maximum = 320;
            _trkSize.TickStyle = TickStyle.None;
            _trkSize.AutoSize = false;
            _trkSize.Scroll += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _suppressUiEvents = true;
                try
                {
                    if ((int)_numSize.Value != _trkSize.Value)
                    {
                        _numSize.Value = _trkSize.Value;
                    }
                    _working.Size = _trkSize.Value;
                }
                finally
                {
                    _suppressUiEvents = false;
                }
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_trkSize);

            _numSize = new NumericUpDown();
            _numSize.Location = new Point(200, 60);
            _numSize.Size = new Size(62, 24);
            _numSize.Minimum = 28;
            _numSize.Maximum = 320;
            _numSize.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                int val = (int)_numSize.Value;
                _suppressUiEvents = true;
                try
                {
                    if (_trkSize.Value != val)
                    {
                        _trkSize.Value = val;
                    }
                    _working.Size = val;
                }
                finally
                {
                    _suppressUiEvents = false;
                }
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_numSize);

            _previewPanel = new PreviewPanel();
            _previewPanel.Location = new Point(406, 20);
            _previewPanel.Size = new Size(86, 86);
            _previewPanel.BorderStyle = BorderStyle.FixedSingle;
            _previewPanel.Paint += OnPreviewPaint;
            _grpAppearance.Controls.Add(_previewPanel);

            Label lblPosX = new Label();
            lblPosX.Text = "位置 X:";
            lblPosX.AutoSize = true;
            lblPosX.Location = new Point(12, 125);
            _grpAppearance.Controls.Add(lblPosX);
            lblPosX.BringToFront();

            _numPosX = new NumericUpDown();
            _numPosX.Location = new Point(68, 122);
            _numPosX.Size = new Size(76, 24);
            _numPosX.Minimum = -32768;
            _numPosX.Maximum = 32768;
            _numPosX.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.X = (int)_numPosX.Value;
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_numPosX);
            _numPosX.BringToFront();

            Label lblPosY = new Label();
            lblPosY.Text = "Y:";
            lblPosY.AutoSize = true;
            lblPosY.Location = new Point(152, 125);
            _grpAppearance.Controls.Add(lblPosY);
            lblPosY.BringToFront();

            _numPosY = new NumericUpDown();
            _numPosY.Location = new Point(172, 122);
            _numPosY.Size = new Size(76, 24);
            _numPosY.Minimum = -32768;
            _numPosY.Maximum = 32768;
            _numPosY.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.Y = (int)_numPosY.Value;
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_numPosY);
            _numPosY.BringToFront();

            Label lblOpacity = new Label();
            lblOpacity.Text = "透明度:";
            lblOpacity.AutoSize = true;
            lblOpacity.Location = new Point(262, 125);
            _grpAppearance.Controls.Add(lblOpacity);
            lblOpacity.BringToFront();

            _trkOpacity = new TrackBar();
            _trkOpacity.Location = new Point(322, 120);
            _trkOpacity.Size = new Size(92, 28);
            _trkOpacity.Minimum = 25;
            _trkOpacity.Maximum = 100;
            _trkOpacity.TickStyle = TickStyle.None;
            _trkOpacity.AutoSize = false;
            _trkOpacity.Scroll += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.Opacity = _trkOpacity.Value / 100.0;
                _lblOpacityVal.Text = string.Format("{0}%", _trkOpacity.Value);
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _grpAppearance.Controls.Add(_trkOpacity);

            _lblOpacityVal = new Label();
            _lblOpacityVal.AutoSize = true;
            _lblOpacityVal.Location = new Point(420, 125);
            _grpAppearance.Controls.Add(_lblOpacityVal);
            _lblOpacityVal.BringToFront();

            Label lblColor = new Label();
            lblColor.Text = "配色:";
            lblColor.AutoSize = true;
            lblColor.Location = new Point(12, 174);
            _grpAppearance.Controls.Add(lblColor);
            lblColor.BringToFront();

            _palettePanel = new FlowLayoutPanel();
            _palettePanel.Location = new Point(68, 170);
            _palettePanel.Size = new Size(266, 28);
            _palettePanel.WrapContents = false;
            _palettePanel.Margin = new Padding(0);
            _palettePanel.Padding = new Padding(0);
            BuildPaletteButtons();
            _grpAppearance.Controls.Add(_palettePanel);
            _palettePanel.BringToFront();

            _btnCustomColor = new Button();
            _btnCustomColor.Text = "自定义...";
            _btnCustomColor.Location = new Point(346, 169);
            _btnCustomColor.Size = new Size(86, 26);
            _btnCustomColor.Click += OnCustomColorClick;
            _grpAppearance.Controls.Add(_btnCustomColor);
            _btnCustomColor.BringToFront();
        }

        private void BuildPaletteButtons()
        {
            if (TilePalettes.Presets == null)
            {
                return;
            }

            for (int i = 0; i < TilePalettes.Presets.Length; i++)
            {
                TilePaletteItem preset = TilePalettes.Presets[i];
                Button btn = new Button();
                btn.Size = new Size(26, 22);
                btn.Margin = new Padding(0, 1, 5, 1);
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;
                btn.BackColor = TilePalettes.FromHex(preset.BackHex, Color.FromArgb(30, 34, 44));
                btn.FlatAppearance.BorderColor = TilePalettes.FromHex(preset.AccentHex, Color.FromArgb(88, 166, 255));
                btn.Text = "";

                TilePaletteItem captured = preset;
                btn.Click += delegate
                {
                    if (_working == null) return;
                    _working.BackColorHex = captured.BackHex;
                    _working.AccentColorHex = captured.AccentHex;
                    _working.TextColorHex = captured.TextHex;
                    _previewPanel.Invalidate();
                    PushWorkingTileLive();
                };
                _palettePanel.Controls.Add(btn);
            }
        }

        private void BuildActionGroup()
        {
            _grpAction = new GroupBox();
            _grpAction.Text = "触发动作";
            _grpAction.Location = new Point(0, 220);
            _grpAction.Size = new Size(506, 234);
            _grpAction.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _editorHost.Controls.Add(_grpAction);

            _rbHotkey = new RadioButton();
            _rbHotkey.Text = "快捷键";
            _rbHotkey.AutoSize = true;
            _rbHotkey.Location = new Point(14, 22);
            _rbHotkey.CheckedChanged += OnModeChanged;
            _grpAction.Controls.Add(_rbHotkey);

            _rbAnchor = new RadioButton();
            _rbAnchor.Text = "锚定点击";
            _rbAnchor.AutoSize = true;
            _rbAnchor.Location = new Point(110, 22);
            _rbAnchor.CheckedChanged += OnModeChanged;
            _grpAction.Controls.Add(_rbAnchor);

            BuildHotkeyPanel();
            BuildAnchorPanel();
        }

        private void BuildHotkeyPanel()
        {
            _hotkeyPanel = new Panel();
            _hotkeyPanel.Location = new Point(12, 48);
            _hotkeyPanel.Size = new Size(482, 178);
            _hotkeyPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grpAction.Controls.Add(_hotkeyPanel);

            _btnRecordHotkey = new Button();
            _btnRecordHotkey.Text = "录制快捷键";
            _btnRecordHotkey.Location = new Point(0, 2);
            _btnRecordHotkey.Size = new Size(210, 28);
            _btnRecordHotkey.UseVisualStyleBackColor = true;
            _btnRecordHotkey.Click += delegate
            {
                if (_working == null) return;
                if (_isRecordingHotkey)
                {
                    StopHotkeyRecording();
                }
                else
                {
                    StartHotkeyRecording();
                }
            };
            _hotkeyPanel.Controls.Add(_btnRecordHotkey);

            _btnClearHotkey = new Button();
            _btnClearHotkey.Text = "清空";
            _btnClearHotkey.Location = new Point(218, 2);
            _btnClearHotkey.Size = new Size(64, 28);
            _btnClearHotkey.Click += delegate
            {
                if (_working == null) return;
                StopHotkeyRecording();
                _working.ModLCtrl = false;
                _working.ModRCtrl = false;
                _working.ModLAlt = false;
                _working.ModRAlt = false;
                _working.ModLShift = false;
                _working.ModRShift = false;
                _working.ModLWin = false;
                _working.ModRWin = false;
                _working.SyncLegacyModifiers();
                _working.VirtualKey = 0;
                SyncHotkeyControlsFromWorking(true, null);
                PushWorkingTileLive();
            };
            _hotkeyPanel.Controls.Add(_btnClearHotkey);

            Label lblType = new Label();
            lblType.Text = "键入:";
            lblType.AutoSize = true;
            lblType.Location = new Point(0, 42);
            _hotkeyPanel.Controls.Add(lblType);

            _txtHotkeyInput = new TextBox();
            _txtHotkeyInput.Location = new Point(52, 38);
            _txtHotkeyInput.Size = new Size(240, 24);
            _txtHotkeyInput.TextChanged += OnHotkeyTextChanged;
            _txtHotkeyInput.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ApplyHotkeyTextExplicitly();
                }
            };
            _hotkeyPanel.Controls.Add(_txtHotkeyInput);

            _btnApplyHotkeyText = new Button();
            _btnApplyHotkeyText.Text = "应用";
            _btnApplyHotkeyText.Location = new Point(300, 37);
            _btnApplyHotkeyText.Size = new Size(64, 26);
            _btnApplyHotkeyText.Click += delegate { ApplyHotkeyTextExplicitly(); };
            _hotkeyPanel.Controls.Add(_btnApplyHotkeyText);

            Label lblLeftMods = new Label();
            lblLeftMods.Text = "左修饰:";
            lblLeftMods.AutoSize = true;
            lblLeftMods.Location = new Point(0, 78);
            _hotkeyPanel.Controls.Add(lblLeftMods);

            _chkLCtrl = CreateModCheckbox("LCtrl", 64, 76);
            _chkLAlt = CreateModCheckbox("LAlt", 138, 76);
            _chkLShift = CreateModCheckbox("LShift", 206, 76);
            _chkLWin = CreateModCheckbox("LWin", 282, 76);

            Label lblRightMods = new Label();
            lblRightMods.Text = "右修饰:";
            lblRightMods.AutoSize = true;
            lblRightMods.Location = new Point(0, 108);
            _hotkeyPanel.Controls.Add(lblRightMods);

            _chkRCtrl = CreateModCheckbox("RCtrl", 64, 106);
            _chkRAlt = CreateModCheckbox("RAlt", 138, 106);
            _chkRShift = CreateModCheckbox("RShift", 206, 106);
            _chkRWin = CreateModCheckbox("RWin", 282, 106);

            Label lblKey = new Label();
            lblKey.Text = "主按键:";
            lblKey.AutoSize = true;
            lblKey.Location = new Point(0, 144);
            _hotkeyPanel.Controls.Add(lblKey);

            _cmbVirtualKey = new ComboBox();
            _cmbVirtualKey.Location = new Point(64, 141);
            _cmbVirtualKey.Size = new Size(180, 24);
            _cmbVirtualKey.DropDownStyle = ComboBoxStyle.DropDownList;
            PopulateSupportedKeys();
            _cmbVirtualKey.SelectedIndexChanged += OnManualHotkeyChanged;
            _hotkeyPanel.Controls.Add(_cmbVirtualKey);

            _lblCurrentHotkey = new Label();
            _lblCurrentHotkey.AutoSize = true;
            _lblCurrentHotkey.Location = new Point(256, 144);
            _lblCurrentHotkey.Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold);
            _hotkeyPanel.Controls.Add(_lblCurrentHotkey);
        }

        private CheckBox CreateModCheckbox(string text, int x, int y)
        {
            CheckBox chk = new CheckBox();
            chk.Text = text;
            chk.AutoSize = true;
            chk.Location = new Point(x, y);
            chk.CheckedChanged += OnManualHotkeyChanged;
            _hotkeyPanel.Controls.Add(chk);
            return chk;
        }

        private void BuildAnchorPanel()
        {
            _anchorPanel = new Panel();
            _anchorPanel.Location = new Point(12, 48);
            _anchorPanel.Size = new Size(482, 178);
            _anchorPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grpAction.Controls.Add(_anchorPanel);

            Label lblAx = new Label();
            lblAx.Text = "X:";
            lblAx.AutoSize = true;
            lblAx.Location = new Point(0, 10);
            _anchorPanel.Controls.Add(lblAx);

            _numAnchorX = new NumericUpDown();
            _numAnchorX.Location = new Point(28, 7);
            _numAnchorX.Size = new Size(88, 24);
            _numAnchorX.Minimum = -32768;
            _numAnchorX.Maximum = 32768;
            _numAnchorX.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.AnchorX = (int)_numAnchorX.Value;
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _anchorPanel.Controls.Add(_numAnchorX);

            Label lblAy = new Label();
            lblAy.Text = "Y:";
            lblAy.AutoSize = true;
            lblAy.Location = new Point(128, 10);
            _anchorPanel.Controls.Add(lblAy);

            _numAnchorY = new NumericUpDown();
            _numAnchorY.Location = new Point(150, 7);
            _numAnchorY.Size = new Size(88, 24);
            _numAnchorY.Minimum = -32768;
            _numAnchorY.Maximum = 32768;
            _numAnchorY.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.AnchorY = (int)_numAnchorY.Value;
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _anchorPanel.Controls.Add(_numAnchorY);

            _btnPickScreen = new Button();
            _btnPickScreen.Text = "拾取位置";
            _btnPickScreen.Location = new Point(252, 5);
            _btnPickScreen.Size = new Size(96, 28);
            _btnPickScreen.Click += OnPickScreenClick;
            _anchorPanel.Controls.Add(_btnPickScreen);

            Label lblClick = new Label();
            lblClick.Text = "点击类型:";
            lblClick.AutoSize = true;
            lblClick.Location = new Point(0, 52);
            _anchorPanel.Controls.Add(lblClick);

            _cmbClickType = new ComboBox();
            _cmbClickType.Location = new Point(74, 48);
            _cmbClickType.Size = new Size(132, 24);
            _cmbClickType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbClickType.Items.Add(new KeyItem((int)MouseClickType.LeftClick, "左键单击"));
            _cmbClickType.Items.Add(new KeyItem((int)MouseClickType.DoubleClick, "左键双击"));
            _cmbClickType.Items.Add(new KeyItem((int)MouseClickType.RightClick, "右键单击"));
            _cmbClickType.SelectedIndexChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                KeyItem item = _cmbClickType.SelectedItem as KeyItem;
                if (item != null)
                {
                    _working.ClickType = (MouseClickType)item.VirtualKey;
                    _previewPanel.Invalidate();
                    PushWorkingTileLive();
                }
            };
            _anchorPanel.Controls.Add(_cmbClickType);

            _chkRestoreCursor = new CheckBox();
            _chkRestoreCursor.Text = "光标归位";
            _chkRestoreCursor.AutoSize = true;
            _chkRestoreCursor.Location = new Point(220, 50);
            _chkRestoreCursor.CheckedChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.RestoreCursor = _chkRestoreCursor.Checked;
                PushWorkingTileLive();
            };
            _anchorPanel.Controls.Add(_chkRestoreCursor);

            Label lblHold = new Label();
            lblHold.Text = "点击时长:";
            lblHold.AutoSize = true;
            lblHold.Location = new Point(0, 96);
            _anchorPanel.Controls.Add(lblHold);

            _numClickHoldMs = new NumericUpDown();
            _numClickHoldMs.Location = new Point(74, 93);
            _numClickHoldMs.Size = new Size(82, 24);
            _numClickHoldMs.Minimum = TileConfig.MinClickHoldMs;
            _numClickHoldMs.Maximum = TileConfig.MaxClickHoldMs;
            _numClickHoldMs.Increment = 50;
            _numClickHoldMs.ValueChanged += delegate
            {
                if (_suppressUiEvents || _working == null) return;
                _working.ClickHoldMs = TileConfig.ClampClickHoldMs((int)_numClickHoldMs.Value);
                _previewPanel.Invalidate();
                PushWorkingTileLive();
            };
            _anchorPanel.Controls.Add(_numClickHoldMs);

            Label lblHoldUnit = new Label();
            lblHoldUnit.Text = "ms";
            lblHoldUnit.AutoSize = true;
            lblHoldUnit.Location = new Point(160, 96);
            _anchorPanel.Controls.Add(lblHoldUnit);

            string[] holdLabels = new string[] { "0ms", "30ms", "200ms", "1s", "2s", "5s" };
            int[] holdValues = new int[] { 0, 30, 200, 1000, 2000, 5000 };
            int[] holdWidths = new int[] { 42, 46, 52, 38, 38, 38 };
            int hx = 188;
            for (int i = 0; i < holdLabels.Length; i++)
            {
                int holdVal = holdValues[i];
                int bw = holdWidths[i];
                Button btnHold = new Button();
                btnHold.Text = holdLabels[i];
                btnHold.Location = new Point(hx, 92);
                btnHold.Size = new Size(bw, 26);
                btnHold.Click += delegate
                {
                    if (_working == null) return;
                    _numClickHoldMs.Value = ClampDecimal(_numClickHoldMs, holdVal);
                };
                _anchorPanel.Controls.Add(btnHold);
                hx += bw + 4;
            }
        }

        private void PopulateSupportedKeys()
        {
            _cmbVirtualKey.Items.Clear();
            List<KeyValuePair<int, string>> keys = NativeMethods.GetSupportedKeys();
            bool hasZero = false;
            if (keys != null)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].Key == 0)
                    {
                        hasZero = true;
                    }
                    _cmbVirtualKey.Items.Add(new KeyItem(keys[i].Key, keys[i].Value));
                }
            }
            if (!hasZero)
            {
                _cmbVirtualKey.Items.Insert(0, new KeyItem(0, "(无)"));
            }
        }

        private void LoadSelectedTileIntoEditor()
        {
            string id = GetSelectedListTileId();
            _selectedTileId = id;
            TileConfig source = FindTileInActiveProfile(id);

            if (source == null)
            {
                _working = null;
                _editorHost.Enabled = false;
                _previewPanel.Invalidate();
                return;
            }

            _editorHost.Enabled = true;

            _working = source.Clone();
            _working.NormalizeModifiers();
            _working.SyncLegacyModifiers();
            _working.Size = TileConfig.ClampSize(_working.Size);
            _working.Opacity = TileConfig.ClampOpacity(_working.Opacity);
            _working.ClickHoldMs = TileConfig.ClampClickHoldMs(_working.ClickHoldMs);

            bool prevSuppress = _suppressUiEvents;
            _suppressUiEvents = true;
            try
            {
                _txtLabel.Text = _working.Label ?? string.Empty;
                _trkSize.Value = _working.Size;
                _numSize.Value = _working.Size;
                _numPosX.Value = ClampDecimal(_numPosX, _working.X);
                _numPosY.Value = ClampDecimal(_numPosY, _working.Y);

                int opPct = (int)Math.Round(_working.Opacity * 100.0);
                if (opPct < 25) opPct = 25;
                if (opPct > 100) opPct = 100;
                _trkOpacity.Value = opPct;
                _lblOpacityVal.Text = string.Format("{0}%", opPct);

                _rbHotkey.Checked = (_working.Action == ActionType.Hotkey);
                _rbAnchor.Checked = (_working.Action == ActionType.AnchorClick);
                UpdateModePanels();

                SyncHotkeyControlsFromWorking(true, null);

                _numAnchorX.Value = ClampDecimal(_numAnchorX, _working.AnchorX);
                _numAnchorY.Value = ClampDecimal(_numAnchorY, _working.AnchorY);

                int clickIdx = 0;
                for (int i = 0; i < _cmbClickType.Items.Count; i++)
                {
                    KeyItem item = _cmbClickType.Items[i] as KeyItem;
                    if (item != null && item.VirtualKey == (int)_working.ClickType)
                    {
                        clickIdx = i;
                        break;
                    }
                }
                _cmbClickType.SelectedIndex = clickIdx;
                _chkRestoreCursor.Checked = _working.RestoreCursor;
                _numClickHoldMs.Value = ClampDecimal(_numClickHoldMs, _working.ClickHoldMs);

                _previewPanel.Invalidate();
            }
            finally
            {
                _suppressUiEvents = prevSuppress;
            }
        }

        private void UpdateDeleteTileButtonState()
        {
            if (_btnDeleteTile == null)
            {
                return;
            }
            int count = (_currentConfig != null) ? _currentConfig.GetActiveTiles().Count : 0;
            _btnDeleteTile.Enabled = (count > 1 && !string.IsNullOrEmpty(_selectedTileId));
        }

        private string FormatWorkingHotkey()
        {
            if (_working == null)
            {
                return "未设置";
            }
            return NativeMethods.FormatHotkey(
                _working.ModLCtrl, _working.ModRCtrl,
                _working.ModLAlt, _working.ModRAlt,
                _working.ModLShift, _working.ModRShift,
                _working.ModLWin, _working.ModRWin,
                _working.VirtualKey);
        }

        private void SyncHotkeyControlsFromWorking(bool updateTextInput, string overrideLabel)
        {
            if (_working == null)
            {
                return;
            }

            _updatingHotkeyUi = true;
            try
            {
                _chkLCtrl.Checked = _working.ModLCtrl;
                _chkRCtrl.Checked = _working.ModRCtrl;
                _chkLAlt.Checked = _working.ModLAlt;
                _chkRAlt.Checked = _working.ModRAlt;
                _chkLShift.Checked = _working.ModLShift;
                _chkRShift.Checked = _working.ModRShift;
                _chkLWin.Checked = _working.ModLWin;
                _chkRWin.Checked = _working.ModRWin;

                int matchedIdx = 0;
                for (int i = 0; i < _cmbVirtualKey.Items.Count; i++)
                {
                    KeyItem item = _cmbVirtualKey.Items[i] as KeyItem;
                    if (item != null && item.VirtualKey == _working.VirtualKey)
                    {
                        matchedIdx = i;
                        break;
                    }
                }
                if (matchedIdx == 0 && _working.VirtualKey != 0)
                {
                    KeyItem custom = new KeyItem(_working.VirtualKey, string.Format("VK_{0:X2}", _working.VirtualKey));
                    _cmbVirtualKey.Items.Add(custom);
                    matchedIdx = _cmbVirtualKey.Items.Count - 1;
                }
                _cmbVirtualKey.SelectedIndex = matchedIdx;

                string formatted = FormatWorkingHotkey();
                bool hasAny = _working.HasAnyHotkey();
                if (updateTextInput)
                {
                    _txtHotkeyInput.Text = hasAny ? formatted : string.Empty;
                }

                if (!string.IsNullOrEmpty(overrideLabel))
                {
                    _lblCurrentHotkey.Text = overrideLabel;
                    _lblCurrentHotkey.ForeColor = Color.FromArgb(180, 70, 40);
                }
                else
                {
                    _lblCurrentHotkey.Text = hasAny ? formatted : "未设置";
                    _lblCurrentHotkey.ForeColor = SystemColors.ControlText;
                }
                _previewPanel.Invalidate();
            }
            finally
            {
                _updatingHotkeyUi = false;
            }
        }

        private void StartHotkeyRecording()
        {
            StopHotkeyRecording();

            _recLCtrl = false;
            _recRCtrl = false;
            _recLAlt = false;
            _recRAlt = false;
            _recLShift = false;
            _recRShift = false;
            _recLWin = false;
            _recRWin = false;
            _swallowNextUpVk = 0;

            _isRecordingHotkey = true;
            IntPtr hMod = NativeMethods.GetModuleHandle(null);
            _llHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _llHookCallback, hMod, 0);

            _btnRecordHotkey.UseVisualStyleBackColor = false;
            _btnRecordHotkey.BackColor = Color.FromArgb(37, 99, 235);
            _btnRecordHotkey.ForeColor = Color.White;
            _btnRecordHotkey.Text = "按下快捷键...";
            _btnRecordHotkey.Focus();
        }

        private void StopHotkeyRecording()
        {
            _isRecordingHotkey = false;
            UninstallKeyboardHook();

            if (_btnRecordHotkey != null && !_btnRecordHotkey.IsDisposed)
            {
                _btnRecordHotkey.BackColor = SystemColors.Control;
                _btnRecordHotkey.ForeColor = SystemColors.ControlText;
                _btnRecordHotkey.UseVisualStyleBackColor = true;
                _btnRecordHotkey.Text = "录制快捷键";
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_llHookHandle != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.UnhookWindowsHookEx(_llHookHandle);
                }
                catch
                {
                }
                _llHookHandle = IntPtr.Zero;
            }
        }

        private void OnHotkeyTextChanged(object sender, EventArgs e)
        {
            if (_suppressUiEvents || _updatingHotkeyUi || _working == null)
            {
                return;
            }

            string raw = _txtHotkeyInput.Text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            bool lCtrl;
            bool rCtrl;
            bool lAlt;
            bool rAlt;
            bool lShift;
            bool rShift;
            bool lWin;
            bool rWin;
            int vk;
            if (NativeMethods.TryParseHotkey(raw, out lCtrl, out rCtrl, out lAlt, out rAlt, out lShift, out rShift, out lWin, out rWin, out vk))
            {
                _working.ModLCtrl = lCtrl;
                _working.ModRCtrl = rCtrl;
                _working.ModLAlt = lAlt;
                _working.ModRAlt = rAlt;
                _working.ModLShift = lShift;
                _working.ModRShift = rShift;
                _working.ModLWin = lWin;
                _working.ModRWin = rWin;
                _working.SyncLegacyModifiers();
                _working.VirtualKey = vk;

                SyncHotkeyControlsFromWorking(false, null);
                PushWorkingTileLive();
            }
            else
            {
                _lblCurrentHotkey.Text = "格式无效";
                _lblCurrentHotkey.ForeColor = Color.FromArgb(180, 70, 40);
            }
        }

        private void ApplyHotkeyTextExplicitly()
        {
            if (_working == null)
            {
                return;
            }

            string raw = _txtHotkeyInput.Text;
            bool lCtrl;
            bool rCtrl;
            bool lAlt;
            bool rAlt;
            bool lShift;
            bool rShift;
            bool lWin;
            bool rWin;
            int vk;
            if (NativeMethods.TryParseHotkey(raw, out lCtrl, out rCtrl, out lAlt, out rAlt, out lShift, out rShift, out lWin, out rWin, out vk))
            {
                _working.ModLCtrl = lCtrl;
                _working.ModRCtrl = rCtrl;
                _working.ModLAlt = lAlt;
                _working.ModRAlt = rAlt;
                _working.ModLShift = lShift;
                _working.ModRShift = rShift;
                _working.ModLWin = lWin;
                _working.ModRWin = rWin;
                _working.SyncLegacyModifiers();
                _working.VirtualKey = vk;

                SyncHotkeyControlsFromWorking(true, null);
                PushWorkingTileLive();
            }
            else
            {
                _lblCurrentHotkey.Text = "格式无效";
                _lblCurrentHotkey.ForeColor = Color.FromArgb(180, 70, 40);
            }
        }

        private void OnManualHotkeyChanged(object sender, EventArgs e)
        {
            if (_suppressUiEvents || _updatingHotkeyUi || _working == null)
            {
                return;
            }

            _working.ModLCtrl = _chkLCtrl.Checked;
            _working.ModRCtrl = _chkRCtrl.Checked;
            _working.ModLAlt = _chkLAlt.Checked;
            _working.ModRAlt = _chkRAlt.Checked;
            _working.ModLShift = _chkLShift.Checked;
            _working.ModRShift = _chkRShift.Checked;
            _working.ModLWin = _chkLWin.Checked;
            _working.ModRWin = _chkRWin.Checked;
            _working.SyncLegacyModifiers();

            KeyItem selected = _cmbVirtualKey.SelectedItem as KeyItem;
            _working.VirtualKey = (selected != null) ? selected.VirtualKey : 0;

            SyncHotkeyControlsFromWorking(true, null);
            PushWorkingTileLive();
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            if (_suppressUiEvents || _working == null)
            {
                return;
            }
            StopHotkeyRecording();
            _working.Action = _rbAnchor.Checked ? ActionType.AnchorClick : ActionType.Hotkey;
            UpdateModePanels();
            _previewPanel.Invalidate();
            PushWorkingTileLive();
        }

        private void UpdateModePanels()
        {
            bool isHotkey = !_rbAnchor.Checked;
            _hotkeyPanel.Visible = isHotkey;
            _anchorPanel.Visible = !isHotkey;
        }

        private void OnRenameProfileClick(object sender, EventArgs e)
        {
            ProfileItem item = _cmbProfile.SelectedItem as ProfileItem;
            if (item == null)
            {
                return;
            }

            StopHotkeyRecording();
            _manager.BeginModalSuppression();
            try
            {
                string renamed = PromptProfileName(item.Name);
                if (!string.IsNullOrWhiteSpace(renamed))
                {
                    _manager.RenameProfile(item.Id, renamed);
                }
            }
            finally
            {
                _manager.EndModalSuppression();
            }
        }

        private string PromptProfileName(string initialName)
        {
            using (Form dlg = new Form())
            {
                dlg.SuspendLayout();
                dlg.AutoScaleDimensions = new SizeF(96F, 96F);
                dlg.AutoScaleMode = AutoScaleMode.Dpi;
                dlg.Text = "重命名方案";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(300, 108);
                dlg.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

                Label lbl = new Label();
                lbl.Text = "方案名称:";
                lbl.AutoSize = true;
                lbl.Location = new Point(14, 18);
                dlg.Controls.Add(lbl);

                TextBox txt = new TextBox();
                txt.Text = initialName ?? string.Empty;
                txt.Location = new Point(88, 14);
                txt.Size = new Size(196, 24);
                txt.SelectAll();
                dlg.Controls.Add(txt);

                Button btnOk = new Button();
                btnOk.Text = "确定";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(124, 62);
                btnOk.Size = new Size(76, 28);
                dlg.Controls.Add(btnOk);

                Button btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(208, 62);
                btnCancel.Size = new Size(76, 28);
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                dlg.ResumeLayout(false);
                dlg.PerformLayout();

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    return txt.Text.Trim();
                }
            }
            return null;
        }

        private void OnCustomColorClick(object sender, EventArgs e)
        {
            if (_working == null)
            {
                return;
            }

            _manager.BeginModalSuppression();
            try
            {
                using (ColorDialog dlg = new ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.Color = TilePalettes.FromHex(_working.BackColorHex, Color.FromArgb(30, 34, 44));
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        Color chosen = dlg.Color;
                        _working.BackColorHex = TilePalettes.ToHex(chosen);
                        double luminance = (0.299 * chosen.R + 0.587 * chosen.G + 0.114 * chosen.B) / 255.0;
                        _working.TextColorHex = (luminance > 0.62) ? "#181C24" : "#F2F6FC";
                        _previewPanel.Invalidate();
                        PushWorkingTileLive();
                    }
                }
            }
            finally
            {
                _manager.EndModalSuppression();
            }
        }

        private void OnPickScreenClick(object sender, EventArgs e)
        {
            if (_working == null)
            {
                return;
            }

            double prevOpacity = this.Opacity;
            _manager.BeginModalSuppression();
            this.Opacity = 0.0;
            try
            {
                using (ScreenPickerForm picker = new ScreenPickerForm())
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        _suppressUiEvents = true;
                        try
                        {
                            _numAnchorX.Value = ClampDecimal(_numAnchorX, picker.PickedX);
                            _numAnchorY.Value = ClampDecimal(_numAnchorY, picker.PickedY);
                            _working.AnchorX = (int)_numAnchorX.Value;
                            _working.AnchorY = (int)_numAnchorY.Value;
                        }
                        finally
                        {
                            _suppressUiEvents = false;
                        }
                        _previewPanel.Invalidate();
                        PushWorkingTileLive();
                    }
                }
            }
            finally
            {
                this.Opacity = prevOpacity;
                _manager.EndModalSuppression();
                this.Activate();
            }
        }

        private void PushWorkingTileLive()
        {
            if (_working == null)
            {
                return;
            }

            TileConfig snapshot = _working.Clone();
            _manager.UpdateTileLive(snapshot);
            UpdateSelectedListViewRow(snapshot);
        }

        private void UpdateSelectedListViewRow(TileConfig tile)
        {
            if (tile == null)
            {
                return;
            }

            for (int i = 0; i < _listTiles.Items.Count; i++)
            {
                ListViewItem item = _listTiles.Items[i];
                string id = item.Tag as string;
                if (string.Equals(id, tile.Id, StringComparison.Ordinal))
                {
                    string lbl = string.IsNullOrEmpty(tile.Label) ? "宏" : tile.Label;
                    string sub = tile.GetSubtitle();
                    string sz = string.Format("{0}px", TileConfig.ClampSize(tile.Size));

                    if (!string.Equals(item.Text, lbl, StringComparison.Ordinal))
                    {
                        item.Text = lbl;
                    }
                    if (item.SubItems.Count > 1 && !string.Equals(item.SubItems[1].Text, sub, StringComparison.Ordinal))
                    {
                        item.SubItems[1].Text = sub;
                    }
                    if (item.SubItems.Count > 2 && !string.Equals(item.SubItems[2].Text, sz, StringComparison.Ordinal))
                    {
                        item.SubItems[2].Text = sz;
                    }
                    break;
                }
            }
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int pw = _previewPanel.ClientSize.Width;
            int ph = _previewPanel.ClientSize.Height;

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 26, 32)))
            {
                g.FillRectangle(bg, 0, 0, pw, ph);
            }

            if (_working == null)
            {
                return;
            }

            int cardSize = Math.Min(pw - 16, Math.Max(28, Math.Min(110, _working.Size)));
            int cx = (pw - cardSize) / 2;
            int cy = (ph - cardSize) / 2;

            Color back = TilePalettes.FromHex(_working.BackColorHex, Color.FromArgb(30, 34, 44));
            Color accent = TilePalettes.FromHex(_working.AccentColorHex, Color.FromArgb(88, 166, 255));
            Color text = TilePalettes.FromHex(_working.TextColorHex, Color.FromArgb(242, 246, 252));

            int alpha = (int)Math.Round(TileConfig.ClampOpacity(_working.Opacity) * 255.0);
            using (SolidBrush cardBrush = new SolidBrush(Color.FromArgb(alpha, back)))
            {
                g.FillRectangle(cardBrush, cx, cy, cardSize, cardSize);
            }
            using (Pen borderPen = new Pen(Color.FromArgb(145, accent), 1f))
            {
                g.DrawRectangle(borderPen, cx, cy, cardSize - 1, cardSize - 1);
            }
            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                int barM = Math.Max(4, cardSize / 6);
                g.FillRectangle(accentBrush, cx + barM, cy + 6, Math.Max(6, cardSize - barM * 2), 3);
            }

            string label = string.IsNullOrEmpty(_working.Label) ? "宏" : _working.Label;
            string sub = _working.GetSubtitle();

            float labelPx = Math.Max(10f, cardSize * 0.22f);
            float subPx = Math.Max(9f, cardSize * 0.145f);
            using (Font fLabel = new Font("Microsoft YaHei UI", labelPx, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fSub = new Font("Microsoft YaHei UI", subPx, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush tb = new SolidBrush(text))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(175, text)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;

                g.DrawString(label, fLabel, tb, new RectangleF(cx + 3, cy + 12, cardSize - 6, cardSize * 0.44f), sf);
                if (cardSize >= 44)
                {
                    g.DrawString(sub, fSub, sb, new RectangleF(cx + 3, cy + cardSize * 0.56f, cardSize - 6, cardSize * 0.34f), sf);
                }
                sf.Dispose();
            }
        }

        private TileConfig FindTileInActiveProfile(string tileId)
        {
            if (_currentConfig == null || string.IsNullOrEmpty(tileId))
            {
                return null;
            }
            List<TileConfig> tiles = _currentConfig.GetActiveTiles();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (string.Equals(tiles[i].Id, tileId, StringComparison.Ordinal))
                {
                    return tiles[i];
                }
            }
            return null;
        }

        private string GetSelectedListTileId()
        {
            if (_listTiles == null || _listTiles.SelectedItems.Count == 0)
            {
                return null;
            }
            return _listTiles.SelectedItems[0].Tag as string;
        }

        private static decimal ClampDecimal(NumericUpDown ctrl, int value)
        {
            decimal d = value;
            if (d < ctrl.Minimum) return ctrl.Minimum;
            if (d > ctrl.Maximum) return ctrl.Maximum;
            return d;
        }

        private static Button CreateToolbarButton(string text, int x, int y, int w, int h)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(x, y);
            btn.Size = new Size(w, h);
            return btn;
        }

        protected override void Dispose(bool disposing)
        {
            _isRecordingHotkey = false;
            UninstallKeyboardHook();
            base.Dispose(disposing);
        }
    }
}
