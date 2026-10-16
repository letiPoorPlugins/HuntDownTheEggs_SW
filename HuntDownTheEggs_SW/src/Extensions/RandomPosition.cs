
using System.Collections;
using System.Runtime.InteropServices;
using HuntDownTheEggs;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HuntDownTheEggs_SW;

public static class NavMesh
{
    private static HuntDownTheEggsPlugin? _plugin;
    private static ISwiftlyCore? _core;
    private static nint _navMeshPtrAddress;

    public static void Init(HuntDownTheEggsPlugin plugin, ISwiftlyCore core)
    {
        _plugin = plugin;
        _core = core;
        InitializeUnmanagedFunctions();
    }

    private static void Log(string message)
    {
        if (_plugin!.Config.Debug) _core!.Logger.LogInformation("[HDTE] {Message}", message);
    }

    public static bool InitializeUnmanagedFunctions()
    {
        if (_plugin is null)
            return false;

        try
        {
            if (!_core!.GameData.HasSignature(
                    "CCSNavArea_IsValidNavMesh"))
            {
                _core!.Logger.LogError(
                    "Missing signature: CCSNavArea_IsValidNavMesh");
                return false;
            }

            nint functionAddress =
                _core!.GameData.GetSignature(
                    "CCSNavArea_IsValidNavMesh");

            if (functionAddress == 0)
            {
                _core!.Logger.LogError(
                    "Signature CCSNavArea_IsValidNavMesh returned null.");
                return false;
            }

            _navMeshPtrAddress = functionAddress.Rel(3);

            Log($"NavMesh pointer address: 0x{_navMeshPtrAddress:X}");

            return true;
        }
        catch (Exception ex)
        {
            _core!.Logger.LogError(
                $"NavMesh initialization failed: {ex.Message}");
            return false;
        }
    }

    public static nint GetNavMeshPtrAddress()
    {
        return _navMeshPtrAddress;
    }

    public static nint GetNavMeshAddress()
    {
        if (_navMeshPtrAddress == 0)
            return 0;

        return Marshal.ReadIntPtr(_navMeshPtrAddress);
    }

    public static CNavMesh? GetNavMesh()
    {
        try
        {
            nint address = GetNavMeshAddress();

            Log($"NavMesh address: 0x{address:X}");

            if (address == 0)
            {
                Log("NavMesh address is null.");
                return null;
            }

            var navMesh = new CNavMesh(address);

            Log($"NavMesh created. Areas: {navMesh.Count}");

            return navMesh;
        }
        catch (Exception ex)
        {
            Log($"Error getting NavMesh: {ex.Message}");
            return null;
        }
    }

    public static Vector? GetRandomPosition(
        int maxAttempts = 10,
        bool includeOneWayAccessible = false)
    {
        try
        {
            var spawnPoints = GetSpawnPoints();

            Log($"Found {spawnPoints.Count} spawn points.");

            if (spawnPoints.Count == 0)
                return null;

            Vector? startPosition = spawnPoints
                .Select(entity => entity.AbsOrigin)
                .FirstOrDefault(position => position is not null);

            if (startPosition is not Vector position)
                return null;

            return GetRandomAccessiblePosition(
                position,
                maxAttempts,
                includeOneWayAccessible);
        }
        catch (Exception ex)
        {
            Log($"GetRandomPosition error: {ex.Message}");
            return null;
        }
    }

