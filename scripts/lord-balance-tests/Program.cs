using Server.MirDatabase;
using Server.MirObjects;
using Server.SoloExtension;

// Directory.Build.targets includes TestWorkspace's ModuleInitializer before the
// real server types initialize. No maps, accounts, connections or server start.
int passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}

var info = new MonsterInfo { Name = "LordBalanceFixture", MoveSpeed = 1000, AttackSpeed = 1000, Experience = 100 };
info.Stats[Stat.HP] = 100;
info.Stats[Stat.MinDC] = 100; info.Stats[Stat.MaxDC] = 200;
info.Stats[Stat.MinMC] = 50; info.Stats[Stat.MaxMC] = 100;
info.Stats[Stat.MinSC] = 10; info.Stats[Stat.MaxSC] = 20;
info.Stats[Stat.MinAC] = 12; info.Stats[Stat.MaxAC] = 24;
info.Stats[Stat.MinMAC] = 16; info.Stats[Stat.MaxMAC] = 32;

var lord = new FixtureMonster(info) { Rarity = MonsterRarity.Lord, EliteTier = 2 };
lord.RefreshAll();
Check(lord.Stats[Stat.MinDC] == 204 && lord.Stats[Stat.MaxDC] == 468,
    "real RefreshAll applies lord physical attack multipliers 2.04 and 2.34");
Check(lord.Stats[Stat.MinDC] == Math.Round(170 * 1.20) && lord.Stats[Stat.MaxDC] == Math.Round(390 * 1.20),
    "lord physical attack is twenty percent higher than the previous 1.70/1.95 multipliers");
Check(lord.Stats[Stat.MinMC] == 60 && lord.Stats[Stat.MaxMC] == 120,
    "lord minimum and maximum magic attack increase by twenty percent");
Check(lord.MaxHealth == 900 && lord.Stats[Stat.MinAC] == 12 && lord.Stats[Stat.MaxAC] == 24 &&
      lord.Stats[Stat.MinMAC] == 16 && lord.Stats[Stat.MaxMAC] == 32 &&
      lord.Stats[Stat.MinSC] == 10 && lord.Stats[Stat.MaxSC] == 20,
    "lord health, physical/magic defense and spirit attack retain their previous values");
Check(lord.MoveSpeed == 800 && lord.AttackSpeed == 800 && lord.CoolEye,
    "lord movement, attack interval and invisibility detection are unchanged");
Check(lord.Experience == 1500 && EcologyAndRarityModule.GetExpMultiplier(lord) == 15.0 &&
      EcologyAndRarityModule.GetGoldMultiplier(lord) == 3.5 &&
      EcologyAndRarityModule.GetDropBonusPercent(lord) == 100 && EcologyAndRarityModule.GetDropLoops(lord) == 4,
    "lord experience, gold and drop multipliers are unchanged");

var initial = Snapshot(lord);
for (int refresh = 1; refresh <= 3; refresh++)
{
    lord.RefreshAll();
    Check(Snapshot(lord).SequenceEqual(initial),
        $"real RefreshAll repeat {refresh} rebuilds lord stats without multiplying bonuses again");
}
Check(info.Stats[Stat.HP] == 100 && info.Stats[Stat.MinDC] == 100 && info.Stats[Stat.MaxDC] == 200 &&
      info.Stats[Stat.MinMC] == 50 && info.Stats[Stat.MaxMC] == 100 && info.AttackSpeed == 1000 && info.MoveSpeed == 1000,
    "refreshing a lord leaves its shared monster definition unchanged");

