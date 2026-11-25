using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;

namespace QuickTile
{
    public enum ActionType
    {
        Hotkey = 0,
        AnchorClick = 1
    }

    public enum MouseClickType
    {
        LeftClick = 0,
        DoubleClick = 1,
        RightClick = 2
    }

    [DataContract]
    public class TileConfig
    {
        public const int DefaultClickHoldMs = 30;
        public const int MinClickHoldMs = 0;
        public const int MaxClickHoldMs = 30000;

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Label { get; set; }

        [DataMember]
        public int X { get; set; }

        [DataMember]
        public int Y { get; set; }

        [DataMember]
        public int Size { get; set; }

        [DataMember]
        public string BackColorHex { get; set; }

        [DataMember]
        public string AccentColorHex { get; set; }

        [DataMember]
        public string TextColorHex { get; set; }

        [DataMember]
        public double Opacity { get; set; }

        [DataMember]
        public ActionType Action { get; set; }

        [DataMember]
        public bool ModCtrl { get; set; }

        [DataMember]
        public bool ModAlt { get; set; }

        [DataMember]
        public bool ModShift { get; set; }

        [DataMember]
        public bool ModWin { get; set; }

        [DataMember]
        public bool ModLCtrl { get; set; }

        [DataMember]
        public bool ModRCtrl { get; set; }

        [DataMember]
        public bool ModLAlt { get; set; }

        [DataMember]
        public bool ModRAlt { get; set; }

        [DataMember]
        public bool ModLShift { get; set; }

        [DataMember]
        public bool ModRShift { get; set; }

        [DataMember]
        public bool ModLWin { get; set; }

        [DataMember]
        public bool ModRWin { get; set; }

        [DataMember]
        public int VirtualKey { get; set; }

        [DataMember]
        public int AnchorX { get; set; }

        [DataMember]
        public int AnchorY { get; set; }

        [DataMember]
        public MouseClickType ClickType { get; set; }

        [DataMember]
        public bool RestoreCursor { get; set; }

        [DataMember]
        public int ClickHoldMs { get; set; }

        public TileConfig()
        {
            InitDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            TilePaletteItem preset = TilePalettes.Presets[0];
            Id = Guid.NewGuid().ToString("N");
            Label = "新方块";
            X = 100;
            Y = 100;
            Size = 68;
            BackColorHex = preset.BackHex;
            AccentColorHex = preset.AccentHex;
            TextColorHex = preset.TextHex;
            Opacity = 0.92;
            Action = ActionType.Hotkey;
            ModCtrl = false;
            ModAlt = false;
            ModShift = false;
            ModWin = false;
            ModLCtrl = false;
            ModRCtrl = false;
            ModLAlt = false;
            ModRAlt = false;
            ModLShift = false;
            ModRShift = false;
            ModLWin = false;
            ModRWin = false;
            VirtualKey = 0;
            AnchorX = 0;
            AnchorY = 0;
            ClickType = MouseClickType.LeftClick;
            RestoreCursor = true;
            ClickHoldMs = DefaultClickHoldMs;
        }

        public void NormalizeModifiers()
        {
            if (ModCtrl && !ModLCtrl && !ModRCtrl)
            {
                ModLCtrl = true;
            }
            if (ModAlt && !ModLAlt && !ModRAlt)
            {
                ModLAlt = true;
            }
            if (ModShift && !ModLShift && !ModRShift)
            {
                ModLShift = true;
            }
            if (ModWin && !ModLWin && !ModRWin)
            {
                ModLWin = true;
            }
        }

        public void SyncLegacyModifiers()
        {
            ModCtrl = ModLCtrl || ModRCtrl;
            ModAlt = ModLAlt || ModRAlt;
            ModShift = ModLShift || ModRShift;
            ModWin = ModLWin || ModRWin;
        }

