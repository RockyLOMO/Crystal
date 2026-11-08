using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using Client;
using Client.MirControls;
using Client.MirScenes;
using Server.MirDatabase;
using Server.MirEnvir;
using ClientMapObject = Client.MirObjects.MapObject;
using ClientPlayer = Client.MirObjects.PlayerObject;
using ServerMonster = Server.MirObjects.MonsterObject;
using S = ServerPackets;

internal static class Program
{
    private static int passed;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 1) throw new ArgumentException("Optional argument: client native dependency directory.");
        args = args.Select(TestWorkspace.ResolvePath).ToArray();
        // No device is created. Some SlimDX installations still require their native
        // dependencies merely to load the real client types.
        if (args.Length == 1)
            foreach (string name in new[] { "msvcr100.dll", "msvcp100.dll", "d3dx9_43.dll", "d3dcompiler_43.dll" })
                File.Copy(Path.Combine(args[0], name), Path.Combine(AppContext.BaseDirectory, name), true);
        Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        ServerRecovery();
        ClientOwnership();
        Console.WriteLine($"PASS: {passed} healing ownership checks. No service, sockets, account loading or Direct3D device started.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine($"PASS {++passed}: {description}");
    }

    private static void ServerRecovery()
    {
        var environment = Envir.Main;
        var time = typeof(Envir).GetProperty(nameof(Envir.Time))!;
        long originalTime = environment.Time;
        try
        {
            // Real ProcessRegen, ChangeHP and BroadcastDamageIndicator. Only the
            // external broadcast/health publication boundaries are intercepted.
            var target = new CapturingMonster(100);
            foreach (var sample in new[] { (Max: 60, Amount: 2), (Max: 250, Amount: 6) })
            {
                var monster = new CapturingMonster(sample.Max) { HP = sample.Max - 20, Target = target, RegenTime = 10_000 };
                time.SetValue(environment, 10_000L);
                int before = monster.HP;
                monster.TickRegen();
                var packet = monster.Packets.OfType<S.DamageIndicator>().Single();
                Check(monster.HP - before == sample.Amount,
                    $"real monster regeneration restores {sample.Amount} HP for MaxHP={sample.Max} while it has a combat target");
                Check(packet.Damage == sample.Amount && packet.Type == DamageType.Hit &&
                      packet.ObjectID == monster.ObjectID && packet.ObjectID != target.ObjectID,
                    $"monster +{sample.Amount} packet belongs to the recovering monster, not its combat target");
                Check(monster.RegenTime == 20_000,
                    $"MaxHP={sample.Max} regeneration advances its existing ten-second timer");
                monster.Packets.Clear();
                time.SetValue(environment, 10_001L);
                monster.TickRegen();
                Check(monster.HP == before + sample.Amount && !monster.Packets.OfType<S.DamageIndicator>().Any(),
                    $"MaxHP={sample.Max} monster cannot repeat natural regeneration before the timer");
            }

            var capped = new CapturingMonster(250) { HP = 249, RegenTime = 20_000 };
            time.SetValue(environment, 20_000L);
            capped.TickRegen();
            var capPacket = capped.Packets.OfType<S.DamageIndicator>().Single();
            Check(capped.HP == 250 && capPacket.Damage == 6 && capPacket.ObjectID == capped.ObjectID,
                "existing cap boundary restores only one actual HP while the packet retains the calculated +6 amount");
            capped.Packets.Clear();
            capped.RegenTime = 30_000;
            time.SetValue(environment, 30_000L);
            capped.TickRegen();
            Check(capped.HP == 250 && !capped.Packets.OfType<S.DamageIndicator>().Any(),
                "a full-health monster without pending healing emits no natural recovery indicator");
        }
        finally
        {
            time.SetValue(environment, originalTime);
        }
    }

    private static void ClientOwnership()
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        CMain.Graphics = graphics;
        CMain.Time = 1_000;
        var assembly = typeof(GameScene).Assembly;
        var displayDamage = assembly.GetType("Client.Settings")!.GetField("DisplayDamage")!;
        bool previousDisplayDamage = (bool)displayDamage.GetValue(null)!;
        displayDamage.SetValue(null, true);
        var handler = typeof(GameScene).GetMethod("DamageIndicator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var scene = (GameScene)RuntimeHelpers.GetUninitializedObject(typeof(GameScene));
        var previousScene = GameScene.Scene;
        GameScene.Scene = scene;
        try
        {
            MapControl.Objects.Clear();
            MapControl.ObjectsList.Clear();
            var player = new ClientPlayer(91_001) { DisplayRectangle = new Rectangle(400, 300, 48, 64) };
            var monsterType = assembly.GetType("Client.MirObjects.MonsterObject")!;
            ClientMapObject Monster(uint id, Point point)
            {
                var monster = (ClientMapObject)Activator.CreateInstance(monsterType, new object[] { id })!;
                monster.DisplayRectangle = new Rectangle(point, new Size(48, 64));
                return monster;
            }
            var monster = Monster(91_002, new Point(600, 300));
            var otherMonster = Monster(91_003, new Point(800, 300));
            void Receive(uint id, int amount) => handler.Invoke(scene,
                new object[] { new S.DamageIndicator { ObjectID = id, Type = DamageType.Hit, Damage = amount } });

            ClientMapObject.TargetObjectID = monster.ObjectID;
            Receive(player.ObjectID, 6);
            Check(player.Damages.Count == 1 && monster.Damages.Count == 0 && otherMonster.Damages.Count == 0,
                "actual client handler attaches player +6 to the player while a monster is targeted");
            Check(player.Damages[0].Text == "+6" && player.Damages[0].Colour == Color.LimeGreen,
                "player healing retains its green positive presentation");
            ClientMapObject.TargetObjectID = otherMonster.ObjectID;
            Receive(monster.ObjectID, 2);
            Check(player.Damages.Count == 1 && monster.Damages.Count == 1 && otherMonster.Damages.Count == 0,
                "actual client handler attaches monster +2 to the packet object after changing the attack target");
            Check(monster.Damages[0].Text == "+2" && monster.Damages[0].Colour == Color.LimeGreen,
                "real monster recovery also receives a green positive indicator");
            ClientMapObject.TargetObjectID = player.ObjectID;
            Receive(monster.ObjectID, 6);
            Check(player.Damages.Count == 1 && monster.Damages.Count == 2 && otherMonster.Damages.Count == 0,
                "monster +6 remains on the monster even when the player is selected");
            Receive(99_999, 2);
            Check(player.Damages.Count == 1 && monster.Damages.Count == 2 && otherMonster.Damages.Count == 0,
                "an unknown packet ID does not redirect recovery to the selected object");

            // DrawDamages and Damage.Draw remain real. Replacing only the final
            // label.Draw boundary captures coordinates without creating a device.
            foreach (var obj in new[] { (ClientMapObject)player, monster })
            {
                foreach (var damage in obj.Damages) damage.DamageLabel = new CapturingLabel();
                obj.DrawDamages();
                foreach (var damage in obj.Damages)
                {
                    var label = (CapturingLabel)damage.DamageLabel;
                    var expected = obj.DisplayRectangle.Location;
                    expected.Offset(15 - damage.Text.Length * 3, -75);
                    Check(label.DrawCalls == 1 && label.Location == expected,
                        $"real DrawDamages anchors {damage.Text} at its owner ID={obj.ObjectID}, independently of the selected target");
                }
            }
        }
        finally
        {
            foreach (var obj in MapControl.Objects.Values)
                foreach (var damage in obj.Damages) damage.DamageLabel?.Dispose();
            ClientMapObject.TargetObjectID = 0;
            MapControl.Objects.Clear();
            MapControl.ObjectsList.Clear();
            GameScene.Scene = previousScene;
            displayDamage.SetValue(null, previousDisplayDamage);
            CMain.Graphics = null;
        }
    }

    private sealed class CapturingMonster : ServerMonster
    {
        public readonly List<Packet> Packets = new();
        public CapturingMonster(int maxHealth) : base(new MonsterInfo()) => Stats[Stat.HP] = maxHealth;
        public void TickRegen() => base.ProcessRegen();
        public override void Broadcast(Packet packet) => Packets.Add(packet);
        public override void BroadcastHealthChange() { }
    }

    private sealed class CapturingLabel : MirLabel
    {
        public int DrawCalls;
        public override void Draw() => DrawCalls++;
    }
}
