using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.SoloExtension
{
    public sealed class SoloPlayerProfile
    {
        private static readonly ConcurrentDictionary<string, SoloPlayerProfile> Cache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public string CharacterName { get; set; } = string.Empty;

        // 【玛法行军帐】扎营坐标记录
        public string CampMapFileName { get; set; } = string.Empty;
        public string CampMapTitle { get; set; } = string.Empty;
        public int CampX { get; set; }
        public int CampY { get; set; }
        public string CampUpdatedAt { get; set; } = string.Empty;
        public ushort CampStyleImage { get; set; } = 55; // 默认 55 (圣火营火盆·动态火焰)

        // 【玛法猎魔手札】4x4 范围自动拾取模式：0 = 关闭，1 = 全部拾取，2 = 仅拾取金币/药水/材料与精良以上极品（默认 2）
        public int AutoPickRangeMode { get; set; } = 2;

        // 【设置】4x4 自动拾取严格负重保护开关（默认开启，接近包袱负重上限时自动停止捡装备）
        public bool AutoPickWeightProtect { get; set; } = true;

        // 【玛法猎魔手札】怪物图鉴累计击杀数 (Key: 怪物种族分组标识, Value: 累计击杀数)
        public Dictionary<string, int> MonsterKills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // 是否已发放随身奇物（【玛法行军帐】与【玛法猎魔手札】）
        public bool StarterItemsGranted { get; set; }

        [JsonIgnore]
        public long LastSavedTick { get; set; }

        [JsonIgnore]
        public bool IsDirty { get; set; }

        [JsonIgnore]
        public int LastSyncedCodexScore { get; set; } = -1;

        public static string GetSoloDataDirectory()
        {
            string dir = Path.Combine(Settings.EnvirPath, "SoloData");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetProfilePath(string characterName)
        {
            string safeName = SanitizeFileName(characterName);
            return Path.Combine(GetSoloDataDirectory(), $"Solo_{safeName}.json");
        }

        public static SoloPlayerProfile GetOrLoad(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return new SoloPlayerProfile();

            return Cache.GetOrAdd(characterName, LoadFromDisk);
        }

        private static SoloPlayerProfile LoadFromDisk(string characterName)
        {
            try
            {
                string path = GetProfilePath(characterName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<SoloPlayerProfile>(json, JsonOptions);
                    if (loaded != null)
                    {
                        loaded.CharacterName = characterName;
                        loaded.MonsterKills ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.GetLogger(LogType.Server)?.Error($"[SoloExtension] 加载角色旁路存档失败 ({characterName}): {ex.Message}");
            }

            return new SoloPlayerProfile
            {
                CharacterName = characterName,
                AutoPickRangeMode = 2
            };
        }

        public void Save(bool force = true)
        {
            if (string.IsNullOrWhiteSpace(CharacterName)) return;
            if (!force && !IsDirty) return;

            try
            {
                string path = GetProfilePath(CharacterName);
                string dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tempPath = path + ".tmp";
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
                IsDirty = false;
                LastSavedTick = MirEnvir.Envir.Main?.Time ?? 0;
            }
            catch (Exception ex)
            {
                Logger.GetLogger(LogType.Server)?.Error($"[SoloExtension] 保存角色旁路存档失败 ({CharacterName}): {ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
