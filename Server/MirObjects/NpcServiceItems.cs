namespace Server.MirObjects;

/// <summary>Item lookup extensions for the existing, server-validated NPC services.</summary>
public static class NpcServiceItems
{
    public static UserItem FindEquippedRepairItem(PlayerObject player, ulong uniqueID)
    {
        if (uniqueID == 0 || player.Info?.Equipment == null) return null;
        foreach (var item in player.Info.Equipment)
            if (item?.UniqueID == uniqueID && item.Info != null && item.MaxDura > 0)
                return item;
        return null;
    }
}
