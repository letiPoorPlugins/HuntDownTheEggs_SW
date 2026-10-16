using HuntDownTheEggs.Api;

namespace HuntDownTheEggs;

public sealed class HuntDownTheEggsApi : IHuntDownTheEggsApi
{
    private readonly HuntDownTheEggsPlugin _plugin;

    public HuntDownTheEggsApi(HuntDownTheEggsPlugin plugin)
    {
        _plugin = plugin;
    }

    public int GetEggCount(ulong steamId)
    {
        return _plugin.Players.Get(steamId)?.Eggs.Count ?? 0;
    }

    public bool HasFoundEgg(ulong steamId, int eggId)
    {
        return _plugin.Players.Get(steamId)?.Eggs.Contains(eggId) ?? false;
    }

    public int GetKillEggCount(ulong steamId)
    {
        return _plugin.Players.Get(steamId)?.KillEggs ?? 0;
    }

    public int GetRandomEggCount(ulong steamId)
    {
        return _plugin.Players.Get(steamId)?.RandomEggs ?? 0;
    }

    public event Action<EggCollectedEvent>? EggCollected;
    public void NotifyEggCollected(
        ulong steamId,
        string playerName,
        int? eggId,
        string map,
        EggCollectionKind kind){
        var args = new EggCollectedEvent(
            steamId,
            playerName,
            eggId,
            map,
            kind);

        var handlers = EggCollected;
        if (handlers is null)
            return;

        foreach (Action<EggCollectedEvent> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(args);
            }
            catch (Exception ex)
            {
                _plugin.DebugLog(
                    $"EggCollected subscriber failed: {ex.Message}");
            }
        }


    }

}