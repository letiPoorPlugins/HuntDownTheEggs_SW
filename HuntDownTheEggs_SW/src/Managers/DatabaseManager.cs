using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace HuntDownTheEggs;

/// <summary>
/// Te same tabele i zapytania co w wersji CSS (EggHunt / EggKills / EggRandom), więc istniejące dane działają bez migracji.
/// Połączenie bierzemy z Core.Database (database.jsonc SwiftlyS2) zamiast własnych DBHost/DBPassword w configu.
/// </summary>
public sealed class DatabaseManager
{
    private readonly HuntDownTheEggsPlugin _plugin;
    private readonly ISwiftlyCore _core;

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
            conn.Execute("CREATE TABLE IF NOT EXISTS EggHunt(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggID VARCHAR(255));");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggKills(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT);");
            conn.Execute("CREATE TABLE IF NOT EXISTS EggRandom(SteamID VARCHAR(255), Name VARCHAR(255), Map VARCHAR(255), EggsPicked INT);");
            _core.Logger.LogInformation("HuntDownTheEggs: database ready (connection '{Name}')", _plugin.Config.DatabaseConnection);
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
                "SELECT EggID FROM EggHunt WHERE SteamID = @sid AND Map = @mapName", new { sid, mapName });

            var totalEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(SUM(LENGTH(eh.EggID) - LENGTH(REPLACE(eh.EggID, ',', '')) + 1), 0) AS TotalEggs " +
                "FROM EggHunt eh WHERE eh.SteamID = @sid", new { sid });

            var killEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggKills WHERE SteamID = @sid AND Map = @mapName", new { sid, mapName });

            var randomEggs = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(EggsPicked, 0) FROM EggRandom WHERE SteamID = @sid AND Map = @mapName", new { sid, mapName });

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
    }

    public async Task<Dictionary<string, int>> GetTopEggsAsync()
    {
        try
        {
            using var conn = Open();
            var rows = await conn.QueryAsync<(string Name, int TotalEggs)>(
                @"SELECT `Name`, SUM(LENGTH(EggID) - LENGTH(REPLACE(EggID, ',', '')) + 1) AS TotalEggs
                  FROM `EggHunt`
                  WHERE `EggID` IS NOT NULL AND `EggID` <> ''
                  GROUP BY `SteamID`, `Name`
                  ORDER BY TotalEggs DESC
                  LIMIT 5;");
            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "HuntDownTheEggs: error while loading top eggs");
            return [];
        }
    }

    public Task<Dictionary<string, int>> GetTopKillEggsAsync(string map) => GetTopAsync("EggKills", map);
    public Task<Dictionary<string, int>> GetTopRandomEggsAsync(string map) => GetTopAsync("EggRandom", map);

    private async Task<Dictionary<string, int>> GetTopAsync(string table, string map)
    {
        try
        {
            using var conn = Open();
            var rows = await conn.QueryAsync<(string Name, int EggsPicked)>(
                $"SELECT `Name`, `EggsPicked` FROM `{table}` WHERE `Map` = @map ORDER BY `EggsPicked` DESC LIMIT 5;",
                new { map });
            return ToDict(rows);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "HuntDownTheEggs: error while loading top list from {Table}", table);
            return [];
        }
    }

    private static Task<bool> ExistsAsync(IDbConnection conn, string table, string sid, string map) =>
        conn.QueryFirstOrDefaultAsync<int>($"SELECT COUNT(1) FROM `{table}` WHERE SteamID = @sid AND Map = @map", new { sid, map })
            .ContinueWith(t => t.Result > 0);

    // Oryginał używał ToDictionary – przy dwóch graczach o tym samym nicku rzucało wyjątek. Tu: ostatni wygrywa.
    private static Dictionary<string, int> ToDict(IEnumerable<(string Name, int Count)> rows)
    {
        var d = new Dictionary<string, int>();
        foreach (var (name, count) in rows) d[name] = count;
        return d;
    }
}
