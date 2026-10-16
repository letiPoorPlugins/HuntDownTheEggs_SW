namespace HuntDownTheEggs.Api;
public enum EggCollectionKind
{
    Placed,
    Kill,
    Random
}

public sealed record EggCollectedEvent(
    ulong SteamId,
    string PlayerName,
    int? EggId,
    string Map,
    EggCollectionKind Kind
);
public interface IHuntDownTheEggsApi
{
    int GetEggCount(ulong steamId);

    bool HasFoundEgg(ulong steamId, int eggId);

    int GetKillEggCount(ulong steamId);

    int GetRandomEggCount(ulong steamId);
    event Action<EggCollectedEvent>? EggCollected;
    
}