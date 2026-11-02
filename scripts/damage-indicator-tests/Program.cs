using System.Drawing;
using System.Globalization;
using Client.Utils;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
int passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}

foreach (int amount in new[] { 3, 4, 6 })
{
    Check(DamageIndicatorStyle.Format(amount) == $"+{amount}" &&
        DamageIndicatorStyle.GetColour(amount, true) == Color.LimeGreen,
        $"restored {amount} HP displays green +{amount}");
}
Check(DamageIndicatorStyle.Format(-3) == "-3" &&
    DamageIndicatorStyle.GetColour(-3, true) == Color.Red,
    "lost player HP retains red negative value");
Check(DamageIndicatorStyle.Format(0) == "0" &&
    DamageIndicatorStyle.GetColour(0, true) == Color.Red,
    "zero retains its original player presentation");
Check(DamageIndicatorStyle.GetColour(-3, false) == Color.White &&
    DamageIndicatorStyle.GetColour(0, false) == Color.White,
    "non-player damage and zero retain white");
Check(DamageIndicatorStyle.GetColour(3, false) == Color.LimeGreen,
    "non-player recovery also clearly indicates healing");
Check(DamageIndicatorStyle.Format(1234) == "+1,234" &&
    DamageIndicatorStyle.Format(-1234) == "-1,234",
    "grouped signed values retain separators");
Check(DamageIndicatorStyle.Format(int.MinValue) == "-2,147,483,648" &&
    DamageIndicatorStyle.Format(int.MaxValue) == "+2,147,483,647",
    "extreme signed values do not overflow");
Console.WriteLine($"PASS: {passed} damage indicator checks");
