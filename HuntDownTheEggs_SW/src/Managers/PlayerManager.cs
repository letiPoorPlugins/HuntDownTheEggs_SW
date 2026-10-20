using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace HuntDownTheEggs;

public sealed class PlayerManager
{
    private readonly HuntDownTheEggsPlugin _plugin;
    private readonly ISwiftlyCore _core;
    private readonly ConcurrentDictionary<ulong, PlayerData> _players = new();
    private readonly ConcurrentDictionary<ulong, byte> _loading = new();

    private Dictionary<string, int> _topPlacedEggsPerMap = [];
    private Dictionary<string, int> _topRandomEggsPerMap = [];
    private Dictionary<string, int> _topKillEggsPerMap = [];
    private Dictionary<string, int> _topAllEggsPerMap = [];
    private Dictionary<string, int> _topPlacedEggs = [];
    private Dictionary<string, int> _topRandomEggs = [];
    private Dictionary<string, int> _topKillEggs = [];
    private Dictionary<string, int> _topAllEggs = [];
    private readonly object _topListsLock = new();
    public PlayerManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
    {
        _plugin = plugin;
        _core = core;
    }

    public void Clear() => _players.Clear();

    public PlayerData? Get(ulong steamId) => _players.TryGetValue(steamId, out var d) ? d : null;

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
        SteamId = d.SteamId,
        PlayerName = d.PlayerName,
        Map = d.Map,
        Eggs = [.. d.Eggs],
        KillEggs = d.KillEggs,
        RandomEggs = d.RandomEggs,
        TotalEggs = d.TotalEggs
    };

    public void LoadTopLists(string map)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var results = await Task.WhenAll(
                    _plugin.Database.GetTopEggsPerMapAsync(map),
                    _plugin.Database.GetTopRandomEggsPerMapAsync(map),
                    _plugin.Database.GetTopKillEggsPerMapAsync(map),
                    _plugin.Database.GetTopAllEggsPerMapAsync(map),

                    _plugin.Database.GetTopPlacedEggsAsync(),
                    _plugin.Database.GetTopRandomEggsAsync(),
                    _plugin.Database.GetTopKillEggsAsync(),
                    _plugin.Database.GetTopAllEggsAsync()
                );

                if (map != _plugin.MapName)
                    return;

                lock (_topListsLock)
                {
                    _topPlacedEggsPerMap = results[0];
                    _topRandomEggsPerMap = results[1];
                    _topKillEggsPerMap = results[2];
                    _topAllEggsPerMap = results[3];

                    _topPlacedEggs = results[4];
                    _topRandomEggs = results[5];
                    _topKillEggs = results[6];
                    _topAllEggs = results[7];
                }

                _plugin.DebugLog("All 8 egg leaderboards loaded.");
            }
            catch (Exception ex)
            {
                _core.Logger.LogError(ex, "Error loading egg leaderboards");
            }
        });
    }

    public Dictionary<string, int> TopPlacedEggsPerMap
    {
        get { lock (_topListsLock) return new(_topPlacedEggsPerMap); }
    }

    public Dictionary<string, int> TopRandomEggsPerMap
    {
        get { lock (_topListsLock) return new(_topRandomEggsPerMap); }
    }

    public Dictionary<string, int> TopKillEggsPerMap
    {
        get { lock (_topListsLock) return new(_topKillEggsPerMap); }
    }

    public Dictionary<string, int> TopAllEggsPerMap
    {
        get { lock (_topListsLock) return new(_topAllEggsPerMap); }
    }

    public Dictionary<string, int> TopPlacedEggs
    {
        get { lock (_topListsLock) return new(_topPlacedEggs); }
    }

    public Dictionary<string, int> TopRandomEggs
    {
        get { lock (_topListsLock) return new(_topRandomEggs); }
    }

    public Dictionary<string, int> TopKillEggs
    {
        get { lock (_topListsLock) return new(_topKillEggs); }
    }

    public Dictionary<string, int> TopAllEggs
    {
        get { lock (_topListsLock) return new(_topAllEggs); }
    }

}
