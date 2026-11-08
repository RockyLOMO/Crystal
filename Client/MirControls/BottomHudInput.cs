using Client.MirScenes;

namespace Client.MirControls;

/// <summary>Continue a map-origin right-button gesture through bottom HUD controls.</summary>
public static class BottomHudInput
{
    public static bool CanContinue(GameScene scene, MirControl hovered, MouseButtons mapButtons)
    {
        if (scene == null || mapButtons != MouseButtons.Right ||
            (MirControl.ActiveControl != null && MirControl.ActiveControl != scene.MapControl)) return false;
        // A held map gesture retains ActiveControl. Highlight() then clears MouseControl
        // when entering another control, so resolve the topmost actual hit in that case.
        hovered ??= Hit(scene, CMain.MPoint);
        if (hovered.IsDisposed || !hovered.Visible) return false;
        for (var control = hovered; control != null && control != scene; control = control.Parent)
            if (control == scene.MainDialog || control == scene.ChatDialog || control == scene.ChatControl ||
                control == scene.BeltDialog || control == scene.HeroBeltDialog || Classic176HUDLayout.IsBottomExtension(control))
                return true;
        return false;
    }

    private static MirControl Hit(MirControl parent, Point point)
    {
        for (int i = parent.Controls.Count - 1; i >= 0; i--)
            if (parent.Controls[i].IsMouseOver(point)) return Hit(parent.Controls[i], point);
        return parent;
    }
}