    public static Vector? GetRandomAccessiblePosition(
        Vector startPosition,
        int maxAttempts = 10,
        bool includeOneWayAccessible = false)
    {
        try
        {
            CNavMesh? navMesh = GetNavMesh();

            if (navMesh is null || navMesh.Count == 0)
                return TryGetBasicRandomPosition();

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    CNavArea area =
                        navMesh[Random.Shared.Next(navMesh.Count)];

                    float distance = DistanceTo(
                        startPosition,
                        area.Center);

                    if (distance < 5000.0f)
                    {
                        Log(
                            $"Selected nav area {area.ID}, " +
                            $"distance: {distance}");

                        return area.Center;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Random area attempt {i + 1}: {ex.Message}");
                }
            }

            for (int i = 0; i < Math.Min(maxAttempts, navMesh.Count); i++)
            {
                try
                {
                    CNavArea area =
                        navMesh[Random.Shared.Next(navMesh.Count)];

                    if (area.BlockedTeam == 0)
                        return area.Center;
                }
                catch (Exception ex)
                {
                    Log($"Fallback area attempt {i + 1}: {ex.Message}");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log($"GetRandomAccessiblePosition error: {ex.Message}");
            return null;
        }
    }

    private static Vector? TryGetBasicRandomPosition()
    {
        try
        {
            var spawnPoints = GetSpawnPoints();

            if (spawnPoints.Count == 0)
                return null;

            var entity =
                spawnPoints[Random.Shared.Next(spawnPoints.Count)];

            Vector? position = entity.AbsOrigin;

            if (position is not Vector pos)
                return null;

            return new Vector(
                pos.X + Random.Shared.Next(-500, 500),
                pos.Y + Random.Shared.Next(-500, 500),
                pos.Z + 50);
        }
        catch (Exception ex)
        {
            Log($"Basic random position error: {ex.Message}");
            return null;
        }
    }

    public static CNavArea? GetClosestNavArea(
        Vector position,
        float maximumDistance = -1)
    {
        try
        {
            CNavMesh? navMesh = GetNavMesh();

            if (navMesh is null)
                return null;

            float closestDistance = float.MaxValue;
            CNavArea? closest = null;

            foreach (CNavArea area in navMesh)
            {
                try
                {
                    float distance = DistanceTo(position, area.Center);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = area;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error checking nav area: {ex.Message}");
                }
            }

            if (maximumDistance > 0 &&
                closestDistance > maximumDistance)
            {
                return null;
            }

            return closest;
        }
        catch (Exception ex)
        {
            Log($"GetClosestNavArea error: {ex.Message}");
            return null;
        }
    }

    public static bool IsAreaAccessible(
        CNavArea startNavArea,
        CNavArea? goalNavArea = null,
        Vector? startPosition = null,
        Vector? goalPosition = null)
    {
        if (goalNavArea is null)
            return true;

        if (goalNavArea.BlockedTeam != 0)
            return false;

        return DistanceTo(
            startNavArea.Center,
            goalNavArea.Center) < 3000.0f;
    }

    private static List<CBaseEntity> GetSpawnPoints()
    {
        var entities = new List<CBaseEntity>();

        if (_plugin is null)
            return entities;

        foreach (string name in new[]
        {
            "info_player_terrorist",
            "info_player_counterterrorist",
            "info_player_start"
        })
        {
            try
            {
                entities.AddRange(
                    _core!.EntitySystem.GetAllEntitiesByDesignerName<CBaseEntity>(name));
            }
            catch (Exception ex)
            {
                Log($"Failed to find {name}: {ex.Message}");
            }
        }

        return entities;
    }

    private static float DistanceTo(Vector a, Vector b)
    {
        float x = a.X - b.X;
        float y = a.Y - b.Y;
        float z = a.Z - b.Z;

        return MathF.Sqrt(x * x + y * y + z * z);
    }
}

public sealed class CNavMesh : IReadOnlyCollection<CNavArea>
{
    public nint Handle { get; }

    public CNavMesh(nint pointer)
    {
        Handle = pointer;
    }

    public int Count => Marshal.ReadInt32(Handle + 8);

    public CNavArea this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            nint navAreas = Marshal.ReadIntPtr(Handle + 16);
            nint areaAddress = Marshal.ReadIntPtr(navAreas + index * 8);

            if (areaAddress == 0)
                throw new InvalidOperationException(
                    "Nav area pointer is null.");

            return new CNavArea(areaAddress);
        }
    }

    public IEnumerator<CNavArea> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class CNavArea
{
    public nint Handle { get; }

    public CNavArea(nint pointer)
    {
        Handle = pointer;
    }

    public Vector Center => ReadVector(Handle + 12);
    public Vector Min => ReadVector(Handle + 36);
    public Vector Max => ReadVector(Handle + 48);

    private static Vector ReadVector(nint address)
    {
        return new Vector(
            Marshal.PtrToStructure<float>(address),
            Marshal.PtrToStructure<float>(address + 4),
            Marshal.PtrToStructure<float>(address + 8)
        );
    }

    public uint ID =>
        unchecked((uint)Marshal.ReadInt32(Handle + 84));

    public byte BlockedTeam =>
        Marshal.ReadByte(Handle + 92);
}

public static class IntPtrExtension
{
    public static nint Rel(this nint address, int offset)
    {
        int relativeOffset = Marshal.ReadInt32(address + offset);

        return address + offset + sizeof(int) + relativeOffset;
    }
}