// Expected values are the existing non-lord rules, not a duplicate of the new
// implementation. Exercise the upstream RefreshAll entry and actual subclass.
foreach (var row in new[]
{
    (Rarity: MonsterRarity.None, Subtype: -1, HP: 100, MinDC: 100, MaxDC: 200, MinAC: 12, MaxAC: 24, Attack: 1000, Move: 1000),
    (Rarity: MonsterRarity.Uncommon, Subtype: -1, HP: 200, MinDC: 100, MaxDC: 240, MinAC: 12, MaxAC: 24, Attack: 1000, Move: 1000),
    (Rarity: MonsterRarity.Rare, Subtype: 0, HP: 350, MinDC: 100, MaxDC: 260, MinAC: 12, MaxAC: 24, Attack: 1000, Move: 1000),
    (Rarity: MonsterRarity.Rare, Subtype: 1, HP: 350, MinDC: 100, MaxDC: 260, MinAC: 16, MaxAC: 32, Attack: 1000, Move: 1000),
    (Rarity: MonsterRarity.Rare, Subtype: 2, HP: 350, MinDC: 100, MaxDC: 300, MinAC: 12, MaxAC: 24, Attack: 1000, Move: 1000),
    (Rarity: MonsterRarity.Elite, Subtype: -1, HP: 550, MinDC: 140, MaxDC: 320, MinAC: 12, MaxAC: 24, Attack: 860, Move: 880)
})
{
    var monster = new FixtureMonster(info) { Rarity = row.Rarity, EliteSubType = row.Subtype,
        EliteTier = row.Rarity == MonsterRarity.Elite ? (byte)2 : row.Rarity == MonsterRarity.Rare ? (byte)1 : (byte)0 };
    monster.RefreshAll();
    Check(monster.MaxHealth == row.HP && monster.Stats[Stat.MinDC] == row.MinDC && monster.Stats[Stat.MaxDC] == row.MaxDC &&
          monster.Stats[Stat.MinAC] == row.MinAC && monster.Stats[Stat.MaxAC] == row.MaxAC &&
          monster.AttackSpeed == row.Attack && monster.MoveSpeed == row.Move,
        $"real RefreshAll preserves non-lord {row.Rarity} subtype {row.Subtype} physical stats and speed");
    Check(monster.Stats[Stat.MinMC] == 50 && monster.Stats[Stat.MaxMC] == 100 &&
          monster.Stats[Stat.MinMAC] == 16 && monster.Stats[Stat.MaxMAC] == 32,
        $"non-lord {row.Rarity} subtype {row.Subtype} magic attack and defense receive no new bonus");
}

var legacy = new FixtureMonster(info) { Rarity = MonsterRarity.None, EliteTier = 2 };
legacy.RefreshAll();
Check(legacy.MaxHealth == 550 && legacy.Stats[Stat.MinDC] == 150 && legacy.Stats[Stat.MaxDC] == 340 &&
      legacy.Stats[Stat.MinMC] == 50 && legacy.Stats[Stat.MaxMC] == 100 && legacy.AttackSpeed == 850 && legacy.MoveSpeed == 850,
    "the legacy EliteTier 2 fallback is not treated as a four-tier lord");

var pet = new FixtureMonster(info) { Rarity = MonsterRarity.Lord, EliteTier = 2, Master = lord, PetLevel = 2 };
pet.RefreshAll();
Check(pet.MaxHealth == 140 && pet.Stats[Stat.MinDC] == 102 && pet.Stats[Stat.MaxDC] == 202 &&
      pet.Stats[Stat.MinMC] == 50 && pet.Stats[Stat.MaxMC] == 100 && pet.AttackSpeed == 1000 && pet.MoveSpeed == 1000,
    "owned pets skip wild lord scaling and retain their original pet level bonuses");

lord.LordEnraged = true;
lord.RefreshAll();
Check(lord.Stats[Stat.MinDC] == 204 && lord.Stats[Stat.MaxDC] == 468 &&
      lord.Stats[Stat.MinMC] == 60 && lord.Stats[Stat.MaxMC] == 120 && lord.MaxHealth == 900 &&
      lord.AttackSpeed == 720 && lord.MoveSpeed == 800,
    "enraged lord refresh preserves the existing faster interval without stacking the new damage bonus");

var zeroMagicInfo = new MonsterInfo { Name = "PhysicalOnlyFixture", MoveSpeed = 1000, AttackSpeed = 1000 };
zeroMagicInfo.Stats[Stat.HP] = 100;
zeroMagicInfo.Stats[Stat.MinDC] = 10; zeroMagicInfo.Stats[Stat.MaxDC] = 20;
var physicalLord = new FixtureMonster(zeroMagicInfo) { Rarity = MonsterRarity.Lord, EliteTier = 2 };
physicalLord.RefreshAll();
Check(physicalLord.Stats[Stat.MinMC] == 0 && physicalLord.Stats[Stat.MaxMC] == 0,
    "a physical-only lord gains no invented magic attack when its base magic stats are zero");
Console.WriteLine($"PASS: {passed} lord balance checks; no service started and no runtime data loaded.");

static int[] Snapshot(MonsterObject monster) => new[]
{
    monster.Stats[Stat.HP], monster.Stats[Stat.MinDC], monster.Stats[Stat.MaxDC],
    monster.Stats[Stat.MinMC], monster.Stats[Stat.MaxMC], monster.Stats[Stat.MinAC], monster.Stats[Stat.MaxAC],
    monster.Stats[Stat.MinMAC], monster.Stats[Stat.MaxMAC], monster.Stats[Stat.MinSC], monster.Stats[Stat.MaxSC],
    monster.AttackSpeed, monster.MoveSpeed
};

sealed class FixtureMonster : MonsterObject
{
    public FixtureMonster(MonsterInfo info) : base(info) { }
}
