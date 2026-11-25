using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace QuickTile
{
    public static class ConfigStore
    {
        public static string DefaultConfigPath
        {
            get
            {
                string envPath = Environment.GetEnvironmentVariable("QUICKTILE_CONFIG");
                if (!string.IsNullOrWhiteSpace(envPath))
                {
                    return envPath.Trim();
                }
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiles.json");
            }
        }

        public static TileConfig CreateSingleFallbackTile()
        {
            Rectangle wa = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1920, 1080);

            int size = 68;
            int x = wa.Right - size - 24;
            int y = wa.Top + Math.Max(20, (wa.Height - size) / 2);

            return CreateHotkeyTile(
                "方块 1",
                x,
                y,
                size,
                TilePalettes.Presets[0],
                true, false, false, false,
                (int)Keys.C);
        }

        public static AppConfig CreateDefaultConfig()
        {
            AppConfig config = new AppConfig();
            config.AlwaysOnTop = true;
            config.LockPosition = false;
            config.LockSize = false;
            config.ActiveProfileId = "default";
            config.Tiles = null;

            TileProfile defProfile = new TileProfile();
            defProfile.Id = "default";
            defProfile.Name = "默认方案";

            Rectangle wa = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1920, 1080);

            int size = 68;
            int gap = 10;
            int x = wa.Right - size - 24;
            int totalHeight = 4 * size + 3 * gap;
            int startY = wa.Top + Math.Max(20, (wa.Height - totalHeight) / 2);

            defProfile.Tiles.Add(CreateHotkeyTile(
                "复制",
                x,
                startY + 0 * (size + gap),
                size,
                TilePalettes.Presets[0],
                true, false, false, false,
                (int)Keys.C));

            defProfile.Tiles.Add(CreateHotkeyTile(
                "粘贴",
                x,
                startY + 1 * (size + gap),
                size,
                TilePalettes.Presets[1],
                true, false, false, false,
                (int)Keys.V));

            defProfile.Tiles.Add(CreateHotkeyTile(
                "撤销",
                x,
                startY + 2 * (size + gap),
                size,
                TilePalettes.Presets[2],
                true, false, false, false,
                (int)Keys.Z));

            defProfile.Tiles.Add(CreateHotkeyTile(
                "截图",
                x,
                startY + 3 * (size + gap),
                size,
                TilePalettes.Presets[4],
                false, false, true, true,
                (int)Keys.S));

            config.Profiles.Add(defProfile);
            return config;
        }

        private static TileConfig CreateHotkeyTile(
            string label,
            int x,
            int y,
            int size,
            TilePaletteItem palette,
            bool ctrl,
            bool alt,
            bool shift,
            bool win,
            int vk)
        {
            TileConfig tile = new TileConfig();
            tile.Label = label;
            tile.X = x;
            tile.Y = y;
            tile.Size = TileConfig.ClampSize(size);
            tile.BackColorHex = palette.BackHex;
            tile.AccentColorHex = palette.AccentHex;
            tile.TextColorHex = palette.TextHex;
            tile.Opacity = 0.92;
            tile.Action = ActionType.Hotkey;
            tile.ModLCtrl = ctrl;
            tile.ModLAlt = alt;
            tile.ModLShift = shift;
            tile.ModLWin = win;
            tile.SyncLegacyModifiers();
            tile.VirtualKey = vk;
            return tile;
        }

        public static AppConfig Load(string path = null)
        {
            string target = string.IsNullOrWhiteSpace(path) ? DefaultConfigPath : path;

            try
            {
                if (!File.Exists(target))
                {
                    return CreateDefaultConfig();
                }

                string json = File.ReadAllText(target, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefaultConfig();
                }

                byte[] bytes = Encoding.UTF8.GetBytes(json);
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
                AppConfig loaded;
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    loaded = serializer.ReadObject(ms) as AppConfig;
                }

                if (loaded == null)
                {
                    return CreateDefaultConfig();
                }

                NormalizeConfig(loaded);
                return loaded;
            }
            catch
            {
                return CreateDefaultConfig();
            }
        }

        public static void Save(AppConfig config, string path = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            NormalizeConfig(config);

            string target = string.IsNullOrWhiteSpace(path) ? DefaultConfigPath : path;
            string fullPath = Path.GetFullPath(target);
            string dir = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = Path.Combine(
                string.IsNullOrEmpty(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir,
                string.Format("{0}.{1}.tmp", Path.GetFileName(fullPath), Guid.NewGuid().ToString("N")));

            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(fs, new UTF8Encoding(false), true, true, "  "))
                {
                    serializer.WriteObject(writer, config);
                    writer.Flush();
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Replace(tempPath, fullPath, null, true);
                    }
                    catch
                    {
                        File.Copy(tempPath, fullPath, true);
                        File.Delete(tempPath);
                    }
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void NormalizeConfig(AppConfig config)
        {
            if (config.Profiles == null)
            {
                config.Profiles = new List<TileProfile>();
            }

            if (config.Profiles.Count == 0)
            {
                TileProfile migrated = new TileProfile();
                migrated.Id = "default";
                migrated.Name = "默认方案";

                if (config.Tiles != null && config.Tiles.Count > 0)
                {
                    migrated.Tiles = config.Tiles;
                }
                else
                {
                    AppConfig defaults = CreateDefaultConfig();
                    migrated.Tiles = defaults.Profiles[0].Tiles;
                }
                config.Profiles.Add(migrated);
            }

            // Always clear legacy top-level Tiles after migration so only Profiles are persisted.
            config.Tiles = null;

            TilePaletteItem fallbackPalette = TilePalettes.Presets[0];
            List<TileProfile> cleanedProfiles = new List<TileProfile>(config.Profiles.Count);

            for (int p = 0; p < config.Profiles.Count; p++)
            {
                TileProfile profile = config.Profiles[p];
                if (profile == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString("N");
                }
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    profile.Name = string.Format("方案 {0}", cleanedProfiles.Count + 1);
                }
                if (profile.Tiles == null)
                {
                    profile.Tiles = new List<TileConfig>();
                }

                List<TileConfig> cleanedTiles = new List<TileConfig>(profile.Tiles.Count);
                for (int i = 0; i < profile.Tiles.Count; i++)
                {
                    TileConfig tile = profile.Tiles[i];
                    if (tile == null)
                    {
                        continue;
                    }
                    NormalizeTile(tile, fallbackPalette);
                    cleanedTiles.Add(tile);
                }

                if (cleanedTiles.Count == 0)
                {
                    cleanedTiles.Add(CreateSingleFallbackTile());
                }

                profile.Tiles = cleanedTiles;
                cleanedProfiles.Add(profile);
            }

            if (cleanedProfiles.Count == 0)
            {
                TileProfile fallbackProfile = new TileProfile();
                fallbackProfile.Id = "default";
                fallbackProfile.Name = "默认方案";
                fallbackProfile.Tiles.Add(CreateSingleFallbackTile());
                cleanedProfiles.Add(fallbackProfile);
            }

            config.Profiles = cleanedProfiles;

            bool activeFound = false;
            if (!string.IsNullOrEmpty(config.ActiveProfileId))
            {
                for (int i = 0; i < config.Profiles.Count; i++)
                {
                    if (string.Equals(config.Profiles[i].Id, config.ActiveProfileId, StringComparison.Ordinal))
                    {
                        activeFound = true;
                        break;
                    }
                }
            }
            if (!activeFound)
            {
                config.ActiveProfileId = config.Profiles[0].Id;
            }
        }

        private static void NormalizeTile(TileConfig tile, TilePaletteItem fallbackPalette)
        {
            if (string.IsNullOrWhiteSpace(tile.Id))
            {
                tile.Id = Guid.NewGuid().ToString("N");
            }
            if (tile.Label == null)
            {
                tile.Label = "方块";
            }

            tile.Size = TileConfig.ClampSize(tile.Size);
            tile.Opacity = TileConfig.ClampOpacity(tile.Opacity);
            tile.ClickHoldMs = TileConfig.ClampClickHoldMs(tile.ClickHoldMs);
            tile.NormalizeModifiers();
            tile.SyncLegacyModifiers();

            if (string.IsNullOrWhiteSpace(tile.BackColorHex))
            {
                tile.BackColorHex = fallbackPalette.BackHex;
            }
            if (string.IsNullOrWhiteSpace(tile.AccentColorHex))
            {
                tile.AccentColorHex = fallbackPalette.AccentHex;
            }
            if (string.IsNullOrWhiteSpace(tile.TextColorHex))
            {
                tile.TextColorHex = fallbackPalette.TextHex;
            }
        }
    }
}
