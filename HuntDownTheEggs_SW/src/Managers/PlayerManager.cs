using System.Collections.Concurrent;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HuntDownTheEggs;

public sealed class PlayerManager
{
    private readonly HuntDownTheEggsPlugin _plugin;
    private readonly ISwiftlyCore _core;
    private readonly ConcurrentDictionary<ulong, PlayerData> _players = new();
    private readonly ConcurrentDictionary<ulong, byte> _loading = new();

    private Dictionary<string, int> _topEggs = [];
    private Dictionary<string, int> _topKillEggs = [];
    private Dictionary<string, int> _topRandomEggs = [];

    public PlayerManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
    {
        _plugin = plugin;
        _core = core;
    }

    public void Clear() => _players.Clear();

    public PlayerData? Get(ulong steamId) => _players.TryGetValue(steamId, out var d) ? d : null;

    /// <summary>Ładuje dane gracza z bazy w tle (bez blokowania wątku gry). Bezpieczne do wielokrotnego wołania.</summary>
    public void EnsureLoaded(ulong steamId, string name)
    {
        if (steamId == 0 || _players.ContainsKey(steamId)) return;
        if (!_loading.TryAdd(steamId, 0)) return;

        var map = _plugin.MapName;
        _ = Task.Run(async () =>
        {
            try
            {
                var existing = await _plugin.Database.GetPlayerEggsAsync(steamId, map);
                var data = existing ?? new PlayerData { SteamId = steamId, Map = map };
                data.PlayerName = name;
                // Jeśli mapa zmieniła się w międzyczasie, wynik jest nieaktualny.
                if (map == _plugin.MapName) _players[steamId] = data;
            }
            finally
            {
                _loading.TryRemove(steamId, out _);
            }
        });
    }

    public void AddFoundEgg(ulong steamId, int eggId)
    {
        if (_players.TryGetValue(steamId, out var d) && !d.Eggs.Contains(eggId)) d.Eggs.Add(eggId);
    }

    public void IncrementKillEggs(ulong steamId)
    {
        if (_players.TryGetValue(steamId, out var d)) d.KillEggs++;
    }

    public void IncrementRandomEggs(ulong steamId)
    {
        if (_players.TryGetValue(steamId, out var d)) d.RandomEggs++;
    }
    public void SaveAndForget(ulong steamId, string name)
    {
        if (!_players.TryRemove(steamId, out var d)) return;
        d.PlayerName = name;
        var snapshot = Snapshot(d);
        _ = Task.Run(() => _plugin.Database.SavePlayerEggsAsync(snapshot));
    }
    public void SaveAllBlocking()
    {
        foreach (var d in _players.Values.ToList())
            _plugin.Database.SavePlayerEggsAsync(Snapshot(d)).GetAwaiter().GetResult();
    }

    public void SaveAllInBackground()
    {
        var snapshots = _players.Values.Select(Snapshot).ToList();
        _ = Task.Run(async () =>
        {
            foreach (var s in snapshots) await _plugin.Database.SavePlayerEggsAsync(s);
        });
    }

    private static PlayerData Snapshot(PlayerData d) => new()
    {
        SteamId = d.SteamId, PlayerName = d.PlayerName, Map = d.Map,
        Eggs = [.. d.Eggs], KillEggs = d.KillEggs, RandomEggs = d.RandomEggs, TotalEggs = d.TotalEggs
    };

    public void LoadTopLists(string map)
    {
        _ = Task.Run(async () =>
        {
            _topEggs = await _plugin.Database.GetTopEggsAsync();
            _topKillEggs = await _plugin.Database.GetTopKillEggsAsync(map);
            _topRandomEggs = await _plugin.Database.GetTopRandomEggsAsync(map);
        });
    }

    public Dictionary<string, int> TopEggs => _topEggs;
    public Dictionary<string, int> TopKillEggs => _topKillEggs;
    public Dictionary<string, int> TopRandomEggs => _topRandomEggs;
}
