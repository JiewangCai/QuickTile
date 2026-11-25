using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickTile
{
    public static class NativeMethods
    {
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_LAYERED = 0x00080000;

        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_DPICHANGED = 0x02E0;
        public const int MA_NOACTIVATE = 3;

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const uint LLKHF_EXTENDED = 0x01;
        public const uint LLKHF_UP = 0x80;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int INPUT_MOUSE = 0;
        public const int INPUT_KEYBOARD = 1;
        public const int INPUT_HARDWARE = 2;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        public const int GWL_EXSTYLE = -20;

        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_LMENU = 0xA4;
        public const int VK_RMENU = 0xA5;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtrSafe(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr(hWnd, nIndex);
            }
            return new IntPtr(GetWindowLong(hWnd, nIndex));
        }

        public static void EnablePerMonitorDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch
            {
                // Fallback below on older Windows builds
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }

        public static void ToVirtualDesktopAbsolute(
            int x,
            int y,
            int virtX,
            int virtY,
            int virtW,
            int virtH,
            out int absX,
            out int absY)
        {
            int denomX = virtW > 1 ? (virtW - 1) : 1;
            int denomY = virtH > 1 ? (virtH - 1) : 1;

            int rx = (int)Math.Round(((x - virtX) * 65535.0) / denomX);
            int ry = (int)Math.Round(((y - virtY) * 65535.0) / denomY);

            if (rx < 0) rx = 0;
            if (rx > 65535) rx = 65535;
            if (ry < 0) ry = 0;
            if (ry > 65535) ry = 65535;

            absX = rx;
            absY = ry;
        }

        public static Point ComputeMenuScreenPosition(Point clickPoint, Size menuSize, Rectangle monitorWorkArea)
        {
            int x = clickPoint.X;
            int y = clickPoint.Y;

            if (x + menuSize.Width > monitorWorkArea.Right - 4)
            {
                x = clickPoint.X - menuSize.Width;
            }
            if (y + menuSize.Height > monitorWorkArea.Bottom - 4)
            {
                y = clickPoint.Y - menuSize.Height;
            }

            int minX = monitorWorkArea.Left + 4;
            int maxX = Math.Max(minX, monitorWorkArea.Right - menuSize.Width - 4);
            int minY = monitorWorkArea.Top + 4;
            int maxY = Math.Max(minY, monitorWorkArea.Bottom - menuSize.Height - 4);

            if (x < minX) x = minX;
            if (x > maxX) x = maxX;
            if (y < minY) y = minY;
            if (y > maxY) y = maxY;

            return new Point(x, y);
        }

        public static void TryApplyRoundedCorners(IntPtr hwnd, bool small = false)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int preference = small ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, Marshal.SizeOf(typeof(int)));
            }
            catch
            {
                // Ignored on Windows 10 or systems without DWMWA_WINDOW_CORNER_PREFERENCE
            }
        }

        public static void SetTopMostNoActivate(IntPtr hwnd, bool topMost)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                topMost ? HWND_TOPMOST : HWND_NOTOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void SetWindowBoundsNoActivate(IntPtr hwnd, int x, int y, int width, int height)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                x,
                y,
                width,
                height,
                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static void SetWindowBoundsTopMostNoActivate(IntPtr hwnd, int x, int y, int width, int height)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                x,
                y,
                width,
                height,
                SWP_NOACTIVATE);
        }

        public static bool IsExtendedKey(int vk)
        {
            if (vk >= 0x21 && vk <= 0x28)
            {
                return true;
            }
            if (vk >= 0x2C && vk <= 0x2E)
            {
                return true;
            }
            if (vk >= 0x5B && vk <= 0x5D)
            {
                return true;
            }
            if (vk == 0x6F || vk == 0x90 || vk == 0xA3 || vk == 0xA5)
            {
                return true;
            }
            if (vk >= 0xAD && vk <= 0xB3)
            {
                return true;
            }
            return false;
        }

        public static string GetKeyDisplayName(int vk)
        {
            if (vk <= 0)
            {
                return Lang.T("(无)", "(None)");
            }

            if (vk >= 0x41 && vk <= 0x5A)
            {
                return ((char)vk).ToString();
            }
            if (vk >= 0x30 && vk <= 0x39)
            {
                return ((char)vk).ToString();
            }
            if (vk >= 0x70 && vk <= 0x87)
            {
                return string.Format("F{0}", vk - 0x70 + 1);
            }
            if (vk >= 0x60 && vk <= 0x69)
            {
                return string.Format("Num{0}", vk - 0x60);
            }

            switch (vk)
            {
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x13: return "Pause";
                case 0x14: return "CapsLock";
                case 0x1B: return "Esc";
                case 0x20: return "Space";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x2C: return "PrtSc";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x5B: return "LWin";
                case 0x5C: return "RWin";
                case 0x5D: return "Apps";
                case 0x6A: return "Num*";
                case 0x6B: return "Num+";
                case 0x6D: return "Num-";
                case 0x6E: return "Num.";
                case 0x6F: return "Num/";
                case 0x90: return "NumLock";
                case 0x91: return "ScrollLock";
                case 0xAD: return "Mute";
                case 0xAE: return "Vol-";
                case 0xAF: return "Vol+";
                case 0xB0: return "Next";
                case 0xB1: return "Prev";
                case 0xB2: return "Stop";
                case 0xB3: return "Play/Pause";
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "`";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
            }

            if (Enum.IsDefined(typeof(Keys), vk))
            {
                return ((Keys)vk).ToString();
            }

            return string.Format("VK_{0:X2}", vk);
        }

        public static string FormatHotkey(bool ctrl, bool alt, bool shift, bool win, int vk)
        {
            return FormatHotkey(ctrl, false, alt, false, shift, false, win, false, vk);
        }

        public static string FormatHotkey(
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
            List<string> parts = new List<string>();
            if (lWin)
            {
                parts.Add("LWin");
            }
            if (rWin)
            {
                parts.Add("RWin");
            }
            if (lCtrl)
            {
                parts.Add("LCtrl");
            }
            if (rCtrl)
            {
                parts.Add("RCtrl");
            }
            if (lAlt)
            {
                parts.Add("LAlt");
            }
            if (rAlt)
            {
                parts.Add("RAlt");
            }
            if (lShift)
            {
                parts.Add("LShift");
            }
            if (rShift)
            {
                parts.Add("RShift");
            }
            if (vk > 0)
            {
                parts.Add(GetKeyDisplayName(vk));
            }
            if (parts.Count == 0)
            {
                return Lang.T("未设置", "Not Set");
            }
            return string.Join("+", parts.ToArray());
        }

        public static List<KeyValuePair<int, string>> GetSupportedKeys()
        {
            List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
            list.Add(new KeyValuePair<int, string>(0, Lang.T("(无)", "(None)")));

            int vk;
            for (vk = 0x41; vk <= 0x5A; vk++)
            {
                list.Add(new KeyValuePair<int, string>(vk, GetKeyDisplayName(vk)));
            }
            for (vk = 0x30; vk <= 0x39; vk++)
            {
                list.Add(new KeyValuePair<int, string>(vk, GetKeyDisplayName(vk)));
            }
            for (vk = 0x70; vk <= 0x7B; vk++)
            {
                list.Add(new KeyValuePair<int, string>(vk, GetKeyDisplayName(vk)));
            }

            int[] extras = new int[]
            {
                0x0D, 0x20, 0x09, 0x1B, 0x08, 0x2E, 0x2D,
                0x24, 0x23, 0x21, 0x22,
                0x25, 0x26, 0x27, 0x28,
                0x2C, 0x5D, 0x13, 0x14,
                0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE,
                0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
                0x6A, 0x6B, 0x6D, 0x6E, 0x6F,
                0xAD, 0xAE, 0xAF, 0xB3, 0xB1, 0xB0, 0xB2
            };

            for (int i = 0; i < extras.Length; i++)
            {
                list.Add(new KeyValuePair<int, string>(extras[i], GetKeyDisplayName(extras[i])));
            }

            return list;
        }

        public static bool TryParseHotkey(
            string text,
            out bool ctrl,
            out bool alt,
            out bool shift,
            out bool win,
            out int vk)
        {
            bool lCtrl;
            bool rCtrl;
            bool lAlt;
            bool rAlt;
            bool lShift;
            bool rShift;
            bool lWin;
            bool rWin;

            bool ok = TryParseHotkey(
                text,
                out lCtrl, out rCtrl,
                out lAlt, out rAlt,
                out lShift, out rShift,
                out lWin, out rWin,
                out vk);

            ctrl = lCtrl || rCtrl;
            alt = lAlt || rAlt;
            shift = lShift || rShift;
            win = lWin || rWin;
            return ok;
        }

        public static bool TryParseHotkey(
            string text,
            out bool lCtrl,
            out bool rCtrl,
            out bool lAlt,
            out bool rAlt,
            out bool lShift,
            out bool rShift,
            out bool lWin,
            out bool rWin,
            out int vk)
        {
            lCtrl = false;
            rCtrl = false;
            lAlt = false;
            rAlt = false;
            lShift = false;
            rShift = false;
            lWin = false;
            rWin = false;
            vk = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            List<string> tokens = new List<string>();

            if (trimmed == "+")
            {
                tokens.Add("+");
            }
            else
            {
                string normalized = ReplaceCaseInsensitive(trimmed, "num+", "NumAdd");
                normalized = ReplaceCaseInsensitive(normalized, "vol+", "VolumeUp");

                string[] raw = normalized.Split('+');
                for (int i = 0; i < raw.Length; i++)
                {
                    string tok = raw[i].Trim();
                    if (tok.Length == 0)
                    {
                        if (i > 0 && i == raw.Length - 2 && raw[i + 1].Trim().Length == 0)
                        {
                            tokens.Add("+");
                            break;
                        }
                        return false;
                    }
                    tokens.Add(tok);
                }
            }

            if (tokens.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                string tok = tokens[i];
                string low = tok.ToLowerInvariant();

                if (low == "ctrl" || low == "control" || low == "lctrl" || low == "leftctrl" || low == "左ctrl")
                {
                    lCtrl = true;
                    continue;
                }
                if (low == "rctrl" || low == "rightctrl" || low == "右ctrl")
                {
                    rCtrl = true;
                    continue;
                }
                if (low == "alt" || low == "menu" || low == "lalt" || low == "leftalt" || low == "左alt")
                {
                    lAlt = true;
                    continue;
                }
                if (low == "ralt" || low == "rightalt" || low == "altgr" || low == "右alt")
                {
                    rAlt = true;
                    continue;
                }
                if (low == "shift" || low == "lshift" || low == "leftshift" || low == "左shift")
                {
                    lShift = true;
                    continue;
                }
                if (low == "rshift" || low == "rightshift" || low == "右shift")
                {
                    rShift = true;
                    continue;
                }
                if (low == "win" || low == "windows" || low == "super" || low == "meta" || low == "lwin" || low == "leftwin" || low == "左win")
                {
                    lWin = true;
                    continue;
                }
                if (low == "rwin" || low == "rightwin" || low == "右win")
                {
                    rWin = true;
                    continue;
                }

                int parsedVk;
                if (!TryParseMainKeyToken(low, out parsedVk))
                {
                    return false;
                }
                if (vk != 0)
                {
                    return false;
                }
                vk = parsedVk;
            }

            return vk > 0 || lCtrl || rCtrl || lAlt || rAlt || lShift || rShift || lWin || rWin;
        }

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search))
            {
                return input;
            }

            int idx = input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return input;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int pos = 0;
            while (idx >= 0)
            {
                sb.Append(input, pos, idx - pos);
                sb.Append(replacement);
                pos = idx + search.Length;
                idx = input.IndexOf(search, pos, StringComparison.OrdinalIgnoreCase);
            }
            sb.Append(input, pos, input.Length - pos);
            return sb.ToString();
        }

        private static bool TryParseMainKeyToken(string low, out int vk)
        {
            vk = 0;
            if (string.IsNullOrEmpty(low))
            {
                return false;
            }

            if (low.Length == 1)
            {
                char ch = low[0];
                if (ch >= 'a' && ch <= 'z')
                {
                    vk = (int)char.ToUpperInvariant(ch);
                    return true;
                }
                if (ch >= '0' && ch <= '9')
                {
                    vk = (int)ch;
                    return true;
                }
                switch (ch)
                {
                    case ';': vk = 0xBA; return true;
                    case '=': vk = 0xBB; return true;
                    case '+': vk = 0xBB; return true;
                    case ',': vk = 0xBC; return true;
                    case '-': vk = 0xBD; return true;
                    case '.': vk = 0xBE; return true;
                    case '/': vk = 0xBF; return true;
                    case '`': vk = 0xC0; return true;
                    case '~': vk = 0xC0; return true;
                    case '[': vk = 0xDB; return true;
                    case '\\': vk = 0xDC; return true;
                    case ']': vk = 0xDD; return true;
                    case '\'': vk = 0xDE; return true;
                }
            }

            if (low.Length == 2 && low[0] == 'd' && low[1] >= '0' && low[1] <= '9')
            {
                vk = (int)low[1];
                return true;
            }

            if (low.StartsWith("f") && low.Length >= 2 && low.Length <= 3)
            {
                int fNum;
                if (int.TryParse(low.Substring(1), out fNum) && fNum >= 1 && fNum <= 24)
                {
                    vk = 0x70 + (fNum - 1);
                    return true;
                }
            }

            if (low.StartsWith("num") && low.Length == 4 && low[3] >= '0' && low[3] <= '9')
            {
                vk = 0x60 + (low[3] - '0');
                return true;
            }
            if (low.StartsWith("numpad") && low.Length == 7 && low[6] >= '0' && low[6] <= '9')
            {
                vk = 0x60 + (low[6] - '0');
                return true;
            }

            switch (low)
            {
                case "space":
                case "spacebar":
                case "空格":
                    vk = 0x20; return true;
                case "enter":
                case "return":
                case "回车":
                    vk = 0x0D; return true;
                case "esc":
                case "escape":
                    vk = 0x1B; return true;
                case "tab":
                    vk = 0x09; return true;
                case "back":
                case "backspace":
                case "退格":
                    vk = 0x08; return true;
                case "del":
                case "delete":
                case "删除":
                    vk = 0x2E; return true;
                case "ins":
                case "insert":
                case "插入":
                    vk = 0x2D; return true;
                case "home":
                    vk = 0x24; return true;
                case "end":
                    vk = 0x23; return true;
                case "pgup":
                case "pageup":
                case "prior":
                    vk = 0x21; return true;
                case "pgdn":
                case "pagedown":
                    vk = 0x22; return true;
                case "up":
                case "上":
                    vk = 0x26; return true;
                case "down":
                case "下":
                    vk = 0x28; return true;
                case "left":
                case "左":
                    vk = 0x25; return true;
                case "right":
                case "右":
                    vk = 0x27; return true;
                case "prtsc":
                case "printscreen":
                case "snapshot":
                case "prntscrn":
                case "截图":
                    vk = 0x2C; return true;
                case "apps":
                case "contextmenu":
                    vk = 0x5D; return true;
                case "pause":
                case "break":
                    vk = 0x13; return true;
                case "capslock":
                case "caps":
                case "capital":
                    vk = 0x14; return true;
                case "numlock":
                    vk = 0x90; return true;
                case "scrolllock":
                case "scroll":
                    vk = 0x91; return true;
                case "numadd":
                case "add":
                    vk = 0x6B; return true;
                case "numsub":
                case "num-":
                case "subtract":
                    vk = 0x6D; return true;
                case "nummul":
                case "num*":
                case "multiply":
                    vk = 0x6A; return true;
                case "numdiv":
                case "num/":
                case "divide":
                    vk = 0x6F; return true;
                case "numdec":
                case "num.":
                case "decimal":
                    vk = 0x6E; return true;
                case "plus":
                case "equal":
                case "oemplus":
                    vk = 0xBB; return true;
                case "minus":
                case "oemminus":
                    vk = 0xBD; return true;
                case "comma":
                case "oemcomma":
                    vk = 0xBC; return true;
                case "period":
                case "oemperiod":
                    vk = 0xBE; return true;
                case "semicolon":
                case "oemsemicolon":
                case "oem1":
                    vk = 0xBA; return true;
                case "slash":
                case "oemquestion":
                case "oem2":
                    vk = 0xBF; return true;
                case "tilde":
                case "backtick":
                case "oemtilde":
                case "oem3":
                    vk = 0xC0; return true;
                case "oemopenbrackets":
                case "oem4":
                    vk = 0xDB; return true;
                case "backslash":
                case "oempipe":
                case "oem5":
                    vk = 0xDC; return true;
                case "oemclosebrackets":
                case "oem6":
                    vk = 0xDD; return true;
                case "quote":
                case "oemquotes":
                case "oem7":
                    vk = 0xDE; return true;
                case "mute":
                case "volumemute":
                    vk = 0xAD; return true;
                case "vol-":
                case "volumedown":
                    vk = 0xAE; return true;
                case "volumeup":
                    vk = 0xAF; return true;
                case "next":
                case "medianext":
                case "medianexttrack":
                    vk = 0xB0; return true;
                case "prev":
                case "mediaprev":
                case "mediaprevioustrack":
                    vk = 0xB1; return true;
                case "stop":
                case "mediastop":
                    vk = 0xB2; return true;
                case "play/pause":
                case "playpause":
                case "mediaplaypause":
                    vk = 0xB3; return true;
            }

            return false;
        }
    }
}
