# QuickTile

[English](README.md) | [简体中文](README.zh-CN.md)

An ultra-lightweight, zero-dependency Windows desktop floating macro tile utility. Keep customizable square shortcut tiles on top of your screen and trigger keyboard shortcuts or anchored screen clicks **without stealing focus** from the active window.

## Features

- **Focus-Safe Triggering**: Uses `WS_EX_NOACTIVATE` and `WM_MOUSEACTIVATE (MA_NOACTIVATE)` so clicking any tile sends its action without interrupting focus in your current app.
- **Minimalist Floating UI**: Starts silently with your active tiles on screen. Right-click any tile (or click the system tray icon) to open **Settings**. Both the tile context menu and tray menu stay minimal with 4 items: `设置` (Settings), `锁定大小` (Lock Size), `锁定位置` (Lock Position), and `退出程序` (Exit).
- **Multi-Profile Support**: Create, rename, switch, and delete tile profiles directly from the top bar in Settings. Automatically enforces at least 1 profile and at least 1 tile per profile.
- **Free Positioning & Independent Locks (`28–320 px`)**:
  - Drag tiles anywhere across single or multi-monitor setups (including negative coordinates);
  - Resize `1:1` from the bottom-right corner or fine-tune by `4 px` with the mouse wheel;
  - `PerMonitorV2` DPI-aware with `WM_DPICHANGED` guarding to prevent scaling jumps across mixed-DPI monitors;
  - Independent **Lock Position** and **Lock Size** toggles, plus **Always On Top**.
- **Left/Right Modifier-Aware Hotkey Engine**:
  - Distinguishes `LCtrl` / `RCtrl`, `LAlt` / `RAlt`, `LShift` / `RShift`, and `LWin` / `RWin`;
  - Supports low-level keyboard hook (`WH_KEYBOARD_LL`) recording, manual hotkey text input (e.g., `LAlt+Y`, `RCtrl+Shift+S`), and individual modifier checkboxes;
  - Staged hardware `SendInput` injection with hardware scan codes and extended-key flags.
- **Anchored Screen Clicks**:
  - Multi-monitor translucent crosshair picker for physical pixel coordinates;
  - Emits a virtual-desktop hardware move (`MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`) before clicking;
  - Supports Left Click, Double Click, Right Click, configurable hold duration (`0–30000 ms`), and optional cursor position restoration.
- **Single-File Lightweight Build**: Pure .NET Framework 4.x WinForms + Win32 P/Invoke (`dist\QuickTile.exe`, ~91.5 KB) with zero external dependencies.

## Build

Requires Windows built-in .NET Framework 4.x `csc.exe` (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) and PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

The compiled executable is output to `dist\QuickTile.exe`.

## Configuration

- **Default path**: `tiles.json` in the same directory as `QuickTile.exe` (can be overridden via the `QUICKTILE_CONFIG` environment variable).
- Uses an atomic multi-profile JSON format and automatically migrates legacy single-list `Tiles` configs into the default profile (`默认方案`).

## License

[MIT](LICENSE)
