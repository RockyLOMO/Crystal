using Server.MirDatabase;

namespace Server.SoloExtension;

/// <summary>Double dedicated quest-item probability without changing ordinary loot.</summary>
public static class QuestItemDropRate
{
    public static bool Succeeds(DropInfo drop, int rate, int roll)
    {
        // Keep the existing effective denominator (including global/owner bonuses).
        // Two winning rolls double 1/rate exactly, even for odd denominators.
        bool questItem = drop.Item != null && drop.Gold == 0 && drop.GroupedDrop == null &&
            (drop.QuestRequired || drop.Item.Type == ItemType.Quest);
        return roll >= 0 && roll < Math.Min(rate, questItem ? 2 : 1);
    }
}