        public bool HasAnyHotkey()
        {
            return VirtualKey > 0 ||
                ModLCtrl || ModRCtrl || ModCtrl ||
                ModLAlt || ModRAlt || ModAlt ||
                ModLShift || ModRShift || ModShift ||
                ModLWin || ModRWin || ModWin;
        }

        public TileConfig Clone()
        {
            TileConfig copy = new TileConfig();
            copy.Id = Id;
            copy.Label = Label;
            copy.X = X;
            copy.Y = Y;
            copy.Size = Size;
            copy.BackColorHex = BackColorHex;
            copy.AccentColorHex = AccentColorHex;
            copy.TextColorHex = TextColorHex;
            copy.Opacity = Opacity;
            copy.Action = Action;
            copy.ModCtrl = ModCtrl;
            copy.ModAlt = ModAlt;
            copy.ModShift = ModShift;
            copy.ModWin = ModWin;
            copy.ModLCtrl = ModLCtrl;
            copy.ModRCtrl = ModRCtrl;
            copy.ModLAlt = ModLAlt;
            copy.ModRAlt = ModRAlt;
            copy.ModLShift = ModLShift;
            copy.ModRShift = ModRShift;
            copy.ModLWin = ModLWin;
            copy.ModRWin = ModRWin;
            copy.VirtualKey = VirtualKey;
            copy.AnchorX = AnchorX;
            copy.AnchorY = AnchorY;
            copy.ClickType = ClickType;
            copy.RestoreCursor = RestoreCursor;
            copy.ClickHoldMs = ClickHoldMs;
            return copy;
        }

        public string GetSubtitle()
        {
            if (Action == ActionType.AnchorClick)
            {
                string prefix = "左键";
                if (ClickType == MouseClickType.DoubleClick)
                {
                    prefix = "双击";
                }
                else if (ClickType == MouseClickType.RightClick)
                {
                    prefix = "右键";
                }
                string baseText = string.Format("{0} {1},{2}", prefix, AnchorX, AnchorY);
                int holdMs = ClampClickHoldMs(ClickHoldMs);
                if (holdMs > 0 && holdMs != DefaultClickHoldMs)
                {
                    if (holdMs >= 1000 && (holdMs % 1000) == 0)
                    {
                        return string.Format("{0} ({1}s)", baseText, holdMs / 1000);
                    }
                    return string.Format("{0} ({1}ms)", baseText, holdMs);
                }
                return baseText;
            }

            bool effLCtrl = ModLCtrl || (ModCtrl && !ModRCtrl);
            bool effLAlt = ModLAlt || (ModAlt && !ModRAlt);
            bool effLShift = ModLShift || (ModShift && !ModRShift);
            bool effLWin = ModLWin || (ModWin && !ModRWin);
            return NativeMethods.FormatHotkey(
                effLCtrl, ModRCtrl,
                effLAlt, ModRAlt,
                effLShift, ModRShift,
                effLWin, ModRWin,
                VirtualKey);
        }

        public static int ClampSize(int size)
        {
            if (size < 28)
            {
                return 28;
            }
            if (size > 320)
            {
                return 320;
            }
            return size;
        }

        public static int ClampClickHoldMs(int holdMs)
        {
            if (holdMs < MinClickHoldMs)
            {
                return MinClickHoldMs;
            }
            if (holdMs > MaxClickHoldMs)
            {
                return MaxClickHoldMs;
            }
            return holdMs;
        }

        public static double ClampOpacity(double opacity)
        {
            if (double.IsNaN(opacity) || double.IsInfinity(opacity))
            {
                return 0.92;
            }
            if (opacity < 0.25)
            {
                return 0.25;
            }
            if (opacity > 1.0)
            {
                return 1.0;
            }
            return opacity;
        }
    }

    [DataContract]
    public class TileProfile
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public List<TileConfig> Tiles { get; set; }

