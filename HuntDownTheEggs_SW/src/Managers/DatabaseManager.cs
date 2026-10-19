using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace HuntDownTheEggs;
public sealed class DatabaseManager
{
    private readonly HuntDownTheEggsPlugin _plugin;
    private readonly ISwiftlyCore _core;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private const string PlacedCount =
        "CASE WHEN EggID IS NULL OR EggID = '' THEN 0 " +
        "ELSE LENGTH(EggID) - LENGTH(REPLACE(EggID, ',', '')) + 1 END";

    public DatabaseManager(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
    {
        _plugin = plugin;
        _core = core;
    }

    private IDbConnection Open() => _core.Database.GetConnection(_plugin.Config.DatabaseConnection);

    public void Initialize()
    {
        try
        {
            using var conn = Open();
            conn.Execute("CREATE TABLE IF NOT EXISTS EggHunt(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggID VARCHAR(255))");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggKills(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT)");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggRandom(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT)");

            var info = _core.Database.GetConnectionInfo(_plugin.Config.DatabaseConnection);
            _core.Logger.LogInformation(
                "HuntDownTheEggs: database ready (connection '{Name}', driver '{Driver}')",
                _plugin.Config.DatabaseConnection, info.Driver);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "HuntDownTheEggs: error while initializing database");
        }
    }


    public async Task<PlayerData?> GetPlayerEggsAsync(ulong steamId, string mapName)
    {
        try
        {
            using var conn = Open();
            var sid = steamId.ToString();

            var eggIds = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var totalEggs = await conn.QueryFirstOrDefaultAsync<int>(
                $@"SELECT
                   (SELECT COALESCE(SUM({PlacedCount}), 0) FROM EggHunt WHERE SteamID = @sid)
                   +
                   (SELECT COALESCE(SUM(EggsPicked), 0) FROM EggKills WHERE SteamID = @sid)
                   +
                   (SELECT COALESCE(SUM(EggsPicked), 0) FROM EggRandom WHERE SteamID = @sid)
                   AS TotalEggs",
                new { sid });

            var killEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggKills WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var randomEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggRandom WHERE SteamID = @sid AND Map = @mapName",
                new { sid, mapName });

            var eggs = eggIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList() ?? [];

            return new PlayerData
            {
                SteamId = steamId,
                Map = mapName,
                Eggs = eggs,
                KillEggs = killEggs,
                RandomEggs = randomEggs,
                TotalEggs = totalEggs
            };
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "HuntDownTheEggs: database error while loading player {SteamId}", steamId);
            return null;
        }
    }

    public async Task SavePlayerEggsAsync(PlayerData d)
    {
        await _writeLock.WaitAsync();
        try
        {
            using var conn = Open();
            var sid = d.SteamId.ToString();

            // Kill eggs
            if (await ExistsAsync(conn, "EggKills", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggKills SET EggsPicked = @eggs, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { sid, eggs = d.KillEggs, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggKills (SteamID, Map, EggsPicked, Name) VALUES (@sid, @map, @eggs, @name)",
                    new { sid, map = d.Map, eggs = d.KillEggs, name = d.PlayerName });

            // Random eggs
            if (await ExistsAsync(conn, "EggRandom", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggRandom SET EggsPicked = @eggs, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { sid, eggs = d.RandomEggs, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggRandom (SteamID, Map, EggsPicked, Name) VALUES (@sid, @map, @eggs, @name)",
                    new { sid, map = d.Map, eggs = d.RandomEggs, name = d.PlayerName });

            // Placed eggs
            if (d.Eggs.Count == 0) return;

            var existingStr = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT EggID FROM EggHunt WHERE SteamID = @sid AND Map = @map", new { sid, map = d.Map });
            var existing = existingStr?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToHashSet() ?? [];

            if (!d.Eggs.Any(e => !existing.Contains(e))) return;

            var combined = string.Join(",", existing.Union(d.Eggs).OrderBy(x => x));

            if (await ExistsAsync(conn, "EggHunt", sid, d.Map))
                await conn.ExecuteAsync("UPDATE EggHunt SET EggID = @combined, Name = @name WHERE SteamID = @sid AND Map = @map",
                    new { combined, sid, map = d.Map, name = d.PlayerName });
            else
                await conn.ExecuteAsync("INSERT INTO EggHunt (SteamID, Map, EggID, Name) VALUES (@sid, @map, @combined, @name)",
                    new { sid, map = d.Map, combined, name = d.PlayerName });
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "HuntDownTheEggs: error while saving player {SteamId}", d.SteamId);
        }
        finally
        {
            _writeLock.Release();
        }
    }


    // TOP 5 on current map

    public Task<Dictionary<string, int>> GetTopEggsPerMapAsync(string mapName) => GetTopPlacedEggsAsync(mapName);

    public Task<Dictionary<string, int>> GetTopKillEggsPerMapAsync(string mapName) => GetTopCountEggsAsync("EggKills", mapName);

    public Task<Dictionary<string, int>> GetTopRandomEggsPerMapAsync(string mapName) => GetTopCountEggsAsync("EggRandom", mapName);

    public Task<Dictionary<string, int>> GetTopAllEggsPerMapAsync(string mapName) => GetTopCombinedEggsAsync(mapName);


    // TOP 5 from all maps

    public Task<Dictionary<string, int>> GetTopPlacedEggsAsync() => GetTopPlacedEggsAsync(null);

    public Task<Dictionary<string, int>> GetTopKillEggsAsync() => GetTopCountEggsAsync("EggKills", null);

    public Task<Dictionary<string, int>> GetTopRandomEggsAsync() => GetTopCountEggsAsync("EggRandom", null);

    public Task<Dictionary<string, int>> GetTopAllEggsAsync() => GetTopCombinedEggsAsync(null);

    private static string MapFilter(string? mapName) => mapName == null ? "" : "WHERE Map = @mapName";
    private async Task<Dictionary<string, int>> GetTopPlacedEggsAsync(string? mapName)
    {
        try
        {
            using var conn = Open();

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM({PlacedCount}) AS Cnt
                       FROM EggHunt
                       {MapFilter(mapName)}
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "Error loading placed egg leaderboard");
            return [];
        }
    }


    private async Task<Dictionary<string, int>> GetTopCountEggsAsync(string table, string? mapName)
    {
        // Nazwa tabeli trafia do SQL-a, więc tylko z białej listy.
        if (table != "EggKills" && table != "EggRandom")
            return [];

        try
        {
            using var conn = Open();

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS Cnt
                       FROM {table}
                       {MapFilter(mapName)}
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "Error loading leaderboard from {Table}", table);
            return [];
        }
    }


    private async Task<Dictionary<string, int>> GetTopCombinedEggsAsync(string? mapName)
    {
        try
        {
            using var conn = Open();
            var filter = MapFilter(mapName);

            var rows = await conn.QueryAsync<TopRow>(
                $@"SELECT Name, Cnt FROM (
                       SELECT MAX(Name) AS Name, SUM(EggCount) AS Cnt
                       FROM (
                           SELECT SteamID, MAX(Name) AS Name, SUM({PlacedCount}) AS EggCount
                           FROM EggHunt
                           {filter}
                           GROUP BY SteamID

                           UNION ALL

                           SELECT SteamID, MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS EggCount
                           FROM EggKills
                           {filter}
                           GROUP BY SteamID

                           UNION ALL

                           SELECT SteamID, MAX(Name) AS Name, SUM(COALESCE(EggsPicked, 0)) AS EggCount
                           FROM EggRandom
                           {filter}
                           GROUP BY SteamID
                       ) AS EggStats
                       GROUP BY SteamID
                   ) t
                   WHERE Cnt > 0
                   ORDER BY Cnt DESC
                   LIMIT 5",
                new { mapName });

            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "Error loading combined egg leaderboard");
            return [];
        }
    }

    private static Task<bool> ExistsAsync(IDbConnection conn, string table, string sid, string map) =>
        conn.QueryFirstOrDefaultAsync<int>(
                $"SELECT COUNT(1) FROM {table} WHERE SteamID = @sid AND Map = @map", new { sid, map })
            .ContinueWith(t => t.Result > 0);

    private sealed class TopRow
    {
        public string Name { get; set; } = string.Empty;
        public decimal Cnt { get; set; }
    }

    private static Dictionary<string, int> ToDict(IEnumerable<TopRow> rows)
    {
        var d = new Dictionary<string, int>();
        foreach (var r in rows) d[r.Name] = (int)r.Cnt;
        return d;
    }
}