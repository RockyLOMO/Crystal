using System.Drawing;

namespace Client.Utils
{
    public static class DamageIndicatorStyle
    {
        // The server sends signed HP changes: positive values restore health.
        public static string Format(int amount) => amount.ToString("+#,##0;-#,##0;0");

        public static Color GetColour(int amount, bool isPlayer) =>
            amount > 0 ? Color.LimeGreen : isPlayer ? Color.Red : Color.White;
    }
}