        public TileProfile()
        {
            InitDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "默认方案";
            Tiles = new List<TileConfig>();
        }
    }

    [DataContract]
    public class AppConfig
    {
        [DataMember]
        public bool AlwaysOnTop { get; set; }

        [DataMember]
        public bool LockPosition { get; set; }

        [DataMember]
        public bool LockSize { get; set; }

        [DataMember]
        public string ActiveProfileId { get; set; }

        [DataMember]
        public List<TileProfile> Profiles { get; set; }

        // Legacy field kept solely for deserializing older single-list configs; cleared after migration.
        [DataMember(EmitDefaultValue = false)]
        public List<TileConfig> Tiles { get; set; }

        public AppConfig()
        {
            InitDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            AlwaysOnTop = true;
            LockPosition = false;
            LockSize = false;
            ActiveProfileId = string.Empty;
            Profiles = new List<TileProfile>();
            Tiles = null;
        }

        public TileProfile GetActiveProfile()
        {
            if (Profiles == null || Profiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(ActiveProfileId))
            {
                for (int i = 0; i < Profiles.Count; i++)
                {
                    if (Profiles[i] != null && string.Equals(Profiles[i].Id, ActiveProfileId, StringComparison.Ordinal))
                    {
                        return Profiles[i];
                    }
                }
            }

            return Profiles[0];
        }

        public List<TileConfig> GetActiveTiles()
        {
            TileProfile active = GetActiveProfile();
            if (active != null && active.Tiles != null)
            {
                return active.Tiles;
            }
            return new List<TileConfig>();
        }
    }

    public class TilePaletteItem
    {
        public string Name { get; set; }
        public string BackHex { get; set; }
        public string AccentHex { get; set; }
        public string TextHex { get; set; }

        public TilePaletteItem()
        {
            Name = string.Empty;
            BackHex = "#1E222A";
            AccentHex = "#4C9AFF";
            TextHex = "#F5F7FA";
        }

        public TilePaletteItem(string name, string backHex, string accentHex, string textHex)
        {
            Name = name;
            BackHex = backHex;
            AccentHex = accentHex;
            TextHex = textHex;
        }
    }

    public static class TilePalettes
    {
        public static readonly TilePaletteItem[] Presets = new TilePaletteItem[]
        {
            new TilePaletteItem("深海蓝", "#1E222A", "#4C9AFF", "#F5F7FA"),
            new TilePaletteItem("翡翠绿", "#1B2623", "#34D399", "#F0FDF4"),
            new TilePaletteItem("琥珀金", "#26211A", "#F59E0B", "#FFFBEB"),
            new TilePaletteItem("珊瑚红", "#281C20", "#F87171", "#FEF2F2"),
            new TilePaletteItem("星云紫", "#221D2B", "#A78BFA", "#F5F3FF"),
            new TilePaletteItem("极光青", "#182429", "#22D3EE", "#ECFEFF"),
            new TilePaletteItem("石墨灰", "#232529", "#9CA3AF", "#F9FAFB"),
            new TilePaletteItem("玫瑰粉", "#291C26", "#F472B6", "#FDF2F8")
        };

        public static Color FromHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            string clean = hex.Trim();
            if (clean.StartsWith("#"))
            {
                clean = clean.Substring(1);
            }

            try
            {
                if (clean.Length == 6)
                {
                    int rgb = int.Parse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int r = (rgb >> 16) & 0xFF;
                    int g = (rgb >> 8) & 0xFF;
                    int b = rgb & 0xFF;
                    return Color.FromArgb(255, r, g, b);
                }
                if (clean.Length == 8)
                {
                    uint argb = uint.Parse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int a = (int)((argb >> 24) & 0xFF);
                    int r = (int)((argb >> 16) & 0xFF);
                    int g = (int)((argb >> 8) & 0xFF);
                    int b = (int)(argb & 0xFF);
                    return Color.FromArgb(a, r, g, b);
                }
                if (clean.Length == 3)
                {
                    int r = int.Parse(clean.Substring(0, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17;
                    int g = int.Parse(clean.Substring(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17;
                    int b = int.Parse(clean.Substring(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17;
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                return fallback;
            }

            return fallback;
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}
