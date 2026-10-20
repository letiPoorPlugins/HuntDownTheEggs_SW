
using System.Text.Json;
using HuntDownTheEggs_SW;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using DrawingColor = System.Drawing.Color;
using NativeColor = SwiftlyS2.Shared.Natives.Color;
using System.Drawing;
using HuntDownTheEggs.Api;
namespace HuntDownTheEggs;

public sealed class EggManager
{
    private const byte CollisionWeapon = 14;

    private readonly HuntDownTheEggsPlugin _plugin;
    private readonly ISwiftlyCore _core;
    private readonly Random _random = new();
    private static readonly object FileLock = new();

    private readonly List<EggData> _eggs = [];
    private readonly Dictionary<uint, EggInstance> _byProp = new();
    private readonly Dictionary<uint, EggInstance> _byTrigger = new();

    public bool PlacingMode { get; set; }

    public EggManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
    {
        _plugin = plugin;
        _core = core;
    }

    // ---------- pliki map ----------

    private string MapFilePath()
    {
        var baseDir = Path.GetDirectoryName(_core.Configuration.GetConfigPath("config.jsonc"))!;
        var dir = Path.Combine(baseDir, "maps");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{_plugin.MapName}.json");
    }

    public void LoadEggsFromMap()
    {
        if (!_plugin.Config.ModesSetup.SearchMode) return;
        var path = MapFilePath();
        if (!File.Exists(path)) { File.WriteAllText(path, "[]"); return; }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return;
        _eggs.Clear();
        _eggs.AddRange(JsonSerializer.Deserialize<List<EggData>>(json) ?? []);
        _plugin.DebugLog($"Loaded {_eggs.Count} eggs from {_plugin.MapName}.json");
    }

    public void CheckIfEggsAreThere()
    {
        if (_eggs.Count > 0) return;
        try { LoadEggsFromMap(); }
        catch (Exception ex) { _plugin.DebugLog(ex.ToString()); }
    }

    private void SaveEggsFile()
    {
        File.WriteAllText(MapFilePath(), JsonSerializer.Serialize(_eggs, new JsonSerializerOptions { WriteIndented = true }));
    }

    public int EggCount => _eggs.Count;
    public EggData? GetEggById(int id) => id >= 0 && id < _eggs.Count ? _eggs[id] : null;

    public void ClearCache()
    {
        _eggs.Clear();
        _byProp.Clear();
        _byTrigger.Clear();
    }

    public void AddNewEgg(Vector pos, string color)
    {
        if (!_plugin.Config.ModesSetup.SearchMode) return;
        lock (FileLock)
        {
            _eggs.Add(new EggData
            {

                Id = _eggs.Count,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                ModelColor = color
            });
            SaveEggsFile();
        }
    }

    public void RemoveEgg(int id)
    {
        var egg = _eggs.FirstOrDefault(e => e.Id == id);
        if (egg != null) _eggs.Remove(egg);
    }

    public void SaveIfRemovedEggs()
    {
        lock (FileLock)
        {
            for (var i = 0; i < _eggs.Count; i++) _eggs[i].Id = i;
            SaveEggsFile();
        }
    }

    // ---------- spawn ----------

    public void SpawnAllEggs()
    {
        var cfg = _plugin.Config.ModesSetup;

        if (!_plugin.AllowToSpawnEggs) return;

        foreach (var egg in _eggs)
            SpawnEgg(new Vector(egg.X, egg.Y, egg.Z), EggKind.Placed, egg.ModelColor, egg.Id);

        if (!cfg.SpawnRandomEggs) return;

        if (!cfg.SpawnRandomEggsOnWarmup && _plugin.WarmupRunning)
        {
            _plugin.DebugLog("Warmup detected, not spawning random eggs on this round.");
            return;
        }

        var humans = _core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient);
        if (humans < cfg.MinPlayersToSpawnRandomEggs)
        {
            _plugin.DebugLog($"Not enough players to spawn random eggs. Current: {humans}, Required: {cfg.MinPlayersToSpawnRandomEggs}");
            return;
        }

        var toSpawn = cfg.NumberOfRandomEggs;
        if (cfg.SpawnRandomEggsByPercentageOfPlayers)
            toSpawn = (int)MathF.Ceiling(cfg.NumberOfRandomEggs * (PlayerPercentage() / 100f));

