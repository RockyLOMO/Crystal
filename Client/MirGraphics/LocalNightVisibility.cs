namespace Client.MirGraphics;

/// <summary>Three extra light radius steps for the local player at night.</summary>
public static class LocalNightVisibility
{
    public static int Range(int range, int count, bool localPlayer, LightSetting setting, bool blind)
        => Math.Clamp(range + (localPlayer && setting == LightSetting.Night && !blind ? 3 : 0), 0, Math.Max(0, count - 1));
}
