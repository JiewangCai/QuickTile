using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickTile
{
    internal static class SelfTest
    {
        private static int _passed = 0;
        private static int _failed = 0;

        private static void Check(bool condition, string name, string detail)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine(string.Format("[PASS] {0}", name));
            }
            else
            {
                _failed++;
                Console.WriteLine(string.Format("[FAIL] {0} :: {1}", name, detail));
            }
        }

        private static void CheckEqualInt(int actual, int expected, string name)
        {
            Check(
                actual == expected,
                name,
                string.Format("expected={0}, actual={1}", expected, actual));
        }

        private static void CheckEqualString(string actual, string expected, string name)
        {
            Check(
                string.Equals(actual, expected, StringComparison.Ordinal),
                name,
                string.Format("expected='{0}', actual='{1}'", expected, actual));
        }

        private static void CheckEqualDouble(double actual, double expected, double eps, string name)
        {
            Check(
                Math.Abs(actual - expected) <= eps,
                name,
                string.Format("expected={0}, actual={1}", expected, actual));
        }

        private static void TestInputStructSize()
        {
            int size = Marshal.SizeOf(typeof(NativeMethods.INPUT));
            int expected = (IntPtr.Size == 8) ? 40 : 28;
            CheckEqualInt(size, expected, "Marshal.SizeOf(INPUT) matches architecture");
        }

        private static void TestExtendedKeys()
        {
            Check(NativeMethods.IsExtendedKey(0x2E), "IsExtendedKey(Delete 0x2E) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0x25), "IsExtendedKey(Left 0x25) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0x2C), "IsExtendedKey(Snapshot 0x2C) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0x5B), "IsExtendedKey(LWin 0x5B) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0x5C), "IsExtendedKey(RWin 0x5C) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0xA3), "IsExtendedKey(RCtrl 0xA3) == true", "returned false");
            Check(NativeMethods.IsExtendedKey(0xA5), "IsExtendedKey(RAlt 0xA5) == true", "returned false");
            Check(!NativeMethods.IsExtendedKey(0xA2), "IsExtendedKey(LCtrl 0xA2) == false", "returned true");
            Check(!NativeMethods.IsExtendedKey(0xA4), "IsExtendedKey(LAlt 0xA4) == false", "returned true");
            Check(!NativeMethods.IsExtendedKey(0xA0), "IsExtendedKey(LShift 0xA0) == false", "returned true");
            Check(!NativeMethods.IsExtendedKey(0xA1), "IsExtendedKey(RShift 0xA1) == false", "returned true");
            Check(!NativeMethods.IsExtendedKey(0x41), "IsExtendedKey(A 0x41) == false", "returned true");
        }

        private static void AssertKeyEvent(
            NativeMethods.INPUT ev,
            ushort expectedVk,
            bool expectExtended,
            bool expectKeyUp,
            string label)
        {
            CheckEqualInt((int)ev.type, NativeMethods.INPUT_KEYBOARD, label + ".type");
            CheckEqualInt((int)ev.U.ki.wVk, (int)expectedVk, label + ".wVk");

            ushort expectedScan = (ushort)(NativeMethods.MapVirtualKey((uint)expectedVk, 0) & 0xFF);
            CheckEqualInt((int)ev.U.ki.wScan, (int)expectedScan, label + ".wScan");

            uint expectedFlags = 0;
            if (expectExtended)
            {
                expectedFlags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            if (expectKeyUp)
            {
                expectedFlags |= NativeMethods.KEYEVENTF_KEYUP;
            }

            Check(
                ev.U.ki.dwFlags == expectedFlags,
                label + ".dwFlags",
                string.Format("expected=0x{0:X}, actual=0x{1:X}", expectedFlags, ev.U.ki.dwFlags));
        }

        private static void TestStagedHotkey(
            string title,
            bool lCtrl,
            bool rCtrl,
            bool lAlt,
            bool rAlt,
            bool lShift,
            bool rShift,
            bool lWin,
            bool rWin,
            int vk,
            ushort[] expectedModVks)
        {
            NativeMethods.INPUT[] modDown;
            NativeMethods.INPUT[] mainDown;
            NativeMethods.INPUT[] mainUp;
            NativeMethods.INPUT[] modUp;
            InputExecutor.BuildHotkeyStages(
                lCtrl, rCtrl,
                lAlt, rAlt,
                lShift, rShift,
                lWin, rWin,
                vk,
                out modDown,
                out mainDown,
                out mainUp,
                out modUp);

            int m = expectedModVks.Length;
            CheckEqualInt(modDown != null ? modDown.Length : -1, m, title + " modDown.Length");
            CheckEqualInt(modUp != null ? modUp.Length : -1, m, title + " modUp.Length");

            int expectMain = vk > 0 ? 1 : 0;
            CheckEqualInt(mainDown != null ? mainDown.Length : -1, expectMain, title + " mainDown.Length");
            CheckEqualInt(mainUp != null ? mainUp.Length : -1, expectMain, title + " mainUp.Length");

            if (modDown != null && modUp != null && modDown.Length == m && modUp.Length == m)
            {
                for (int i = 0; i < m; i++)
                {
                    ushort downVk = expectedModVks[i];
                    AssertKeyEvent(modDown[i], downVk, NativeMethods.IsExtendedKey((int)downVk), false, string.Format("{0} modDown[{1}]", title, i));

                    ushort upVk = expectedModVks[m - 1 - i];
                    AssertKeyEvent(modUp[i], upVk, NativeMethods.IsExtendedKey((int)upVk), true, string.Format("{0} modUp[{1}]", title, i));
                }
            }

            if (expectMain == 1 && mainDown != null && mainUp != null && mainDown.Length == 1 && mainUp.Length == 1)
            {
                AssertKeyEvent(mainDown[0], (ushort)vk, NativeMethods.IsExtendedKey(vk), false, title + " mainDown[0]");
                AssertKeyEvent(mainUp[0], (ushort)vk, NativeMethods.IsExtendedKey(vk), true, title + " mainUp[0]");
            }
        }

        private static void TestBuildHotkeyStagesAndEvents()
        {
            // LCtrl+Y (VK_LCONTROL=0xA2, 'Y'=0x59)
            TestStagedHotkey("LCtrl+Y", true, false, false, false, false, false, false, false, 0x59, new ushort[] { 0xA2 });

            // RCtrl+Y (VK_RCONTROL=0xA3 extended, 'Y'=0x59)
            TestStagedHotkey("RCtrl+Y", false, true, false, false, false, false, false, false, 0x59, new ushort[] { 0xA3 });

            // LAlt+Y (VK_LMENU=0xA4, 'Y'=0x59)
            TestStagedHotkey("LAlt+Y", false, false, true, false, false, false, false, false, 0x59, new ushort[] { 0xA4 });

            // RAlt+Y (VK_RMENU=0xA5 extended, 'Y'=0x59)
            TestStagedHotkey("RAlt+Y", false, false, false, true, false, false, false, false, 0x59, new ushort[] { 0xA5 });

            // LWin+LShift+S (VK_LSHIFT=0xA0, VK_LWIN=0x5B, 'S'=0x53)
            TestStagedHotkey("LWin+LShift+S", false, false, false, false, true, false, true, false, 0x53, new ushort[] { 0xA0, 0x5B });

            // RShift+F5 (VK_RSHIFT=0xA1, F5=0x74)
            TestStagedHotkey("RShift+F5", false, false, false, false, false, true, false, false, 0x74, new ushort[] { 0xA1 });

            // Delete (VK_DELETE=0x2E)
            TestStagedHotkey("Delete", false, false, false, false, false, false, false, false, 0x2E, new ushort[0]);

            // PrintScreen (VK_SNAPSHOT=0x2C)
            TestStagedHotkey("PrintScreen", false, false, false, false, false, false, false, false, 0x2C, new ushort[0]);

            // Flat BuildHotkeyEvents compatibility check for LCtrl+RAlt+Y
            NativeMethods.INPUT[] downEvents;
            NativeMethods.INPUT[] upEvents;
            InputExecutor.BuildHotkeyEvents(true, false, false, true, false, false, false, false, 0x59, out downEvents, out upEvents);
            CheckEqualInt(downEvents.Length, 3, "BuildHotkeyEvents LCtrl+RAlt+Y downEvents.Length");
            CheckEqualInt(upEvents.Length, 3, "BuildHotkeyEvents LCtrl+RAlt+Y upEvents.Length");
            AssertKeyEvent(downEvents[0], 0xA2, false, false, "LCtrl+RAlt+Y down[0]");
            AssertKeyEvent(downEvents[1], 0xA5, true, false, "LCtrl+RAlt+Y down[1]");
            AssertKeyEvent(downEvents[2], 0x59, false, false, "LCtrl+RAlt+Y down[2]");
            AssertKeyEvent(upEvents[0], 0x59, false, true, "LCtrl+RAlt+Y up[0]");
            AssertKeyEvent(upEvents[1], 0xA5, true, true, "LCtrl+RAlt+Y up[1]");
            AssertKeyEvent(upEvents[2], 0xA2, false, true, "LCtrl+RAlt+Y up[2]");
        }

        private static void CheckParseHotkeyValid9(
            string input,
            bool expectLCtrl,
            bool expectRCtrl,
            bool expectLAlt,
            bool expectRAlt,
            bool expectLShift,
            bool expectRShift,
            bool expectLWin,
            bool expectRWin,
            int expectVk)
        {
            bool lCtrl;
            bool rCtrl;
            bool lAlt;
            bool rAlt;
            bool lShift;
            bool rShift;
            bool lWin;
            bool rWin;
            int vk;
            bool ok = NativeMethods.TryParseHotkey(
                input,
                out lCtrl, out rCtrl,
                out lAlt, out rAlt,
                out lShift, out rShift,
                out lWin, out rWin,
                out vk);
            Check(ok, string.Format("TryParseHotkey('{0}') succeeds", input), "returned false");
            if (ok)
            {
                bool match =
                    lCtrl == expectLCtrl && rCtrl == expectRCtrl &&
                    lAlt == expectLAlt && rAlt == expectRAlt &&
                    lShift == expectLShift && rShift == expectRShift &&
                    lWin == expectLWin && rWin == expectRWin &&
                    vk == expectVk;
                Check(
                    match,
                    string.Format("TryParseHotkey('{0}') L/R fields match", input),
                    string.Format(
                        "actual=({0},{1},{2},{3},{4},{5},{6},{7},0x{8:X2})",
                        lCtrl, rCtrl, lAlt, rAlt, lShift, rShift, lWin, rWin, vk));
            }
        }

        private static void CheckParseHotkeyInvalid(string input)
        {
            bool lCtrl;
            bool rCtrl;
            bool lAlt;
            bool rAlt;
            bool lShift;
            bool rShift;
            bool lWin;
            bool rWin;
            int vk;
            bool ok = NativeMethods.TryParseHotkey(
                input,
                out lCtrl, out rCtrl,
                out lAlt, out rAlt,
                out lShift, out rShift,
                out lWin, out rWin,
                out vk);
            Check(!ok, string.Format("TryParseHotkey('{0}') rejects invalid", input), "unexpectedly succeeded");
        }

        private static void TestTryParseHotkey()
        {
            CheckParseHotkeyValid9("Ctrl+Y", true, false, false, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("LCtrl+Y", true, false, false, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("RCtrl+Y", false, true, false, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("左Ctrl+Y", true, false, false, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("右Ctrl+Y", false, true, false, false, false, false, false, false, 0x59);

            CheckParseHotkeyValid9("Alt+Y", false, false, true, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("LAlt+Y", false, false, true, false, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("RAlt+Y", false, false, false, true, false, false, false, false, 0x59);
            CheckParseHotkeyValid9("右Alt+Y", false, false, false, true, false, false, false, false, 0x59);

            CheckParseHotkeyValid9("LShift+A", false, false, false, false, true, false, false, false, 0x41);
            CheckParseHotkeyValid9("RShift+A", false, false, false, false, false, true, false, false, 0x41);
            CheckParseHotkeyValid9("LWin+D", false, false, false, false, false, false, true, false, 0x44);
            CheckParseHotkeyValid9("RWin+D", false, false, false, false, false, false, false, true, 0x44);
            CheckParseHotkeyValid9("RCtrl+RAlt+Y", false, true, false, true, false, false, false, false, 0x59);

            CheckParseHotkeyValid9("ctrl + shift + s", true, false, false, false, true, false, false, false, 0x53);
            CheckParseHotkeyValid9("Alt+F4", false, false, true, false, false, false, false, false, 0x73);
            CheckParseHotkeyValid9("F5", false, false, false, false, false, false, false, false, 0x74);
            CheckParseHotkeyValid9("Space", false, false, false, false, false, false, false, false, 0x20);
            CheckParseHotkeyValid9("空格", false, false, false, false, false, false, false, false, 0x20);
            CheckParseHotkeyValid9("Enter", false, false, false, false, false, false, false, false, 0x0D);
            CheckParseHotkeyValid9("Esc", false, false, false, false, false, false, false, false, 0x1B);
            CheckParseHotkeyValid9("Ctrl++", true, false, false, false, false, false, false, false, 0xBB);
            CheckParseHotkeyValid9("Ctrl+Num+", true, false, false, false, false, false, false, false, 0x6B);

            CheckParseHotkeyInvalid("");
            CheckParseHotkeyInvalid("Ctrl+NotAKey");
            CheckParseHotkeyInvalid("Ctrl+");
        }

        private static void TestTileConfigMethods()
        {
            CheckEqualInt(TileConfig.ClampSize(-10), 28, "ClampSize(-10) -> 28");
            CheckEqualInt(TileConfig.ClampSize(27), 28, "ClampSize(27) -> 28");
            CheckEqualInt(TileConfig.ClampSize(28), 28, "ClampSize(28) -> 28");
            CheckEqualInt(TileConfig.ClampSize(68), 68, "ClampSize(68) -> 68");
            CheckEqualInt(TileConfig.ClampSize(320), 320, "ClampSize(320) -> 320");
            CheckEqualInt(TileConfig.ClampSize(500), 320, "ClampSize(500) -> 320");

            CheckEqualDouble(TileConfig.ClampOpacity(0.0), 0.25, 1e-6, "ClampOpacity(0.0) -> 0.25");
            CheckEqualDouble(TileConfig.ClampOpacity(0.24), 0.25, 1e-6, "ClampOpacity(0.24) -> 0.25");
            CheckEqualDouble(TileConfig.ClampOpacity(0.25), 0.25, 1e-6, "ClampOpacity(0.25) -> 0.25");
            CheckEqualDouble(TileConfig.ClampOpacity(0.92), 0.92, 1e-6, "ClampOpacity(0.92) -> 0.92");
            CheckEqualDouble(TileConfig.ClampOpacity(1.0), 1.0, 1e-6, "ClampOpacity(1.0) -> 1.0");
            CheckEqualDouble(TileConfig.ClampOpacity(1.5), 1.0, 1e-6, "ClampOpacity(1.5) -> 1.0");

            CheckEqualInt(TileConfig.ClampClickHoldMs(-10), 0, "ClampClickHoldMs(-10) -> 0");
            CheckEqualInt(TileConfig.ClampClickHoldMs(0), 0, "ClampClickHoldMs(0) -> 0");
            CheckEqualInt(TileConfig.ClampClickHoldMs(30), 30, "ClampClickHoldMs(30) -> 30");
            CheckEqualInt(TileConfig.ClampClickHoldMs(500), 500, "ClampClickHoldMs(500) -> 500");
            CheckEqualInt(TileConfig.ClampClickHoldMs(30000), 30000, "ClampClickHoldMs(30000) -> 30000");
            CheckEqualInt(TileConfig.ClampClickHoldMs(99999), 30000, "ClampClickHoldMs(99999) -> 30000");

            TileConfig orig = new TileConfig();
            orig.Id = "tile-custom-1";
            orig.Label = "多屏锚点";
            orig.X = -1200;
            orig.Y = -727;
            orig.Size = 96;
            orig.BackColorHex = "#1E293B";
            orig.AccentColorHex = "#38BDF8";
            orig.TextColorHex = "#F8FAFC";
            orig.Opacity = 0.85;
            orig.Action = ActionType.AnchorClick;
            orig.ModRCtrl = true;
            orig.ModLShift = true;
            orig.SyncLegacyModifiers();
            orig.VirtualKey = 0x53;
            orig.AnchorX = -800;
            orig.AnchorY = -141;
            orig.ClickType = MouseClickType.DoubleClick;
            orig.RestoreCursor = false;
            orig.ClickHoldMs = 1500;

            TileConfig clone = orig.Clone();
            Check(clone != null && !object.ReferenceEquals(orig, clone), "Clone returns distinct instance", "same reference or null");
            if (clone != null)
            {
                CheckEqualString(clone.Id, orig.Id, "Clone preserves Id");
                CheckEqualString(clone.Label, orig.Label, "Clone preserves Label");
                CheckEqualInt(clone.X, -1200, "Clone preserves negative X");
                CheckEqualInt(clone.Y, -727, "Clone preserves negative Y");
                CheckEqualInt(clone.Size, 96, "Clone preserves Size");
                CheckEqualDouble(clone.Opacity, 0.85, 1e-6, "Clone preserves Opacity");
                Check(clone.Action == ActionType.AnchorClick, "Clone preserves Action", "mismatch");
                Check(clone.ModRCtrl && clone.ModLShift, "Clone preserves L/R modifiers", "mismatch");
                CheckEqualInt(clone.AnchorX, -800, "Clone preserves negative AnchorX");
                CheckEqualInt(clone.AnchorY, -141, "Clone preserves negative AnchorY");
                Check(clone.ClickType == MouseClickType.DoubleClick, "Clone preserves ClickType", "mismatch");
                Check(!clone.RestoreCursor, "Clone preserves RestoreCursor", "mismatch");
                CheckEqualInt(clone.ClickHoldMs, 1500, "Clone preserves ClickHoldMs");

                clone.Label = "Changed";
                clone.X = 500;
                clone.AnchorX = 100;
                clone.ModRCtrl = false;
                clone.ClickHoldMs = 0;
                CheckEqualString(orig.Label, "多屏锚点", "Mutating clone does not affect original Label");
                CheckEqualInt(orig.X, -1200, "Mutating clone does not affect original X");
                CheckEqualInt(orig.AnchorX, -800, "Mutating clone does not affect original AnchorX");
                Check(orig.ModRCtrl, "Mutating clone does not affect original ModRCtrl", "mutated");
                CheckEqualInt(orig.ClickHoldMs, 1500, "Mutating clone does not affect original ClickHoldMs");
            }

            TileConfig hkTile = new TileConfig();
            hkTile.Action = ActionType.Hotkey;
            hkTile.ModRCtrl = true;
            hkTile.VirtualKey = 0x59;
            CheckEqualString(hkTile.GetSubtitle(), "RCtrl+Y", "GetSubtitle for RCtrl+Y");

            string anchorSubtitle = orig.GetSubtitle();
            Check(
                !string.IsNullOrEmpty(anchorSubtitle) && anchorSubtitle.IndexOf("-800", StringComparison.Ordinal) >= 0 && anchorSubtitle.IndexOf("-141", StringComparison.Ordinal) >= 0,
                "GetSubtitle for AnchorClick contains negative coordinates",
                string.Format("actual='{0}'", anchorSubtitle));

            TileConfig subTile = new TileConfig();
            subTile.Action = ActionType.AnchorClick;
            subTile.AnchorX = 120;
            subTile.AnchorY = 340;
            subTile.ClickType = MouseClickType.LeftClick;

            subTile.ClickHoldMs = 0;
            CheckEqualString(subTile.GetSubtitle(), "左键 120,340", "GetSubtitle AnchorClick 0ms omits duration");
            subTile.ClickHoldMs = 30;
            CheckEqualString(subTile.GetSubtitle(), "左键 120,340", "GetSubtitle AnchorClick default 30ms omits duration");
            subTile.ClickHoldMs = 200;
            CheckEqualString(subTile.GetSubtitle(), "左键 120,340 (200ms)", "GetSubtitle AnchorClick 200ms shows (200ms)");
            subTile.ClickHoldMs = 1500;
            CheckEqualString(subTile.GetSubtitle(), "左键 120,340 (1500ms)", "GetSubtitle AnchorClick 1500ms shows (1500ms)");
            subTile.ClickHoldMs = 2000;
            CheckEqualString(subTile.GetSubtitle(), "左键 120,340 (2s)", "GetSubtitle AnchorClick 2000ms shows (2s)");
        }

        private static void AssertMouseEvent(NativeMethods.INPUT ev, uint expectedFlags, string label)
        {
            CheckEqualInt((int)ev.type, NativeMethods.INPUT_MOUSE, label + ".type");
            Check(
                ev.U.mi.dwFlags == expectedFlags,
                label + ".dwFlags",
                string.Format("expected=0x{0:X}, actual=0x{1:X}", expectedFlags, ev.U.mi.dwFlags));
        }

        private static void TestBuildAnchorClickStages()
        {
            NativeMethods.INPUT[][] stages;
            int[] delays;

            // LeftClick 0ms -> 1 stage [DOWN, UP], delay [0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.LeftClick, 0, out stages, out delays);
            CheckEqualInt(stages.Length, 1, "LeftClick 0ms stages.Length == 1");
            CheckEqualInt(delays.Length, 1, "LeftClick 0ms delays.Length == 1");
            if (stages.Length == 1 && delays.Length == 1)
            {
                CheckEqualInt(stages[0].Length, 2, "LeftClick 0ms stage[0].Length == 2");
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "LeftClick 0ms stage[0][0]");
                AssertMouseEvent(stages[0][1], NativeMethods.MOUSEEVENTF_LEFTUP, "LeftClick 0ms stage[0][1]");
                CheckEqualInt(delays[0], 0, "LeftClick 0ms delays[0] == 0");
            }

            // LeftClick 30ms -> 2 stages [DOWN], [UP], delays [30, 0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.LeftClick, 30, out stages, out delays);
            CheckEqualInt(stages.Length, 2, "LeftClick 30ms stages.Length == 2");
            CheckEqualInt(delays.Length, 2, "LeftClick 30ms delays.Length == 2");
            if (stages.Length == 2 && delays.Length == 2)
            {
                CheckEqualInt(stages[0].Length, 1, "LeftClick 30ms stage[0].Length == 1");
                CheckEqualInt(stages[1].Length, 1, "LeftClick 30ms stage[1].Length == 1");
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "LeftClick 30ms stage[0][0]");
                AssertMouseEvent(stages[1][0], NativeMethods.MOUSEEVENTF_LEFTUP, "LeftClick 30ms stage[1][0]");
                CheckEqualInt(delays[0], 30, "LeftClick 30ms delays[0] == 30");
                CheckEqualInt(delays[1], 0, "LeftClick 30ms delays[1] == 0");
            }

            // LeftClick 1500ms -> 2 stages [DOWN], [UP], delays [1500, 0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.LeftClick, 1500, out stages, out delays);
            CheckEqualInt(stages.Length, 2, "LeftClick 1500ms stages.Length == 2");
            CheckEqualInt(delays.Length, 2, "LeftClick 1500ms delays.Length == 2");
            if (stages.Length == 2 && delays.Length == 2)
            {
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "LeftClick 1500ms stage[0][0]");
                AssertMouseEvent(stages[1][0], NativeMethods.MOUSEEVENTF_LEFTUP, "LeftClick 1500ms stage[1][0]");
                CheckEqualInt(delays[0], 1500, "LeftClick 1500ms delays[0] == 1500");
                CheckEqualInt(delays[1], 0, "LeftClick 1500ms delays[1] == 0");
            }

            // RightClick 200ms -> 2 stages [RIGHTDOWN], [RIGHTUP], delays [200, 0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.RightClick, 200, out stages, out delays);
            CheckEqualInt(stages.Length, 2, "RightClick 200ms stages.Length == 2");
            CheckEqualInt(delays.Length, 2, "RightClick 200ms delays.Length == 2");
            if (stages.Length == 2 && delays.Length == 2)
            {
                CheckEqualInt(stages[0].Length, 1, "RightClick 200ms stage[0].Length == 1");
                CheckEqualInt(stages[1].Length, 1, "RightClick 200ms stage[1].Length == 1");
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_RIGHTDOWN, "RightClick 200ms stage[0][0]");
                AssertMouseEvent(stages[1][0], NativeMethods.MOUSEEVENTF_RIGHTUP, "RightClick 200ms stage[1][0]");
                CheckEqualInt(delays[0], 200, "RightClick 200ms delays[0] == 200");
                CheckEqualInt(delays[1], 0, "RightClick 200ms delays[1] == 0");
            }

            // DoubleClick 0ms -> 2 stages [DOWN, UP], [DOWN, UP], delays [35, 0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.DoubleClick, 0, out stages, out delays);
            CheckEqualInt(stages.Length, 2, "DoubleClick 0ms stages.Length == 2");
            CheckEqualInt(delays.Length, 2, "DoubleClick 0ms delays.Length == 2");
            if (stages.Length == 2 && delays.Length == 2)
            {
                CheckEqualInt(stages[0].Length, 2, "DoubleClick 0ms stage[0].Length == 2");
                CheckEqualInt(stages[1].Length, 2, "DoubleClick 0ms stage[1].Length == 2");
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "DoubleClick 0ms stage[0][0]");
                AssertMouseEvent(stages[0][1], NativeMethods.MOUSEEVENTF_LEFTUP, "DoubleClick 0ms stage[0][1]");
                AssertMouseEvent(stages[1][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "DoubleClick 0ms stage[1][0]");
                AssertMouseEvent(stages[1][1], NativeMethods.MOUSEEVENTF_LEFTUP, "DoubleClick 0ms stage[1][1]");
                CheckEqualInt(delays[0], 35, "DoubleClick 0ms delays[0] == 35");
                CheckEqualInt(delays[1], 0, "DoubleClick 0ms delays[1] == 0");
            }

            // DoubleClick 40ms -> 4 stages [DOWN], [UP], [DOWN], [UP], delays [40, 35, 40, 0]
            InputExecutor.BuildAnchorClickStages(MouseClickType.DoubleClick, 40, out stages, out delays);
            CheckEqualInt(stages.Length, 4, "DoubleClick 40ms stages.Length == 4");
            CheckEqualInt(delays.Length, 4, "DoubleClick 40ms delays.Length == 4");
            if (stages.Length == 4 && delays.Length == 4)
            {
                AssertMouseEvent(stages[0][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "DoubleClick 40ms stage[0][0]");
                AssertMouseEvent(stages[1][0], NativeMethods.MOUSEEVENTF_LEFTUP, "DoubleClick 40ms stage[1][0]");
                AssertMouseEvent(stages[2][0], NativeMethods.MOUSEEVENTF_LEFTDOWN, "DoubleClick 40ms stage[2][0]");
                AssertMouseEvent(stages[3][0], NativeMethods.MOUSEEVENTF_LEFTUP, "DoubleClick 40ms stage[3][0]");
                CheckEqualInt(delays[0], 40, "DoubleClick 40ms delays[0] == 40");
                CheckEqualInt(delays[1], 35, "DoubleClick 40ms delays[1] == 35");
                CheckEqualInt(delays[2], 40, "DoubleClick 40ms delays[2] == 40");
                CheckEqualInt(delays[3], 0, "DoubleClick 40ms delays[3] == 0");
            }
        }

        private static void TestConfigStoreRoundTripAndRecovery()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "QuickTile_SelfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string cfgPath = Path.Combine(tempDir, "tiles.json");

            try
            {
                // 1. Multi-profile round-trip with negative coords, L/R modifiers, separate LockPosition/LockSize
                AppConfig cfg = new AppConfig();
                cfg.AlwaysOnTop = false;
                cfg.LockPosition = true;
                cfg.LockSize = true;
                cfg.ActiveProfileId = "profile-work";

                TileProfile p1 = new TileProfile();
                p1.Id = "profile-default";
                p1.Name = "默认方案";

                TileConfig t1 = new TileConfig();
                t1.Id = "neg-monitor-tile";
                t1.Label = "左屏锚点";
                t1.X = -1200;
                t1.Y = -727;
                t1.Size = 84;
                t1.Opacity = 0.78;
                t1.Action = ActionType.AnchorClick;
                t1.AnchorX = -800;
                t1.AnchorY = -141;
                t1.ClickType = MouseClickType.RightClick;
                t1.RestoreCursor = false;
                t1.ClickHoldMs = 1500;
                p1.Tiles.Add(t1);

                TileProfile p2 = new TileProfile();
                p2.Id = "profile-work";
                p2.Name = "工作方案";

                TileConfig t2 = new TileConfig();
                t2.Id = "lr-hk-tile";
                t2.Label = "右组合";
                t2.X = 6300;
                t2.Y = 1600;
                t2.Size = 68;
                t2.Action = ActionType.Hotkey;
                t2.ModRCtrl = true;
                t2.ModRAlt = true;
                t2.ModLShift = true;
                t2.ModRWin = true;
                t2.SyncLegacyModifiers();
                t2.VirtualKey = 0x59;
                p2.Tiles.Add(t2);

                cfg.Profiles.Add(p1);
                cfg.Profiles.Add(p2);

                ConfigStore.Save(cfg, cfgPath);
                Check(File.Exists(cfgPath), "ConfigStore.Save writes target file", "file missing");

                AppConfig loaded = ConfigStore.Load(cfgPath);
                Check(loaded != null && loaded.Profiles != null, "ConfigStore.Load returns non-null Profiles", "null");
                if (loaded != null && loaded.Profiles != null)
                {
                    Check(!loaded.AlwaysOnTop, "Loaded AlwaysOnTop == false", "expected false");
                    Check(loaded.LockPosition, "Loaded LockPosition == true", "expected true");
                    Check(loaded.LockSize, "Loaded LockSize == true", "expected true");
                    CheckEqualString(loaded.ActiveProfileId, "profile-work", "Loaded ActiveProfileId == 'profile-work'");
                    Check(loaded.Tiles == null, "Normalized AppConfig.Tiles cleared to null", "not null");
                    CheckEqualInt(loaded.Profiles.Count, 2, "Loaded Profiles.Count == 2");

                    TileProfile activeProf = loaded.GetActiveProfile();
                    Check(activeProf != null && string.Equals(activeProf.Id, "profile-work", StringComparison.Ordinal), "GetActiveProfile returns profile-work", "mismatch");

                    if (loaded.Profiles.Count == 2)
                    {
                        TileConfig lt1 = loaded.Profiles[0].Tiles[0];
                        CheckEqualString(loaded.Profiles[0].Name, "默认方案", "Loaded Profile[0].Name");
                        CheckEqualString(lt1.Id, "neg-monitor-tile", "Loaded Profile[0].Tile[0].Id");
                        CheckEqualString(lt1.Label, "左屏锚点", "Loaded Profile[0].Tile[0].Label UTF-8");
                        CheckEqualInt(lt1.X, -1200, "Loaded Tile[0].X negative preserved");
                        CheckEqualInt(lt1.Y, -727, "Loaded Tile[0].Y negative preserved");
                        CheckEqualInt(lt1.AnchorX, -800, "Loaded Tile[0].AnchorX negative preserved");
                        CheckEqualInt(lt1.AnchorY, -141, "Loaded Tile[0].AnchorY negative preserved");
                        CheckEqualInt(lt1.Size, 84, "Loaded Tile[0].Size");
                        CheckEqualDouble(lt1.Opacity, 0.78, 1e-6, "Loaded Tile[0].Opacity");
                        Check(lt1.Action == ActionType.AnchorClick, "Loaded Tile[0].Action", "mismatch");
                        Check(lt1.ClickType == MouseClickType.RightClick, "Loaded Tile[0].ClickType", "mismatch");
                        Check(!lt1.RestoreCursor, "Loaded Tile[0].RestoreCursor", "mismatch");
                        CheckEqualInt(lt1.ClickHoldMs, 1500, "Loaded Tile[0].ClickHoldMs == 1500");

                        TileConfig lt2 = loaded.Profiles[1].Tiles[0];
                        CheckEqualString(loaded.Profiles[1].Name, "工作方案", "Loaded Profile[1].Name");
                        CheckEqualString(lt2.Label, "右组合", "Loaded Profile[1].Tile[0].Label UTF-8");
                        Check(lt2.ModRCtrl && !lt2.ModLCtrl && lt2.ModRAlt && !lt2.ModLAlt && lt2.ModLShift && !lt2.ModRShift && lt2.ModRWin && !lt2.ModLWin && lt2.VirtualKey == 0x59,
                            "Loaded Tile[1] L/R modifier flags preserved",
                            "mismatch");
                    }
                }

                // 2. Atomic overwrite + explicit ClickHoldMs == 0 test
                AppConfig cfg2 = new AppConfig();
                TileProfile pOverwrite = new TileProfile();
                pOverwrite.Id = "p-over";
                pOverwrite.Name = "单方案";
                TileConfig t3 = new TileConfig();
                t3.Id = "overwrite-tile";
                t3.Label = "覆盖测试";
                t3.X = -450;
                t3.Y = -200;
                t3.Action = ActionType.AnchorClick;
                t3.ClickHoldMs = 0;
                pOverwrite.Tiles.Add(t3);
                cfg2.Profiles.Add(pOverwrite);
                cfg2.ActiveProfileId = "p-over";

                ConfigStore.Save(cfg2, cfgPath);
                AppConfig reloaded = ConfigStore.Load(cfgPath);
                Check(reloaded != null && reloaded.GetActiveTiles().Count == 1, "Atomic overwrite reloads 1 tile", "count mismatch");
                if (reloaded != null && reloaded.GetActiveTiles().Count == 1)
                {
                    TileConfig rt = reloaded.GetActiveTiles()[0];
                    CheckEqualString(rt.Id, "overwrite-tile", "Atomic overwrite preserves new tile Id");
                    CheckEqualInt(rt.X, -450, "Atomic overwrite preserves negative X");
                    CheckEqualInt(rt.ClickHoldMs, 0, "Atomic overwrite preserves explicit ClickHoldMs == 0");
                }
                string[] filesAfterSave = Directory.GetFiles(tempDir);
                CheckEqualInt(filesAfterSave.Length, 1, "Atomic overwrite leaves no stale temp files");

                // 3. Strict >= 1 tile invariant when profile has 0 tiles
                File.WriteAllText(cfgPath, "{\"AlwaysOnTop\":true,\"ActiveProfileId\":\"empty-p\",\"Profiles\":[{\"Id\":\"empty-p\",\"Name\":\"空方案\",\"Tiles\":[]}]}", Encoding.UTF8);
                AppConfig nonEmpty = ConfigStore.Load(cfgPath);
                Check(nonEmpty != null && nonEmpty.Profiles != null && nonEmpty.Profiles.Count == 1 && nonEmpty.Profiles[0].Tiles.Count == 1,
                    "Empty profile Tiles array auto-populates strict minimum 1 fallback tile",
                    "failed invariant");

                // 4. Corrupted JSON fallback to defaults
                File.WriteAllText(cfgPath, "{ \"Profiles\": [ broken json !!!", Encoding.UTF8);
                AppConfig fallbackCorrupt = ConfigStore.Load(cfgPath);
                Check(
                    fallbackCorrupt != null && fallbackCorrupt.Profiles != null && fallbackCorrupt.Profiles.Count == 1 && fallbackCorrupt.GetActiveTiles().Count == 4,
                    "Corrupted JSON falls back cleanly to 1 default profile with 4 tiles",
                    "fallback failed");

                // 5. Empty file fallback to defaults
                File.WriteAllText(cfgPath, "   ", Encoding.UTF8);
                AppConfig fallbackEmpty = ConfigStore.Load(cfgPath);
                Check(
                    fallbackEmpty != null && fallbackEmpty.GetActiveTiles().Count == 4,
                    "Empty JSON falls back cleanly to 4 default tiles",
                    "fallback failed");

                // 6. Legacy single-list JSON migration (ModAlt -> ModLAlt, ClickHoldMs default, Tiles -> 默认方案)
                File.WriteAllText(cfgPath, "{\"AlwaysOnTop\":true,\"EditMode\":false,\"ShowSettingsOnStartup\":true,\"Tiles\":[{\"Id\":\"legacy1\",\"Label\":\"旧方块\",\"X\":10,\"Y\":20,\"Size\":68,\"ModAlt\":true,\"VirtualKey\":89},{\"Id\":\"legacy2\",\"Label\":\"语音\",\"X\":2268,\"Y\":554,\"Size\":171,\"Action\":1,\"AnchorX\":2402,\"AnchorY\":1333,\"ClickHoldMs\":200,\"Opacity\":1}]}", Encoding.UTF8);
                AppConfig legacy = ConfigStore.Load(cfgPath);
                Check(legacy != null && legacy.Profiles != null && legacy.Profiles.Count == 1 && legacy.GetActiveTiles().Count == 2,
                    "Legacy single-list JSON migrates into Profiles[0] with 2 tiles",
                    "migration failed");
                if (legacy != null && legacy.GetActiveTiles().Count == 2)
                {
                    CheckEqualString(legacy.Profiles[0].Name, "默认方案", "Migrated legacy profile named 默认方案");
                    Check(!legacy.LockPosition && !legacy.LockSize, "Legacy JSON defaults LockPosition == false and LockSize == false", "expected false");
                    Check(legacy.GetActiveTiles()[0].ModLAlt && !legacy.GetActiveTiles()[0].ModRAlt, "Legacy ModAlt migrates to ModLAlt", "migration failed");
                    CheckEqualInt(legacy.GetActiveTiles()[0].ClickHoldMs, TileConfig.DefaultClickHoldMs, "Legacy tile missing ClickHoldMs defaults to 30");
                    CheckEqualInt(legacy.GetActiveTiles()[1].ClickHoldMs, 200, "Legacy tile ClickHoldMs == 200 preserved");
                    CheckEqualInt(legacy.GetActiveTiles()[1].AnchorX, 2402, "Legacy tile AnchorX preserved");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                }
            }
        }

        private static void TestConfigStoreDefaultEnvAndCreateDefault()
        {
            AppConfig def = ConfigStore.CreateDefaultConfig();
            Check(def != null && def.Profiles != null && def.Profiles.Count == 1, "CreateDefaultConfig creates 1 profile", "invalid default profiles");
            if (def != null)
            {
                Check(def.AlwaysOnTop && !def.LockPosition && !def.LockSize, "CreateDefaultConfig sets AlwaysOnTop=true, LockPosition=false, LockSize=false", "flag mismatch");
                CheckEqualString(def.ActiveProfileId, "default", "CreateDefaultConfig sets ActiveProfileId == 'default'");
                List<TileConfig> tiles = def.GetActiveTiles();
                CheckEqualInt(tiles.Count, 4, "Default profile has 4 tiles");
                for (int i = 0; i < tiles.Count; i++)
                {
                    TileConfig t = tiles[i];
                    Check(!string.IsNullOrEmpty(t.Id), string.Format("Default tile[{0}] has Id", i), "empty Id");
                    CheckEqualInt(t.Size, 68, string.Format("Default tile[{0}] Size == 68", i));
                }
            }

            string prev = Environment.GetEnvironmentVariable("QUICKTILE_CONFIG");
            try
            {
                string customPath = Path.Combine(Path.GetTempPath(), "custom_quicktile_test.json");
                Environment.SetEnvironmentVariable("QUICKTILE_CONFIG", customPath);
                CheckEqualString(ConfigStore.DefaultConfigPath, customPath, "DefaultConfigPath honors QUICKTILE_CONFIG");

                Environment.SetEnvironmentVariable("QUICKTILE_CONFIG", "");
                string fallbackDefault = ConfigStore.DefaultConfigPath;
                Check(
                    !string.IsNullOrEmpty(fallbackDefault) && fallbackDefault.EndsWith("tiles.json", StringComparison.OrdinalIgnoreCase),
                    "DefaultConfigPath falls back to tiles.json when QUICKTILE_CONFIG empty",
                    string.Format("actual='{0}'", fallbackDefault));
            }
            finally
            {
                Environment.SetEnvironmentVariable("QUICKTILE_CONFIG", prev);
            }
        }

        private static void TestToVirtualDesktopAbsolute()
        {
            int ax;
            int ay;

            NativeMethods.ToVirtualDesktopAbsolute(0, 0, 0, 0, 2560, 1440, out ax, out ay);
            CheckEqualInt(ax, 0, "ToVirtualDesktopAbsolute primary origin X == 0");
            CheckEqualInt(ay, 0, "ToVirtualDesktopAbsolute primary origin Y == 0");

            NativeMethods.ToVirtualDesktopAbsolute(2559, 1439, 0, 0, 2560, 1440, out ax, out ay);
            CheckEqualInt(ax, 65535, "ToVirtualDesktopAbsolute primary max X == 65535");
            CheckEqualInt(ay, 65535, "ToVirtualDesktopAbsolute primary max Y == 65535");

            // Mixed negative + multi-monitor bounds
            NativeMethods.ToVirtualDesktopAbsolute(-1200, -727, -1200, -727, 7600, 2435, out ax, out ay);
            CheckEqualInt(ax, 0, "ToVirtualDesktopAbsolute multi-monitor negative min == (0,0) X");
            CheckEqualInt(ay, 0, "ToVirtualDesktopAbsolute multi-monitor negative min == (0,0) Y");

            NativeMethods.ToVirtualDesktopAbsolute(718, 1247, -1200, -727, 7600, 2435, out ax, out ay);
            Check(ax > 0 && ax < 65535 && ay > 0 && ay < 65535, "ToVirtualDesktopAbsolute center pixel within (0..65535)", string.Format("ax={0},ay={1}", ax, ay));

            NativeMethods.ToVirtualDesktopAbsolute(-2000, 99999, -1200, -727, 7600, 2435, out ax, out ay);
            CheckEqualInt(ax, 0, "ToVirtualDesktopAbsolute clamps negative overflow X to 0");
            CheckEqualInt(ay, 65535, "ToVirtualDesktopAbsolute clamps positive overflow Y to 65535");
        }

        private static void TestComputeMenuScreenPosition()
        {
            Rectangle primaryWa = new Rectangle(0, 0, 2560, 1392);
            Size menuSize = new Size(220, 260);

            // 1. Right edge of primary screen -> flips left inside primary monitor
            Point pRight = NativeMethods.ComputeMenuScreenPosition(new Point(2500, 689), menuSize, primaryWa);
            CheckEqualInt(pRight.X, 2280, "ComputeMenuScreenPosition right edge flips left X == 2280");
            CheckEqualInt(pRight.Y, 689, "ComputeMenuScreenPosition right edge preserves Y == 689");
            Check(pRight.X + menuSize.Width <= primaryWa.Right - 4, "ComputeMenuScreenPosition right edge stays inside monitor", string.Format("right={0}", pRight.X + menuSize.Width));

            // 2. Bottom-right corner -> flips left and up
            Point pBottomRight = NativeMethods.ComputeMenuScreenPosition(new Point(2500, 1350), menuSize, primaryWa);
            CheckEqualInt(pBottomRight.X, 2280, "ComputeMenuScreenPosition bottom-right flips left X == 2280");
            CheckEqualInt(pBottomRight.Y, 1090, "ComputeMenuScreenPosition bottom-right flips up Y == 1090");
            Check(pBottomRight.Y + menuSize.Height <= primaryWa.Bottom - 4, "ComputeMenuScreenPosition bottom-right stays inside monitor", string.Format("bottom={0}", pBottomRight.Y + menuSize.Height));

            // 3. Negative secondary monitor right edge -> flips left inside (-1200..0)
            Rectangle negWa = new Rectangle(-1200, -141, 1200, 1920);
            Point pNeg = NativeMethods.ComputeMenuScreenPosition(new Point(-20, 100), menuSize, negWa);
            CheckEqualInt(pNeg.X, -240, "ComputeMenuScreenPosition negative monitor flips left X == -240");
            CheckEqualInt(pNeg.Y, 100, "ComputeMenuScreenPosition negative monitor preserves Y == 100");
            Check(pNeg.X >= negWa.Left + 4 && pNeg.X + menuSize.Width <= negWa.Right - 4, "ComputeMenuScreenPosition negative monitor stays inside bounds", string.Format("x={0}", pNeg.X));

            // 4. Center click -> stays at click coordinates
            Point pCenter = NativeMethods.ComputeMenuScreenPosition(new Point(1000, 500), menuSize, primaryWa);
            CheckEqualInt(pCenter.X, 1000, "ComputeMenuScreenPosition center X == 1000");
            CheckEqualInt(pCenter.Y, 500, "ComputeMenuScreenPosition center Y == 500");
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== QuickTile SelfTest ===");

            TestInputStructSize();
            TestExtendedKeys();
            TestBuildHotkeyStagesAndEvents();
            TestBuildAnchorClickStages();
            TestTryParseHotkey();
            TestTileConfigMethods();
            TestToVirtualDesktopAbsolute();
            TestComputeMenuScreenPosition();
            TestConfigStoreDefaultEnvAndCreateDefault();
            TestConfigStoreRoundTripAndRecovery();

            Console.WriteLine(string.Format("=== Summary: Passed={0}, Failed={1} ===", _passed, _failed));
            return _failed == 0 ? 0 : 1;
        }
    }
}