        var spawned = 0;
        for (var i = 0; i < toSpawn; i++)
        {
            var pos = NavMesh.GetRandomPosition();
            if (pos == null)
            {
                _plugin.DebugLog($"No random point available (attempt {i}). Add some with the addrandompoint command.");
                break;
            }
            if (SpawnEgg(pos.Value, EggKind.Random, "default") != null) spawned++;
        }

        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
        {
            var l = _core.Translation.GetPlayerLocalizer(p);
            p.SendChat(l["hdte.prefix"] + l["hdte.new_round_random_eggs", spawned]);
        }
    }

    private float PlayerPercentage()
    {
        var all = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        var humans = all.Count(p => !p.IsFakeClient);
        var slots = _core.PlayerManager.MaxPlayers - all.Count(p => p.IsFakeClient);
        return slots <= 0 ? 100f : (float)humans / slots * 100f;
    }

    public EggInstance? SpawnEgg(Vector position, EggKind kind, string? color, int eggId = -1, ulong ownerSteamId = 0)
    {
        var cfg = _plugin.Config;
        var shoot = cfg.ModesSetup.ShootEggMode;

        var prop = _core.EntitySystem.CreateEntityByDesignerName<CDynamicProp>("prop_dynamic_override");
        if (prop == null || !prop.IsValid)
        {
            _plugin.DebugLog("Failed to create egg prop!");
            return null;
        }
        /*
        if (shoot)
        {
            prop.Collision.CollisionGroup = CollisionWeapon;
            prop.Collision.CollisionGroupUpdated();
            prop.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            prop.Collision.SolidTypeUpdated();
            prop.TakesDamage = true;
            prop.TakesDamageUpdated();
        }
        */
        prop.Collision.CollisionGroup = CollisionWeapon;
        prop.Collision.CollisionGroupUpdated();
        prop.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        prop.Collision.SolidTypeUpdated();
        prop.TakesDamage = true;
        prop.TakesDamageUpdated();

        var render = ResolveColor(color);

        if (render.HasValue)
        {
            prop.Render = render.Value;
            prop.RenderUpdated();
        }

        prop.Entity!.Flags &= ~(1u << 2);

        prop.SetModel(cfg.EggSetup.EggModel);
        prop.DispatchSpawn();

        if (shoot)
        {
            prop.Health = cfg.ModesSetup.ShootEggHealth;
            prop.MaxHealth = cfg.ModesSetup.ShootEggHealth;
            prop.HealthUpdated();
            prop.MaxHealthUpdated();
        }

        var pos = new Vector(position.X, position.Y, position.Z + cfg.EggSetup.EggModelHeight);
        var rot = kind == EggKind.Random ? new QAngle(0, _random.Next(0, 360), 0) : new QAngle(0, 0, 0);
        prop.Teleport(pos, rot, Vector.Zero);

        prop.UseAnimGraph = false;
        prop.UseAnimGraphUpdated();

        if (!string.IsNullOrWhiteSpace(cfg.EggSetup.EggAnimation))
            prop.AcceptInput("SetAnimation", cfg.EggSetup.EggAnimation);
        prop.AcceptInput("Enable", "");

        prop.CBodyComponent!.SceneNode!.Scale = cfg.EggSetup.EggModelScale;
        prop.CBodyComponent.SceneNode.ScaleUpdated();

        prop.Entity.Name = $"pack-hdte-{kind}-{eggId}";

        var inst = new EggInstance { Prop = prop, Kind = kind, EggId = eggId, OwnerSteamId = ownerSteamId };

        if (cfg.EggSetup.Glowing) inst.Glow = CreateGlow(prop, pos);

        _byProp[prop.Index] = inst;

        if (cfg.ModesSetup.PickUpMode) CreateTrigger(inst, pos);

        return inst;
    }

    private CDynamicProp? CreateGlow(CDynamicProp parent, Vector pos)
    {
        var cfg = _plugin.Config.EggSetup;
        var glow = _core.EntitySystem.CreateEntityByDesignerName<CDynamicProp>("prop_dynamic");
        if (glow == null || !glow.IsValid) return null;

        glow.Spawnflags = 0;
        glow.SpawnflagsUpdated();
        glow.Render = new NativeColor(0, 0, 0, 0);
        glow.RenderUpdated();
        glow.Entity!.Flags &= ~(1u << 2);
        glow.SetModel(cfg.EggModel);
        glow.DispatchSpawn();

        var color = ResolveColor(cfg.GlowingColor)
            ?? new NativeColor(0, 255, 0, 255);
        glow.Glow.GlowColorOverride = color;
        glow.Glow.GlowRange = cfg.GlowingRange;
        glow.Glow.GlowRangeMin = 0;
        glow.Glow.GlowTeam = -1; // -1 = oba teamy
        glow.Glow.GlowType = 3;
        glow.Glow.GlowColorOverrideUpdated();
        glow.Glow.GlowRangeUpdated();
        glow.Glow.GlowRangeMinUpdated();
        glow.Glow.GlowTeamUpdated();
        glow.Glow.GlowTypeUpdated();

        glow.Teleport(pos, new QAngle(0, 0, 0), Vector.Zero);
        glow.AcceptInput("SetParent", "!activator", parent, glow);
        return glow;
    }

    private void CreateTrigger(EggInstance inst, Vector pos)
    {
        var cfg = _plugin.Config.EggSetup;
        var trigger = _core.EntitySystem.CreateEntityByDesignerName<CTriggerMultiple>("trigger_multiple");
        if (trigger == null || !trigger.IsValid)
        {
            _plugin.DebugLog("Failed to create trigger entity for egg!");
            return;
        }

        trigger.Spawnflags = (1 << 0) | (1 << 1);
        trigger.SpawnflagsUpdated();
        trigger.Entity!.Flags &= ~(1u << 2);
        trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        trigger.Collision.SolidFlags = 0;
        trigger.Collision.CollisionGroup = CollisionWeapon;
        trigger.Collision.SolidTypeUpdated();
        trigger.Collision.SolidFlagsUpdated();
        trigger.Collision.CollisionGroupUpdated();

        trigger.DispatchSpawn();
        trigger.SetModel(string.IsNullOrEmpty(cfg.TriggerModel) ? cfg.EggModel : cfg.TriggerModel);

        trigger.CBodyComponent!.SceneNode!.Scale = cfg.EggModelScale;
        trigger.CBodyComponent.SceneNode.ScaleUpdated();
        trigger.Teleport(pos, new QAngle(0, 0, 0), Vector.Zero);
        trigger.AcceptInput("SetParent", "!activator", inst.Prop, trigger);
        trigger.AcceptInput("Enable", "");

        inst.Trigger = trigger;
        _byTrigger[trigger.Index] = inst;
    }

    private NativeColor? ResolveColor(string? color)
    {
        if (string.IsNullOrEmpty(color) || color == "default")
            return null;

        DrawingColor drawingColor;

        if (color == "r")
        {
            var known = Enum.GetValues<KnownColor>()
                .Where(c =>
                    c != KnownColor.Transparent &&
                    !DrawingColor.FromKnownColor(c).IsSystemColor)
                .ToList();

            drawingColor = DrawingColor.FromKnownColor(
                known[_random.Next(known.Count)]);
        }
        else if (Enum.TryParse<KnownColor>(
            color, true, out var knownColor))
        {
            drawingColor = DrawingColor.FromKnownColor(knownColor);
        }
        else
        {
            _plugin.DebugLog(
                $"Invalid egg color '{color}', using default.");

            return null;
        }

        return new NativeColor(
            drawingColor.R,
            drawingColor.G,
            drawingColor.B,
            drawingColor.A);
    }

    // ---------- lookup / delete ----------

    public EggInstance? FindByTrigger(uint triggerIndex) => _byTrigger.GetValueOrDefault(triggerIndex);
    public EggInstance? FindByProp(uint propIndex) => _byProp.GetValueOrDefault(propIndex);

    public void RemoveEgg(EggInstance egg)
    {
        egg.Collected = true;
        _byProp.Remove(egg.Prop.Index);
        if (egg.Trigger != null) _byTrigger.Remove(egg.Trigger.Index);

        if (egg.Trigger is { IsValid: true }) egg.Trigger.Despawn();
        if (egg.Glow is { IsValid: true }) egg.Glow.Despawn();
        if (egg.Prop.IsValid) egg.Prop.Despawn();
    }

    public void RemoveEggDelayed(EggInstance egg, float seconds)
    {
        egg.Collected = true;
        _byProp.Remove(egg.Prop.Index);

        if (!string.IsNullOrWhiteSpace(_plugin.Config.EggSetup.EggCollectAnimation) && egg.Prop.IsValid)
            egg.Prop.AcceptInput("SetAnimation", _plugin.Config.EggSetup.EggCollectAnimation);

        _core.Scheduler.DelayBySeconds(seconds, () =>
        {
            if (egg.Glow is { IsValid: true }) egg.Glow.Despawn();
            if (egg.Prop.IsValid) egg.Prop.Despawn();
        });
    }

    public void RemoveAllEggEntities()
    {
        foreach (var egg in _byProp.Values.ToList())
        {
            if (egg.Trigger is { IsValid: true }) egg.Trigger.Despawn();
            if (egg.Glow is { IsValid: true }) egg.Glow.Despawn();
            if (egg.Prop.IsValid) egg.Prop.Despawn();
        }
        _byProp.Clear();
        _byTrigger.Clear();
    }

    public void ForgetAllEntities()
    {
        _byProp.Clear();
        _byTrigger.Clear();
    }

    // ---------- kill egg ----------

    public void TrySpawnKillEgg(IPlayer positionSource, IPlayer killer)
    {
        var pawn = positionSource.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;
        if ((float)(_random.NextDouble() * 100) > _plugin.Config.ModesSetup.ChanceToSpawn) return;

        var origin = pawn.AbsOrigin;
        if (origin == null) return;

        SpawnEgg(origin.Value, EggKind.Kill, "default", ownerSteamId: killer.SteamID);
    }

    // ---------- prizes ----------

    public string? GetRandomEggType()
    {
        var types = _plugin.Config.PrizeSetup.PresentTypes;
        if (types.Count == 0) return null;
        var roll = _random.NextDouble() * 100.0;
        double cumulative = 0;
        foreach (var kv in types)
        {
            cumulative += kv.Value.Chance;
            if (roll <= cumulative) return kv.Key;
        }
        return types.Keys.First();
    }

    public void GiveEggPrize(IPlayer player)
    {
        if (!player.IsValid || player.IsFakeClient) return;
        var l = _core.Translation.GetPlayerLocalizer(player);
        var prefix = l["hdte.prefix"];
        var prizeCfg = _plugin.Config.PrizeSetup;

        if (!prizeCfg.ReceivePrize || prizeCfg.PresentTypes.Count == 0)
        {
            var d = _plugin.Players.Get(player.SteamID);
            var total = (d?.RandomEggs ?? 0) + (d?.KillEggs ?? 0);
            player.SendChat(prefix + l["hdte.picked_egg_no_prize", total]);
            return;
        }

        var type = GetRandomEggType();
        if (type == null) return;

        var typeCfg = prizeCfg.PresentTypes[type];

        var candidates = typeCfg.Rewards.Select(r => new PrizeCandidate(r.Key, r.Value, null)).ToList();

        var moduleRewards = _plugin.Api.GetPrizeRewards(type);

        candidates.AddRange(moduleRewards.Select(r =>
            new PrizeCandidate(r.RewardName, r.RewardValue, r)));

        if (candidates.Count == 0)
            return;

        // Losujemy dokładnie jedną nagrodę z połączonej puli.
        var reward = candidates[_random.Next(candidates.Count)];

        bool success;

        if (reward.ModuleReward is { } moduleReward)
        {
            success = _plugin.Api.TryGivePrize(
                moduleReward,
                player.SteamID,
                player.Controller.PlayerName);
        }
        else
        {
            _core.Engine.ExecuteCommand(_plugin.Helper.ReplacePlayerParameters(reward.Value, player));
            success = true;
        }

        if (!success)
        {
            _plugin.DebugLog(
                $"Failed to give prize '{reward.Name}' from type '{type}'.");
            return;
        }

        player.SendChat(
            $"{prefix}[{typeCfg.Color}]" +
            $"{l["hdte.received_prize", reward.Name, type]}");

        var descKey = "hdte.received_desc." +
                      reward.Name.Replace(" ", "_");

        var desc = l[descKey];

        if (!string.IsNullOrEmpty(desc) && desc != descKey)
            player.SendChat(prefix + desc);
    }

    private sealed record PrizeCandidate(string Name, string Value, RegisteredPrizeReward? ModuleReward);


}
