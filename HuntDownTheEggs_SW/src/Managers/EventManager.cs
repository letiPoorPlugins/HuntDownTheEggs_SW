using HuntDownTheEggs;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HuntDownTheEggsEggs;

public sealed partial class EventManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;
    private readonly ISwiftlyCore _core = core;
    private Guid _triggerHook;

    public void RegisterEvents()
    {
        _core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);

        _core.Event.OnMapLoad += OnMapLoad;
        _core.Event.OnMapUnload += OnMapUnload;
        _core.Event.OnClientSteamAuthorize += OnClientSteamAuthorize;
        _core.Event.OnClientDisconnected += OnClientDisconnected;
        _core.Event.OnPrecacheResource += OnPrecacheResource;

        _triggerHook = _core.EntitySystem.HookEntityOutput("trigger_multiple", "OnStartTouch", OnTriggerTouch);

        _core.GameHooks.Entities.TakeDamage.Pre += OnTakeDamage;
    }

    public void UnregisterEvents()
    {
        _core.Event.OnMapLoad -= OnMapLoad;
        _core.Event.OnMapUnload -= OnMapUnload;
        _core.Event.OnClientSteamAuthorize -= OnClientSteamAuthorize;
        _core.Event.OnClientDisconnected -= OnClientDisconnected;
        _core.Event.OnPrecacheResource -= OnPrecacheResource;
        _core.GameHooks.Entities.TakeDamage.Pre -= OnTakeDamage;
        _core.EntitySystem.UnhookEntityOutput(_triggerHook);
    }

    // ---------- game events ----------

    private HookResult OnPlayerDeath(EventPlayerDeath e)
    {
        if (!_plugin.Config.ModesSetup.DeathMode) return HookResult.Continue;

        var victim = e.UserIdPlayer;
        var attacker = e.AttackerPlayer;
        if (victim == null || attacker == null || victim.SteamID == attacker.SteamID) return HookResult.Continue;
        if (attacker.PlayerPawn is not { IsValid: true }) return HookResult.Continue;

        if (_plugin.Config.ModesSetup.SpawnDeathEggOnVictim) _plugin.Eggs.TrySpawnKillEgg(victim, attacker);
        else _plugin.Eggs.TrySpawnKillEgg(attacker, attacker);

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart e)
    {
        _plugin.Eggs.ForgetAllEntities();
        _plugin.Eggs.CheckIfEggsAreThere();

        _plugin.WarmupRunning = _plugin.Helper.IfWarmup();

        if (!_plugin.Config.ModesSetup.SpawnEggsOnWarmup && _plugin.WarmupRunning)
        {
            _plugin.DebugLog("Warmup detected, not spawning _plugin.Eggs on this round.");
            return HookResult.Continue;
        }

        _plugin.Eggs.SpawnAllEggs();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd e)
    {
        if (_plugin.Config.ModesSetup.RemoveOnFind && _plugin.Config.ModesSetup.SpawnPlacedEggsOnce)
        {
            _plugin.DebugLog("Saving _plugin.Eggs to map file that were taken");
            _plugin.Eggs.SaveIfRemovedEggs();
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn e)
    {
        var p = e.UserIdPlayer;
        if (p == null || !p.IsValid || p.IsFakeClient) return HookResult.Continue;
        _plugin.Players.EnsureLoaded(p.SteamID, p.Controller.PlayerName);
        return HookResult.Continue;
    }

    // ---------- core events ----------

    private void OnMapLoad(IOnMapLoadEvent e)
    {
        _plugin.MapName = e.MapName;
        _plugin.WarmupRunning = false;

        _plugin.Players.Clear();
        _plugin.Eggs.ClearCache();
        _plugin.Eggs.LoadEggsFromMap();
        _plugin.Players.LoadTopLists(_plugin.MapName);

        _plugin.DebugLog($"Map started: {_plugin.MapName}");
    }

    private void OnMapUnload(IOnMapUnloadEvent e)
    {
        _plugin.Players.SaveAllInBackground();
        _plugin.Eggs.ForgetAllEntities();
    }

    private void OnPrecacheResource(IOnPrecacheResourceEvent e)
    {
        e.AddItem(_plugin.Config.EggSetup.EggModel);
        if (!string.IsNullOrEmpty(_plugin.Config.EggSetup.TriggerModel)) e.AddItem(_plugin.Config.EggSetup.TriggerModel);
    }

    private void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var player = _core.PlayerManager.GetPlayer(e.PlayerId);
        if (player == null || player.IsFakeClient) return;

        var steamId = player.SteamID;
        _plugin.Players.EnsureLoaded(steamId, player.Controller.PlayerName);

        _core.Scheduler.DelayBySeconds(5f, () =>
        {
            if (!player.IsValid) return;
            if (!_plugin.Config.ModesSetup.SearchMode && _plugin.Eggs.EggCount == 0) return;
            var l = _core.Translation.GetPlayerLocalizer(player);
            _plugin.Helper.SendLines(player, l["hdte.welcome", _plugin.Eggs.EggCount, _plugin.Config.ModesSetup.NumberOfRandomEggs]);
        });
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        var player = _core.PlayerManager.GetPlayer(e.PlayerId);
        if (player == null || player.IsFakeClient) return;
        _plugin.Players.SaveAndForget(player.SteamID, player.Controller.PlayerName);
    }

    // ---------- Gather eggs by touching ----------

    private void OnTriggerTouch(IOnEntityFireOutputHookEvent e)
    {
        if(!_plugin.Config.ModesSetup.PickUpMode) return;
        //if (_plugin.Config.ModesSetup.ShootEggMode) return;

        var activator = e.Activator?.As<CBaseEntity>();
        if (activator == null || activator.DesignerName != "player") return;

        var pawn = e.Activator!.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) return;

        var player = _core.PlayerManager.GetPlayerFromPawn(pawn);
        if (player == null || player.IsFakeClient) return;

        if (e.Caller == null) return;
        var egg = _plugin.Eggs.FindByTrigger(e.Caller.Index);
        if (egg == null || egg.Collected) return;

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            _plugin.Players.EnsureLoaded(player.SteamID, player.Controller.PlayerName);
            return;
        }

        var l = _core.Translation.GetPlayerLocalizer(player);
        var prefix = l["hdte.prefix"];

        switch (egg.Kind)
        {
            case EggKind.Kill:
                if (_plugin.Config.EggSetup.KillEggOnlyForKiller && egg.OwnerSteamId != player.SteamID)
                {
                    player.SendCenter(l["hdte.only_for_killer"]);
                    return;
                }
                _plugin.Players.IncrementKillEggs(player.SteamID);
                player.SendChat(prefix + l["hdte.kill_egg"]);
                _plugin.Eggs.RemoveEgg(egg);
                _plugin.Eggs.GiveEggPrize(player);

                _plugin.Helper.NotifyCollected(player, EggKind.Kill, null);
                return;

            case EggKind.Random:
                _plugin.Players.IncrementRandomEggs(player.SteamID);
                player.SendChat(prefix + l["hdte.random_egg"]);
                _plugin.Eggs.RemoveEgg(egg);
                _plugin.Eggs.GiveEggPrize(player);

                _plugin.Helper.NotifyCollected(player, EggKind.Random, null);
                return;
        }

        // Placed
        if (_plugin.Eggs.PlacingMode) return;

        if (data.Eggs.Contains(egg.EggId))
        {
            if (!_plugin.Config.EggSetup.HidePickedEggsForPlayer)
                player.SendChat(prefix + l["hdte.already_own"]);
            return;
        }

        _plugin.Players.AddFoundEgg(player.SteamID, egg.EggId);

        if (_plugin.Config.ModesSetup.RemoveOnFind)
        {
            _plugin.Eggs.RemoveEgg(egg);
            if (_plugin.Config.ModesSetup.SpawnPlacedEggsOnce) _plugin.Eggs.RemoveEgg(egg.EggId);
        }

        _plugin.Eggs.GiveEggPrize(player);
        _plugin.Helper.NotifyCollected(player, EggKind.Placed, egg.EggId);
    }

    // ---------- Gathering eggs by Shooting ----------

    private void OnTakeDamage(ref TakeDamageEntityPreContext ctx)
    {
        if (!_plugin.Config.ModesSetup.ShootEggMode) return;

        var egg = _plugin.Eggs.FindByProp(ctx.Params.Entity.Index);
        if (egg == null || egg.Collected) return;

        ctx.SetHookResult(HookResult.Stop);

        var damage = (int)Math.Round(ctx.Params.Info.Damage);
        var attackerEntity = ctx.Params.Info.Attacker.Value;
        if (attackerEntity == null) return;

        var asBase = attackerEntity.As<CBaseEntity>();
        if (asBase == null || asBase.DesignerName != "player") return;

        var pawn = attackerEntity.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) return;

        var player = _core.PlayerManager.GetPlayerFromPawn(pawn);
        if (player == null || player.IsFakeClient) return;

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            _plugin.Players.EnsureLoaded(player.SteamID, player.Controller.PlayerName);
            return;
        }

        var l = _core.Translation.GetPlayerLocalizer(player);
        var prefix = l["hdte.prefix"];

        if (egg.Kind == EggKind.Kill && _plugin.Config.EggSetup.KillEggOnlyForKiller && egg.OwnerSteamId != player.SteamID)
        {
            player.SendCenter(l["hdte.only_for_killer"]);
            return;
        }

        if (egg.Kind == EggKind.Placed && data.Eggs.Contains(egg.EggId))
        {
            player.SendChat(prefix + l["hdte.already_own"]);
            return;
        }

        egg.Prop.Health -= damage;
        egg.Prop.HealthUpdated();

        if (egg.Prop.Health > 0)
        {
            player.SendChat(prefix + l["hdte.egg_shot_hp", egg.Prop.Health]);
            return;
        }

        // If egg dead
        switch (egg.Kind)
        {
            case EggKind.Kill:
                _plugin.Players.IncrementKillEggs(player.SteamID);
                player.SendChat(prefix + l["hdte.kill_egg"]);
                break;
            case EggKind.Random:
                _plugin.Players.IncrementRandomEggs(player.SteamID);
                player.SendChat(prefix + l["hdte.random_egg"]);
                break;
            default:
                _plugin.Players.AddFoundEgg(player.SteamID, egg.EggId);
                break;
        }

        _plugin.Eggs.RemoveEggDelayed(egg, 0.3f);
        _plugin.Eggs.GiveEggPrize(player);

        _plugin.Helper.NotifyCollected(
            player,
            egg.Kind,
            egg.Kind == EggKind.Placed ? egg.EggId : null
        );
    }

}
