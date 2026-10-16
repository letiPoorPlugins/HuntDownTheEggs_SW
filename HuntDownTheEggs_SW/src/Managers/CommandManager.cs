using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace HuntDownTheEggs;

public sealed class CommandManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;
    private readonly ISwiftlyCore _core = core; 
    public void RegisterCommands()
    {
        var cfg = _plugin.Config.CommandsSetup;
        if (!cfg.EnableCommands) return;

        var perm = _plugin.Config.EggSetup.EggRootPermission;

        // Komendy adminów – uprawnienie sprawdza SwiftlyS2 (zamiast AdminManager.PlayerHasPermissions z CSS).
        Register(cfg.PlaceEggCommands, PlaceEgg, perm);
        Register(cfg.TeleportToEggCommands, TeleportToEgg, perm);
        Register(cfg.RemoveEggCommands, RemoveEgg, perm);
        Register(cfg.ReloadEggsCommands, ReloadEggs, perm);
        Register(cfg.PlacingModeCommands, TogglePlacingMode, perm);

        // Komendy dla wszystkich
        Register(cfg.ShowMyEggsCommands, ShowMyEggs, null);
        Register(cfg.ShowTopEggsCommands, c => ShowTop(c, "hdte.top_list_header", _plugin.Players.TopEggs), null);
        Register(cfg.ShowTopKillEggsCommands, c => ShowTop(c, "hdte.top_kill_eggs_header", _plugin.Players.TopKillEggs), null);
        Register(cfg.ShowTopRandomEggsCommands, c => ShowTop(c, "hdte.top_random_eggs_header", _plugin.Players.TopRandomEggs), null);

        _plugin.DebugLog("Commands registered.");
    }

    private void Register(IEnumerable<string> names, ICommandService.CommandListener handler, string? permission)
    {
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (permission != null) _core.Command.RegisterCommand(name, handler, permission: permission);
            else _core.Command.RegisterCommand(name, handler);
        }
    }

    // ---------- admin ----------

    private void PlaceEgg(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer) return;
        var player = ctx.Sender!;
        var l = _core.Translation.GetPlayerLocalizer(player);
        var origin = player.PlayerPawn?.AbsOrigin;
        if (origin == null) return;

        var color = ctx.Args.Length < 1 ? "default" : ctx.Args[0];
        var id = _plugin.Eggs.EggCount;

        _plugin.Eggs.SpawnEgg(origin.Value, EggKind.Placed, color, id);

        try
        {
            _plugin.Eggs.AddNewEgg(origin.Value, color);
            player.SendChat(l["hdte.prefix"] + l["hdte.placed_egg", $"{origin.Value.X:F0} {origin.Value.Y:F0} {origin.Value.Z:F0}"]);
        }
        catch (Exception ex)
        {
            _plugin.DebugLog($"Error saving egg: {ex}");
            player.SendChat(l["hdte.prefix"] + l["hdte.error_saving_egg"]);
        }
    }

    private void TeleportToEgg(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer) return;
        var player = ctx.Sender!;
        var l = _core.Translation.GetPlayerLocalizer(player);
        var prefix = l["hdte.prefix"];

        if (ctx.Args.Length < 1) { player.SendChat(prefix + l["hdte.usage_tpegg"]); return; }
        if (!int.TryParse(ctx.Args[0], out var id)) { player.SendChat(prefix + l["hdte.invalid_id"]); return; }

        var egg = _plugin.Eggs.GetEggById(id);
        if (egg == null) { player.SendChat(prefix + l["hdte.egg_not_found"]); return; }

        player.Teleport(new Vector(egg.X, egg.Y, egg.Z));
        player.SendChat(prefix + l["hdte.teleport_to_egg", id]);
    }

    private void RemoveEgg(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer) return;
        var player = ctx.Sender!;
        var l = _core.Translation.GetPlayerLocalizer(player);
        var prefix = l["hdte.prefix"];

        if (ctx.Args.Length < 1) { player.SendChat(prefix + l["hdte.usage_removeegg"]); return; }
        if (!int.TryParse(ctx.Args[0], out var id)) { player.SendChat(prefix + l["hdte.invalid_id"]); return; }
        if (_plugin.Eggs.GetEggById(id) == null) { player.SendChat(prefix + l["hdte.egg_not_found"]); return; }

        _plugin.Eggs.RemoveEgg(id);
        _plugin.Eggs.SaveIfRemovedEggs();
        _plugin.Eggs.RemoveAllEggEntities();

        player.SendChat(prefix + l["hdte.regenerating_eggs"]);
        _plugin.Eggs.SpawnAllEggs();
    }

    private void ReloadEggs(ICommandContext ctx)
    {
        _plugin.Eggs.RemoveAllEggEntities();
        _plugin.Eggs.LoadEggsFromMap();
        _plugin.Eggs.SpawnAllEggs();

        if (ctx.IsSentByPlayer)
        {
            var l = _core.Translation.GetPlayerLocalizer(ctx.Sender!);
            ctx.Sender!.SendChat(l["hdte.prefix"] + l["hdte.eggs_reloaded"]);
        }
    }

    private void TogglePlacingMode(ICommandContext ctx)
    {
        _plugin.Eggs.PlacingMode = !_plugin.Eggs.PlacingMode;
        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
        {
            var l = _core.Translation.GetPlayerLocalizer(p);
            p.SendChat(l["hdte.prefix"] + l["hdte.placing_mode", _plugin.Eggs.PlacingMode ? "ON" : "OFF"]);
        }
    }

    // ---------- gracze ----------

    private void ShowMyEggs(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer) return;
        var player = ctx.Sender!;
        var l = _core.Translation.GetPlayerLocalizer(player);

        var data = _plugin.Players.Get(player.SteamID);
        if (data == null)
        {
            player.SendChat(l["hdte.prefix"] + l["hdte.player_not_found"]);
            return;
        }

        var total = data.TotalEggs + data.KillEggs;
        _plugin.SendLines(player, l["hdte.my_eggs", data.TotalEggs, data.KillEggs, total, data.Eggs.Count, data.Map, data.RandomEggs]);
    }

    private void ShowTop(ICommandContext ctx, string headerKey, Dictionary<string, int> top)
    {
        if (!ctx.IsSentByPlayer) return;
        var player = ctx.Sender!;
        var l = _core.Translation.GetPlayerLocalizer(player);

        _plugin.SendLines(player, l[headerKey]);

        if (top.Count == 0)
        {
            player.SendChat(l["hdte.prefix"] + l["hdte.no_eggs_found"]);
            return;
        }

        var i = 0;
        foreach (var (name, count) in top.ToList())
        {
            i++;
            player.SendChat(l["hdte.top_list_player", i, name, count]);
        }
    }

    
}


