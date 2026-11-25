using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuickTile
{
    public static class InputExecutor
    {
        public static void ExecuteAsync(TileConfig tile)
        {
            if (tile == null)
            {
                return;
            }

            TileConfig snapshot = tile.Clone();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    TileConfig item = (TileConfig)state;
                    // Let the tile's WM_LBUTTONUP settle cleanly before injecting mouse/keyboard stages.
                    Thread.Sleep(20);
                    ExecuteSync(item);
                }
                catch
                {
                }
            }, snapshot);
        }

        public static void ExecuteSync(TileConfig tile)
        {
            if (tile == null)
            {
                return;
            }

            if (tile.Action == ActionType.AnchorClick)
            {
                PerformAnchorClick(tile.AnchorX, tile.AnchorY, tile.ClickType, tile.RestoreCursor, tile.ClickHoldMs);
            }
            else
            {
                tile.NormalizeModifiers();
                SendHotkey(
                    tile.ModLCtrl,
                    tile.ModRCtrl,
                    tile.ModLAlt,
                    tile.ModRAlt,
                    tile.ModLShift,
                    tile.ModRShift,
                    tile.ModLWin,
                    tile.ModRWin,
                    tile.VirtualKey);
            }
        }

        public static void SendHotkey(bool ctrl, bool alt, bool shift, bool win, int vk)
        {
            SendHotkey(ctrl, false, alt, false, shift, false, win, false, vk);
        }

        public static void SendHotkey(
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
            NativeMethods.INPUT[] modDownEvents;
            NativeMethods.INPUT[] mainDownEvents;
            NativeMethods.INPUT[] mainUpEvents;
            NativeMethods.INPUT[] modUpEvents;

            BuildHotkeyStages(
                lCtrl, rCtrl,
                lAlt, rAlt,
                lShift, rShift,
                lWin, rWin,
                vk,
                out modDownEvents,
                out mainDownEvents,
                out mainUpEvents,
                out modUpEvents);

            if (modDownEvents.Length == 0 && mainDownEvents.Length == 0)
            {
                return;
            }

            int cbSize = Marshal.SizeOf(typeof(NativeMethods.INPUT));

            if (modDownEvents.Length > 0)
            {
                NativeMethods.SendInput((uint)modDownEvents.Length, modDownEvents, cbSize);
                Thread.Sleep(30);
            }

            if (mainDownEvents.Length > 0)
            {
                NativeMethods.SendInput((uint)mainDownEvents.Length, mainDownEvents, cbSize);
                Thread.Sleep(40);
                NativeMethods.SendInput((uint)mainUpEvents.Length, mainUpEvents, cbSize);

                if (modUpEvents.Length > 0)
                {
                    Thread.Sleep(30);
                    NativeMethods.SendInput((uint)modUpEvents.Length, modUpEvents, cbSize);
                }
            }
            else if (modUpEvents.Length > 0)
            {
                Thread.Sleep(40);
                NativeMethods.SendInput((uint)modUpEvents.Length, modUpEvents, cbSize);
            }
        }

        public static void PerformAnchorClick(int x, int y, MouseClickType clickType, bool restoreCursor)
        {
            PerformAnchorClick(x, y, clickType, restoreCursor, TileConfig.DefaultClickHoldMs);
        }

        public static void PerformAnchorClick(int x, int y, MouseClickType clickType, bool restoreCursor, int holdMs)
        {
            NativeMethods.POINT previousCursor;
            bool hasPrevious = NativeMethods.GetCursorPos(out previousCursor);

            int cbSize = Marshal.SizeOf(typeof(NativeMethods.INPUT));

            int virtX = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int virtY = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int virtW = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int virtH = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
            if (virtW <= 0) virtW = 1920;
            if (virtH <= 0) virtH = 1080;

            int absX;
            int absY;
            NativeMethods.ToVirtualDesktopAbsolute(x, y, virtX, virtY, virtW, virtH, out absX, out absY);

            // Emit a virtual-desktop hardware move so hover-sensitive custom UIs track cursor entry.
            NativeMethods.INPUT[] moveInputs = new NativeMethods.INPUT[]
            {
                CreateMouseInput(
                    absX,
                    absY,
                    NativeMethods.MOUSEEVENTF_MOVE |
                    NativeMethods.MOUSEEVENTF_ABSOLUTE |
                    NativeMethods.MOUSEEVENTF_VIRTUALDESK)
            };
            NativeMethods.SendInput((uint)moveInputs.Length, moveInputs, cbSize);
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(30);

            NativeMethods.INPUT[][] stages;
            int[] delays;
            BuildAnchorClickStages(clickType, holdMs, out stages, out delays);

            for (int i = 0; i < stages.Length; i++)
            {
                NativeMethods.INPUT[] stage = stages[i];
                if (stage != null && stage.Length > 0)
                {
                    NativeMethods.SendInput((uint)stage.Length, stage, cbSize);
                }
                if (delays != null && i < delays.Length && delays[i] > 0)
                {
                    Thread.Sleep(delays[i]);
                }
            }

            if (restoreCursor && hasPrevious)
            {
                Thread.Sleep(25);
                NativeMethods.SetCursorPos(previousCursor.X, previousCursor.Y);
            }
        }

        public static void BuildAnchorClickStages(
            MouseClickType clickType,
            int holdMs,
            out NativeMethods.INPUT[][] stages,
            out int[] delaysAfterStageMs)
        {
            int clampedHold = TileConfig.ClampClickHoldMs(holdMs);

            if (clickType == MouseClickType.RightClick)
            {
                if (clampedHold <= 0)
                {
                    stages = new NativeMethods.INPUT[][]
                    {
                        new NativeMethods.INPUT[]
                        {
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_RIGHTDOWN),
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP)
                        }
                    };
                    delaysAfterStageMs = new int[] { 0 };
                }
                else
                {
                    stages = new NativeMethods.INPUT[][]
                    {
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_RIGHTDOWN) },
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP) }
                    };
                    delaysAfterStageMs = new int[] { clampedHold, 0 };
                }
                return;
            }

            if (clickType == MouseClickType.DoubleClick)
            {
                const int DoubleClickGapMs = 35;
                if (clampedHold <= 0)
                {
                    stages = new NativeMethods.INPUT[][]
                    {
                        new NativeMethods.INPUT[]
                        {
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN),
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP)
                        },
                        new NativeMethods.INPUT[]
                        {
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN),
                            CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP)
                        }
                    };
                    delaysAfterStageMs = new int[] { DoubleClickGapMs, 0 };
                }
                else
                {
                    stages = new NativeMethods.INPUT[][]
                    {
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN) },
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP) },
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN) },
                        new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP) }
                    };
                    delaysAfterStageMs = new int[] { clampedHold, DoubleClickGapMs, clampedHold, 0 };
                }
                return;
            }

            if (clampedHold <= 0)
            {
                stages = new NativeMethods.INPUT[][]
                {
                    new NativeMethods.INPUT[]
                    {
                        CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN),
                        CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP)
                    }
                };
                delaysAfterStageMs = new int[] { 0 };
            }
            else
            {
                stages = new NativeMethods.INPUT[][]
                {
                    new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN) },
                    new NativeMethods.INPUT[] { CreateMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP) }
                };
                delaysAfterStageMs = new int[] { clampedHold, 0 };
            }
        }

        public static void BuildHotkeyEvents(
            bool ctrl,
            bool alt,
            bool shift,
            bool win,
            int vk,
            out NativeMethods.INPUT[] downEvents,
            out NativeMethods.INPUT[] upEvents)
        {
            BuildHotkeyEvents(ctrl, false, alt, false, shift, false, win, false, vk, out downEvents, out upEvents);
        }

        public static void BuildHotkeyEvents(
            bool lCtrl,
            bool rCtrl,
            bool lAlt,
            bool rAlt,
            bool lShift,
            bool rShift,
            bool lWin,
            bool rWin,
            int vk,
            out NativeMethods.INPUT[] downEvents,
            out NativeMethods.INPUT[] upEvents)
        {
            NativeMethods.INPUT[] modDownEvents;
            NativeMethods.INPUT[] mainDownEvents;
            NativeMethods.INPUT[] mainUpEvents;
            NativeMethods.INPUT[] modUpEvents;

            BuildHotkeyStages(
                lCtrl, rCtrl,
                lAlt, rAlt,
                lShift, rShift,
                lWin, rWin,
                vk,
                out modDownEvents,
                out mainDownEvents,
                out mainUpEvents,
                out modUpEvents);

            downEvents = new NativeMethods.INPUT[modDownEvents.Length + mainDownEvents.Length];
            for (int i = 0; i < modDownEvents.Length; i++)
            {
                downEvents[i] = modDownEvents[i];
            }
            for (int i = 0; i < mainDownEvents.Length; i++)
            {
                downEvents[modDownEvents.Length + i] = mainDownEvents[i];
            }

            upEvents = new NativeMethods.INPUT[mainUpEvents.Length + modUpEvents.Length];
            for (int i = 0; i < mainUpEvents.Length; i++)
            {
                upEvents[i] = mainUpEvents[i];
            }
            for (int i = 0; i < modUpEvents.Length; i++)
            {
                upEvents[mainUpEvents.Length + i] = modUpEvents[i];
            }
        }

        public static void BuildHotkeyStages(
            bool lCtrl,
            bool rCtrl,
            bool lAlt,
            bool rAlt,
            bool lShift,
            bool rShift,
            bool lWin,
            bool rWin,
            int vk,
            out NativeMethods.INPUT[] modDownEvents,
            out NativeMethods.INPUT[] mainDownEvents,
            out NativeMethods.INPUT[] mainUpEvents,
            out NativeMethods.INPUT[] modUpEvents)
        {
            List<int> modSequence = new List<int>();
            if (lCtrl)
            {
                modSequence.Add(NativeMethods.VK_LCONTROL);
            }
            if (rCtrl)
            {
                modSequence.Add(NativeMethods.VK_RCONTROL);
            }
            if (lAlt)
            {
                modSequence.Add(NativeMethods.VK_LMENU);
            }
            if (rAlt)
            {
                modSequence.Add(NativeMethods.VK_RMENU);
            }
            if (lShift)
            {
                modSequence.Add(NativeMethods.VK_LSHIFT);
            }
            if (rShift)
            {
                modSequence.Add(NativeMethods.VK_RSHIFT);
            }
            if (lWin)
            {
                modSequence.Add(NativeMethods.VK_LWIN);
            }
            if (rWin)
            {
                modSequence.Add(NativeMethods.VK_RWIN);
            }

            modDownEvents = new NativeMethods.INPUT[modSequence.Count];
            modUpEvents = new NativeMethods.INPUT[modSequence.Count];
            for (int i = 0; i < modSequence.Count; i++)
            {
                modDownEvents[i] = CreateKeyInput(modSequence[i], false);
                modUpEvents[i] = CreateKeyInput(modSequence[modSequence.Count - 1 - i], true);
            }

            if (vk > 0 && !modSequence.Contains(vk))
            {
                mainDownEvents = new NativeMethods.INPUT[] { CreateKeyInput(vk, false) };
                mainUpEvents = new NativeMethods.INPUT[] { CreateKeyInput(vk, true) };
            }
            else
            {
                mainDownEvents = new NativeMethods.INPUT[0];
                mainUpEvents = new NativeMethods.INPUT[0];
            }
        }

        private static NativeMethods.INPUT CreateKeyInput(int vkCode, bool keyUp)
        {
            uint flags = 0;
            if (NativeMethods.IsExtendedKey(vkCode))
            {
                flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            if (keyUp)
            {
                flags |= NativeMethods.KEYEVENTF_KEYUP;
            }

            ushort scan = (ushort)(NativeMethods.MapVirtualKey((uint)vkCode, 0) & 0xFF);

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.U.ki = new NativeMethods.KEYBDINPUT
            {
                wVk = (ushort)vkCode,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            return input;
        }

        private static NativeMethods.INPUT CreateMouseInput(uint flags)
        {
            return CreateMouseInput(0, 0, flags);
        }

        private static NativeMethods.INPUT CreateMouseInput(int dx, int dy, uint flags)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.U.mi = new NativeMethods.MOUSEINPUT
            {
                dx = dx,
                dy = dy,
                mouseData = 0,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            return input;
        }
    }
}
