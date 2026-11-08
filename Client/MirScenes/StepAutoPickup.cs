using Client.MirNetwork;
using Client.MirObjects;

namespace Client.MirScenes;

// The server remains authoritative for ownership, weight and inventory checks.
internal static class StepAutoPickup
{
    internal static void Process(MapControl map)
    {
        var user = MapControl.User;
        if (!Settings.AutoPickUp || user == null || user.Dead || CMain.Time <= GameScene.PickUpTime) return;
        Point p = user.CurrentLocation;
        var cells = map.M2CellInfo;
        if (cells == null || p.X < 0 || p.Y < 0 || p.X >= cells.GetLength(0) || p.Y >= cells.GetLength(1)
            || !map.ValidPoint(p)) return;
        var objects = cells[p.X, p.Y]?.CellObjects;
        if (objects == null) return;
        bool space = user.FreeSpace(user.Inventory) > 0;
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] is not ItemObject item || (!space && item.DrawFrame is not (>= 112 and <= 116))) continue;
            GameScene.PickUpTime = CMain.Time + 250;
            Network.Enqueue(new ClientPackets.PickUp());
            break;
        }
    }
}
