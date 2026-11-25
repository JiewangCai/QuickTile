using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QuickTile
{
    public class TileForm : Form
    {
        private const int ResizeGripSize = 14;
        private const int DragThresholdPx = 5;
        private const int FlashDurationMs = 140;

        private TileConfig _config;
        private bool _lockPosition;
        private bool _lockSize;
        private bool _alwaysOnTop;
        private bool _handlingDpiChanged;

        private bool _isHovered;
        private bool _isFlashing;
        private bool _canDrag;
        private bool _isResizing;
        private bool _didDragOrResize;
        private int _dragStartCursorX;
        private int _dragStartCursorY;
        private int _dragStartX;
        private int _dragStartY;
        private int _dragStartSize;

        private readonly Timer _flashTimer;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _miLockSize;
        private readonly ToolStripMenuItem _miLockPosition;

        public event EventHandler TileChanged;
        public event EventHandler TileGeometryLiveChanged;
        public event EventHandler OpenSettingsRequested;
        public event EventHandler ToggleLockSizeRequested;
        public event EventHandler ToggleLockPositionRequested;
        public event EventHandler ExitRequested;
        public event EventHandler ContextMenuOpened;
        public event EventHandler ContextMenuClosed;

        public TileForm(TileConfig config, bool lockPosition, bool lockSize, bool alwaysOnTop)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;
            _lockPosition = lockPosition;
            _lockSize = lockSize;
            _alwaysOnTop = alwaysOnTop;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            CausesValidation = false;
            TabStop = false;
            MinimumSize = new Size(28, 28);
            MaximumSize = new Size(320, 320);

            _flashTimer = new Timer();
            _flashTimer.Interval = FlashDurationMs;
            _flashTimer.Tick += OnFlashTimerTick;

            _contextMenu = new ContextMenuStrip();

            ToolStripMenuItem miSettings = new ToolStripMenuItem("设置");
            miSettings.Font = new Font(miSettings.Font, FontStyle.Bold);
            miSettings.Click += delegate { RaiseEvent(OpenSettingsRequested); };

            _miLockSize = new ToolStripMenuItem("锁定大小");
            _miLockSize.Click += delegate { RaiseEvent(ToggleLockSizeRequested); };

            _miLockPosition = new ToolStripMenuItem("锁定位置");
            _miLockPosition.Click += delegate { RaiseEvent(ToggleLockPositionRequested); };

            ToolStripMenuItem miExit = new ToolStripMenuItem("退出程序");
            miExit.Click += delegate { RaiseEvent(ExitRequested); };

            _contextMenu.Items.Add(miSettings);
            _contextMenu.Items.Add(_miLockSize);
            _contextMenu.Items.Add(_miLockPosition);
            _contextMenu.Items.Add(miExit);

            _contextMenu.Opened += delegate
            {
                if (_contextMenu.IsHandleCreated)
                {
                    NativeMethods.SetTopMostNoActivate(_contextMenu.Handle, true);
                    NativeMethods.SetForegroundWindow(_contextMenu.Handle);
                }
                RaiseEvent(ContextMenuOpened);
            };
            _contextMenu.Closed += delegate
            {
                RaiseEvent(ContextMenuClosed);
            };

            ApplyState(_config, _lockPosition, _lockSize, _alwaysOnTop);
        }

        public TileConfig Config
        {
            get { return _config; }
        }

        public string TileId
        {
            get { return _config.Id; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
                if (_alwaysOnTop)
                {
                    cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
                }
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)NativeMethods.MA_NOACTIVATE;
                return;
            }
            if (m.Msg == NativeMethods.WM_DPICHANGED)
            {
                if (!_handlingDpiChanged && IsHandleCreated)
                {
                    _handlingDpiChanged = true;
                    try
                    {
                        int sz = TileConfig.ClampSize(_config.Size);
                        NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, sz, sz);
                    }
                    finally
                    {
                        _handlingDpiChanged = false;
                    }
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int sz = TileConfig.ClampSize(_config.Size);
            NativeMethods.TryApplyRoundedCorners(this.Handle, false);
            NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, sz, sz);
            NativeMethods.SetTopMostNoActivate(this.Handle, _alwaysOnTop);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            int sz = TileConfig.ClampSize(_config.Size);
            NativeMethods.TryApplyRoundedCorners(this.Handle, false);
            NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, sz, sz);
            NativeMethods.SetTopMostNoActivate(this.Handle, _alwaysOnTop);
        }

        public void ApplyState(TileConfig config, bool lockPosition, bool lockSize, bool alwaysOnTop)
        {
            if (config == null)
            {
                return;
            }

            _config = config;
            _lockPosition = lockPosition;
            _lockSize = lockSize;
            bool topChanged = (_alwaysOnTop != alwaysOnTop);
            _alwaysOnTop = alwaysOnTop;

            int size = TileConfig.ClampSize(_config.Size);
            _config.Size = size;
            double opacity = TileConfig.ClampOpacity(_config.Opacity);
            _config.Opacity = opacity;

            Text = string.IsNullOrEmpty(_config.Label) ? "QuickTile" : _config.Label;
            Opacity = opacity;

            if (IsHandleCreated)
            {
                NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, size, size);
                if (topChanged)
                {
                    NativeMethods.SetTopMostNoActivate(this.Handle, _alwaysOnTop);
                }
            }
            else
            {
                Bounds = new Rectangle(_config.X, _config.Y, size, size);
            }

            UpdateCursorForPoint(PointToClient(GetPhysicalCursorPoint()));
            Invalidate();
        }

        public void SetModeFlags(bool lockPosition, bool lockSize, bool alwaysOnTop)
        {
            bool changed = (_lockPosition != lockPosition) || (_lockSize != lockSize) || (_alwaysOnTop != alwaysOnTop);
            _lockPosition = lockPosition;
            _lockSize = lockSize;
            if (IsHandleCreated && _alwaysOnTop != alwaysOnTop)
            {
                _alwaysOnTop = alwaysOnTop;
                NativeMethods.SetTopMostNoActivate(this.Handle, _alwaysOnTop);
            }
            else
            {
                _alwaysOnTop = alwaysOnTop;
            }

            if (changed)
            {
                UpdateCursorForPoint(PointToClient(GetPhysicalCursorPoint()));
                Invalidate();
            }
        }

        public void TriggerFlash()
        {
            _isFlashing = true;
            _flashTimer.Stop();
            _flashTimer.Start();
            Invalidate();
        }

        private void OnFlashTimerTick(object sender, EventArgs e)
        {
            _flashTimer.Stop();
            _isFlashing = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            UpdateCursorForPoint(PointToClient(GetPhysicalCursorPoint()));
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            if (!_canDrag && !_isResizing)
            {
                Cursor = Cursors.Default;
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            Point pt = GetPhysicalCursorPoint();
            _dragStartCursorX = pt.X;
            _dragStartCursorY = pt.Y;
            _dragStartX = _config.X;
            _dragStartY = _config.Y;
            _dragStartSize = TileConfig.ClampSize(_config.Size);
            _didDragOrResize = false;

            if (!_lockSize && IsInResizeGrip(e.Location))
            {
                _isResizing = true;
                _canDrag = false;
                this.Capture = true;
                Cursor = Cursors.SizeNWSE;
                Invalidate();
            }
            else if (!_lockPosition)
            {
                _canDrag = true;
                _isResizing = false;
                this.Capture = true;
            }
            else
            {
                _canDrag = false;
                _isResizing = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point cur = GetPhysicalCursorPoint();

            if (_isResizing)
            {
                int dx = cur.X - _dragStartCursorX;
                int dy = cur.Y - _dragStartCursorY;
                int delta = Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
                int newSize = TileConfig.ClampSize(_dragStartSize + delta);

                if (newSize != _config.Size)
                {
                    _config.Size = newSize;
                    _didDragOrResize = true;
                    NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, newSize, newSize);
                    Invalidate();
                    RaiseEvent(TileGeometryLiveChanged);
                }
                return;
            }

            if (_canDrag)
            {
                int dx = cur.X - _dragStartCursorX;
                int dy = cur.Y - _dragStartCursorY;

                if (_didDragOrResize || Math.Abs(dx) >= DragThresholdPx || Math.Abs(dy) >= DragThresholdPx)
                {
                    _didDragOrResize = true;
                    Cursor = Cursors.SizeAll;
                    int newX = _dragStartX + dx;
                    int newY = _dragStartY + dy;
                    if (newX != _config.X || newY != _config.Y)
                    {
                        _config.X = newX;
                        _config.Y = newY;
                        NativeMethods.SetWindowBoundsNoActivate(this.Handle, newX, newY, _config.Size, _config.Size);
                        RaiseEvent(TileGeometryLiveChanged);
                    }
                }
                return;
            }

            UpdateCursorForPoint(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right)
            {
                _canDrag = false;
                _isResizing = false;
                _didDragOrResize = false;
                if (this.Capture)
                {
                    this.Capture = false;
                }

                _miLockSize.Checked = _lockSize;
                _miLockPosition.Checked = _lockPosition;

                Point pt = GetPhysicalCursorPoint();
                Screen scr = Screen.FromPoint(pt);
                Rectangle wa = (scr != null) ? scr.WorkingArea : new Rectangle(0, 0, 1920, 1080);
                Size pref = _contextMenu.GetPreferredSize(Size.Empty);
                Size menuSize = new Size(Math.Max(pref.Width, 140), Math.Max(pref.Height, 100));
                Point menuPos = NativeMethods.ComputeMenuScreenPosition(pt, menuSize, wa);

                NativeMethods.SetForegroundWindow(this.Handle);
                _contextMenu.Show(menuPos);
                if (_contextMenu.IsHandleCreated)
                {
                    NativeMethods.SetTopMostNoActivate(_contextMenu.Handle, true);
                    NativeMethods.SetForegroundWindow(_contextMenu.Handle);
                }
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            bool wasManipulating = _didDragOrResize;

            _canDrag = false;
            _isResizing = false;
            _didDragOrResize = false;

            if (this.Capture)
            {
                this.Capture = false;
            }

            UpdateCursorForPoint(PointToClient(GetPhysicalCursorPoint()));

            if (wasManipulating)
            {
                Invalidate();
                RaiseEvent(TileChanged);
                return;
            }

            if (ClientRectangle.Contains(e.Location))
            {
                TriggerFlash();
                InputExecutor.ExecuteAsync(_config);
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!this.Capture && (_canDrag || _isResizing))
            {
                bool changed = _didDragOrResize;
                _canDrag = false;
                _isResizing = false;
                _didDragOrResize = false;
                UpdateCursorForPoint(PointToClient(GetPhysicalCursorPoint()));
                Invalidate();
                if (changed)
                {
                    RaiseEvent(TileChanged);
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_lockSize)
            {
                return;
            }

            int step = e.Delta > 0 ? 4 : -4;
            int newSize = TileConfig.ClampSize(_config.Size + step);
            if (newSize != _config.Size)
            {
                _config.Size = newSize;
                NativeMethods.SetWindowBoundsNoActivate(this.Handle, _config.X, _config.Y, newSize, newSize);
                Invalidate();
                RaiseEvent(TileGeometryLiveChanged);
                RaiseEvent(TileChanged);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            Color baseBack = TilePalettes.FromHex(_config.BackColorHex, Color.FromArgb(30, 34, 44));
            Color accent = TilePalettes.FromHex(_config.AccentColorHex, Color.FromArgb(88, 166, 255));
            Color text = TilePalettes.FromHex(_config.TextColorHex, Color.FromArgb(242, 246, 252));

            Color fillColor = baseBack;
            if (_isFlashing)
            {
                fillColor = BlendColors(baseBack, accent, 0.36f);
            }
            else if (_isHovered || _isResizing)
            {
                fillColor = BlendColors(baseBack, Color.White, 0.10f);
            }

            using (SolidBrush backBrush = new SolidBrush(fillColor))
            {
                g.FillRectangle(backBrush, 0, 0, w, h);
            }

            Color borderColor = _isFlashing
                ? accent
                : (_isHovered || _isResizing ? Color.FromArgb(165, accent) : Color.FromArgb(54, 255, 255, 255));

            using (Pen borderPen = new Pen(borderColor, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            int barMargin = Math.Max(5, w / 6);
            int barWidth = Math.Max(6, w - barMargin * 2);
            int barHeight = w < 44 ? 2 : 3;
            int barY = w < 44 ? 3 : 6;
            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                g.FillRectangle(accentBrush, barMargin, barY, barWidth, barHeight);
            }

            if (_config.Action == ActionType.AnchorClick && w >= 40)
            {
                int dotSize = 5;
                using (Pen dotPen = new Pen(accent, 1.4f))
                {
                    g.DrawEllipse(dotPen, w - 10, 5, dotSize, dotSize);
                }
            }

            string label = string.IsNullOrEmpty(_config.Label) ? "宏" : _config.Label;
            string subtitle = _config.GetSubtitle();
            bool showSubtitle = (w >= 44 && !string.IsNullOrEmpty(subtitle));

            float labelPx = Math.Max(9f, Math.Min(26f, w * 0.22f));
            float subPx = Math.Max(8f, Math.Min(15f, w * 0.145f));

            using (Font labelFont = CreateFittedFont(g, label, "Microsoft YaHei UI", FontStyle.Bold, labelPx, w - 6))
            using (SolidBrush textBrush = new SolidBrush(text))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;

                if (showSubtitle)
                {
                    int topPad = barY + barHeight + 2;
                    int usableH = h - topPad - 4;
                    int labelH = (int)Math.Round(usableH * 0.56);
                    RectangleF labelRect = new RectangleF(3, topPad, w - 6, labelH);
                    RectangleF subRect = new RectangleF(3, topPad + labelH - 2, w - 6, usableH - labelH);

                    g.DrawString(label, labelFont, textBrush, labelRect, sf);

                    using (Font subFont = CreateFittedFont(g, subtitle, "Microsoft YaHei UI", FontStyle.Regular, subPx, w - 6))
                    using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(180, text)))
                    {
                        g.DrawString(subtitle, subFont, subBrush, subRect, sf);
                    }
                }
                else
                {
                    int topPad = barY + barHeight + 1;
                    RectangleF labelRect = new RectangleF(2, topPad, w - 4, h - topPad - 2);
                    g.DrawString(label, labelFont, textBrush, labelRect, sf);
                }

                sf.Dispose();
            }

            if (!_lockSize && (_isHovered || _isResizing))
            {
                using (Pen gripPen = new Pen(Color.FromArgb(215, accent), 1.4f))
                {
                    g.DrawLine(gripPen, w - 10, h - 3, w - 3, h - 10);
                    g.DrawLine(gripPen, w - 6, h - 3, w - 3, h - 6);
                }
            }
        }

        private bool IsInResizeGrip(Point p)
        {
            int grip = Math.Min(ResizeGripSize, Math.Max(8, ClientSize.Width / 3));
            return p.X >= (ClientSize.Width - grip) && p.Y >= (ClientSize.Height - grip);
        }

        private void UpdateCursorForPoint(Point p)
        {
            if (!_lockSize && IsInResizeGrip(p))
            {
                Cursor = Cursors.SizeNWSE;
            }
            else if (_didDragOrResize)
            {
                Cursor = Cursors.SizeAll;
            }
            else
            {
                Cursor = Cursors.Hand;
            }
        }

        private static Point GetPhysicalCursorPoint()
        {
            NativeMethods.POINT pt;
            if (NativeMethods.GetCursorPos(out pt))
            {
                return new Point(pt.X, pt.Y);
            }
            return Control.MousePosition;
        }

        private void RaiseEvent(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static Color BlendColors(Color c1, Color c2, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            int r = (int)Math.Round(c1.R + (c2.R - c1.R) * amount);
            int g = (int)Math.Round(c1.G + (c2.G - c1.G) * amount);
            int b = (int)Math.Round(c1.B + (c2.B - c1.B) * amount);
            return Color.FromArgb(255, ClampByte(r), ClampByte(g), ClampByte(b));
        }

        private static int ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return value;
        }

        private static Font CreateFittedFont(Graphics g, string text, string family, FontStyle style, float maxPx, int maxWidth)
        {
            float px = maxPx;
            while (px > 8f)
            {
                Font f = new Font(family, px, style, GraphicsUnit.Pixel);
                SizeF measured = g.MeasureString(text, f);
                if (measured.Width <= maxWidth || px <= 8.5f)
                {
                    return f;
                }
                f.Dispose();
                px -= 1f;
            }
            return new Font(family, 8.5f, style, GraphicsUnit.Pixel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_flashTimer != null)
                {
                    _flashTimer.Stop();
                    _flashTimer.Dispose();
                }
                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
