using HuntDownTheEggs;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using HuntDownTheEggs.Api;

namespace HuntDownTheEggsEggs;

public sealed class Helpers(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
{
    private readonly HuntDownTheEggsPlugin _plugin = plugin;
    private readonly ISwiftlyCore _core = core;

    public void SendLines(IPlayer player, string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            player.SendChatAsync(line);
    }

    public void NotifyCollected(IPlayer player, EggKind kind, int? eggId)
    {
        var apiKind = kind switch
        {
            EggKind.Placed => EggCollectionKind.Placed,
            EggKind.Kill => EggCollectionKind.Kill,
            EggKind.Random => EggCollectionKind.Random,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        _plugin.Api.NotifyEggCollected(
            player.SteamID,
            player.Controller.PlayerName,
            eggId,
            _plugin.MapName,
            apiKind
        );
    }

    public string ReplacePlayerParameters(string input, IPlayer p)
    {
        var id64 = p.SteamID;
        const ulong baseId = 76561197960265728UL;
        var accountId = id64 - baseId;
        var steam2 = $"STEAM_0:{accountId & 1}:{accountId >> 1}";
        var steam3 = $"[U:1:{accountId}]";

        return input
            .Replace("{USERID}", p.UserID.ToString())
            .Replace("{STEAMID}", steam2)
            .Replace("{STEAMID3}", steam3)
            .Replace("{STEAMID64}", id64.ToString())
            .Replace("{NAME}", p.Controller.PlayerName)
            .Replace("{SLOT}", p.PlayerID.ToString());
    }

    public bool IfWarmup()
    {
        var gamerules = _core.EntitySystem.GetGameRules();
        if (gamerules == null)
        {
            return false;
        }
        if (gamerules.WarmupPeriod)
        {
            return true;
        }
        else
        {
            return false;
        }
    }


}