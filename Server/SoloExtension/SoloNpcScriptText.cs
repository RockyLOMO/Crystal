using System;
using System.IO;

namespace Server.SoloExtension;

// Only extension-owned NPC scripts use this migration; upstream script syntax is unchanged.
internal static class SoloNpcScriptText
{
    internal static string Normalize(string text) => text.Replace("\\\r\n", "\r\n").Replace("\\\n", "\n");

    internal static void Migrate(string path)
    {
        string before = File.ReadAllText(path);
        string after = Normalize(before);
        if (before == after) return;
        string backup = path + ".before-newline-fix.bak";
        if (!File.Exists(backup)) File.Copy(path, backup);
        File.WriteAllText(path, after);
    }
}
