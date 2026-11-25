using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QuickTile
{
    public class ScreenPickerForm : Form
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private readonly List<MonitorOverlayForm> _secondaryOverlays;
        private readonly Rectangle _primaryBounds;
        private readonly PickerHudForm _hudForm;
        private readonly Timer _trackTimer;

        private int _cursorX;
        private int _cursorY;
        private bool _completed;

        public ScreenPickerForm()
        {
            _secondaryOverlays = new List<MonitorOverlayForm>();

            Screen[] screens = Screen.AllScreens;
            if (screens == null || screens.Length == 0)
            {
                _primaryBounds = new Rectangle(0, 0, 1920, 1080);
            }
            else
            {
                _primaryBounds = screens[0].Bounds;
                for (int i = 1; i < screens.Length; i++)
                {
                    MonitorOverlayForm extra = new MonitorOverlayForm(this, screens[i].Bounds);
                    _secondaryOverlays.Add(extra);
                }
            }

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(10, 14, 22);
            Opacity = 0.28;
            Cursor = Cursors.Cross;
            Bounds = _primaryBounds;

            _hudForm = new PickerHudForm();

            _trackTimer = new Timer();
            _trackTimer.Interval = 16;
            _trackTimer.Tick += OnTrackTick;

            UpdateCursorTracking();
        }

        public int PickedX { get; private set; }
        public int PickedY { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_DPICHANGED)
            {
                if (IsHandleCreated)
                {
                    NativeMethods.SetWindowBoundsTopMostNoActivate(
                        this.Handle,
                        _primaryBounds.X,
                        _primaryBounds.Y,
                        _primaryBounds.Width,
                        _primaryBounds.Height);
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.SetWindowBoundsTopMostNoActivate(
                this.Handle,
                _primaryBounds.X,
                _primaryBounds.Y,
                _primaryBounds.Width,
                _primaryBounds.Height);

            for (int i = 0; i < _secondaryOverlays.Count; i++)
            {
                _secondaryOverlays[i].Show(this);
                _secondaryOverlays[i].EnsureBounds();
            }

            UpdateCursorTracking();
            _hudForm.Show(this);
            _hudForm.UpdatePosition(_cursorX, _cursorY);

            _trackTimer.Start();
            Activate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Escape)
            {
                CancelPicker();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                CancelPicker();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateCursorTracking();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            HandleOverlayMouseUp(e.Button);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintCrosshair(e.Graphics, _primaryBounds, _cursorX, _cursorY);
        }

        internal void HandleOverlayMouseMove()
        {
            UpdateCursorTracking();
        }

        internal void HandleOverlayMouseUp(MouseButtons button)
        {
            if (_completed)
            {
                return;
            }

            if (button == MouseButtons.Right)
            {
                CancelPicker();
                return;
            }

            if (button == MouseButtons.Left)
            {
                NativeMethods.POINT pt;
                if (NativeMethods.GetCursorPos(out pt))
                {
                    PickedX = pt.X;
                    PickedY = pt.Y;
                }
                else
                {
                    Point fallback = Control.MousePosition;
                    PickedX = fallback.X;
                    PickedY = fallback.Y;
                }
                _completed = true;
                DialogResult = DialogResult.OK;
                CloseAllOverlays();
            }
        }

        internal void CancelPicker()
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
            DialogResult = DialogResult.Cancel;
            CloseAllOverlays();
        }

        private void OnTrackTick(object sender, EventArgs e)
        {
            UpdateCursorTracking();
        }

        private void UpdateCursorTracking()
        {
            NativeMethods.POINT pt;
            int nx;
            int ny;
            if (NativeMethods.GetCursorPos(out pt))
            {
                nx = pt.X;
                ny = pt.Y;
            }
            else
            {
                Point fallback = Control.MousePosition;
                nx = fallback.X;
                ny = fallback.Y;
            }

            if (nx == _cursorX && ny == _cursorY && _hudForm.Visible)
            {
                return;
            }

            _cursorX = nx;
            _cursorY = ny;

            Invalidate();
            for (int i = 0; i < _secondaryOverlays.Count; i++)
            {
                if (!_secondaryOverlays[i].IsDisposed)
                {
                    _secondaryOverlays[i].Invalidate();
                }
            }

            if (_hudForm != null && !_hudForm.IsDisposed && _hudForm.Visible)
            {
                _hudForm.UpdatePosition(_cursorX, _cursorY);
            }
        }

        private void CloseAllOverlays()
        {
            _trackTimer.Stop();
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _trackTimer.Stop();

            for (int i = 0; i < _secondaryOverlays.Count; i++)
            {
                MonitorOverlayForm overlay = _secondaryOverlays[i];
                if (overlay != null && !overlay.IsDisposed)
                {
                    overlay.Close();
                    overlay.Dispose();
                }
            }
            _secondaryOverlays.Clear();

            if (_hudForm != null && !_hudForm.IsDisposed)
            {
                _hudForm.Close();
                _hudForm.Dispose();
            }

            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_trackTimer != null)
                {
                    _trackTimer.Stop();
                    _trackTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private static void PaintCrosshair(Graphics g, Rectangle bounds, int cursorX, int cursorY)
        {
            int localX = cursorX - bounds.X;
            int localY = cursorY - bounds.Y;

            using (Pen guidePen = new Pen(Color.FromArgb(200, 88, 166, 255), 1f))
            {
                guidePen.DashStyle = DashStyle.Dash;
                if (localX >= 0 && localX < bounds.Width)
                {
                    g.DrawLine(guidePen, localX, 0, localX, bounds.Height);
                }
                if (localY >= 0 && localY < bounds.Height)
                {
                    g.DrawLine(guidePen, 0, localY, bounds.Width, localY);
                }
            }

            if (localX >= -24 && localX <= bounds.Width + 24 &&
                localY >= -24 && localY <= bounds.Height + 24)
            {
                using (Pen ringPen = new Pen(Color.White, 1.5f))
                {
                    g.DrawEllipse(ringPen, localX - 10, localY - 10, 20, 20);
                }
            }
        }

        private sealed class MonitorOverlayForm : Form
        {
            private readonly ScreenPickerForm _owner;
            private readonly Rectangle _targetBounds;

            public MonitorOverlayForm(ScreenPickerForm owner, Rectangle targetBounds)
            {
                _owner = owner;
                _targetBounds = targetBounds;

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);

                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                AutoScaleMode = AutoScaleMode.None;
                ShowInTaskbar = false;
                TopMost = true;
                DoubleBuffered = true;
                KeyPreview = true;
                BackColor = Color.FromArgb(10, 14, 22);
                Opacity = 0.28;
                Cursor = Cursors.Cross;
                Bounds = _targetBounds;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
                    return cp;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_DPICHANGED)
                {
                    if (IsHandleCreated)
                    {
                        NativeMethods.SetWindowBoundsTopMostNoActivate(
                            this.Handle,
                            _targetBounds.X,
                            _targetBounds.Y,
                            _targetBounds.Width,
                            _targetBounds.Height);
                    }
                    m.Result = IntPtr.Zero;
                    return;
                }
                base.WndProc(ref m);
            }

            public void EnsureBounds()
            {
                if (IsHandleCreated)
                {
                    NativeMethods.SetWindowBoundsTopMostNoActivate(
                        this.Handle,
                        _targetBounds.X,
                        _targetBounds.Y,
                        _targetBounds.Width,
                        _targetBounds.Height);
                }
                else
                {
                    Bounds = _targetBounds;
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if ((keyData & Keys.KeyCode) == Keys.Escape)
                {
                    _owner.CancelPicker();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == Keys.Escape)
                {
                    _owner.CancelPicker();
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                _owner.HandleOverlayMouseMove();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _owner.HandleOverlayMouseUp(e.Button);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                PaintCrosshair(e.Graphics, _targetBounds, _owner._cursorX, _owner._cursorY);
            }
        }

        private sealed class PickerHudForm : Form
        {
            private int _x;
            private int _y;

            public PickerHudForm()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);

                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                AutoScaleMode = AutoScaleMode.None;
                ShowInTaskbar = false;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(24, 28, 38);
                Opacity = 0.94;
                Size = new Size(148, 30);
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
                    cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE |
                                  NativeMethods.WS_EX_TOOLWINDOW |
                                  NativeMethods.WS_EX_TOPMOST |
                                  WS_EX_TRANSPARENT;
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
                base.WndProc(ref m);
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                NativeMethods.TryApplyRoundedCorners(this.Handle, true);
            }

            public void UpdatePosition(int cursorX, int cursorY)
            {
                _x = cursorX;
                _y = cursorY;

                Rectangle screenBounds = Screen.FromPoint(new Point(cursorX, cursorY)).Bounds;
                int targetX = cursorX + 18;
                int targetY = cursorY + 18;

                if (targetX + Width > screenBounds.Right - 8)
                {
                    targetX = cursorX - Width - 14;
                }
                if (targetY + Height > screenBounds.Bottom - 8)
                {
                    targetY = cursorY - Height - 14;
                }
                if (targetX < screenBounds.Left + 4)
                {
                    targetX = screenBounds.Left + 4;
                }
                if (targetY < screenBounds.Top + 4)
                {
                    targetY = screenBounds.Top + 4;
                }

                Bounds = new Rectangle(targetX, targetY, 148, 30);
                NativeMethods.SetTopMostNoActivate(this.Handle, true);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (Pen borderPen = new Pen(Color.FromArgb(88, 166, 255), 1f))
                {
                    g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                }

                string coordText = string.Format("{0}, {1}", _x, _y);
                using (Font f1 = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(130, 200, 255)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(coordText, f1, b1, new RectangleF(0, 0, Width, Height), sf);
                    sf.Dispose();
                }
            }
        }
    }
}
