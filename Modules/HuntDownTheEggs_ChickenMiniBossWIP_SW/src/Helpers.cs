using System.Numerics;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HuntDownTheEggs_ChickenMiniBoss_SW;

public sealed partial class HuntDownTheEggs_ChickenMiniBoss_SW
{
    private Vector3 FindTargetGroup(
        Vector3 chickenPos,
        List<(IPlayer Player, CCSPlayerPawn Pawn, Vector3 Position)> players)
    {
        Vector3 bestCenter = players[0].Position;
        float bestScore = float.MaxValue;

        float radiusSquared = GroupRadius * GroupRadius;

        foreach (var candidate in players)
        {
            Vector3 sum = Vector3.Zero;
            int count = 0;

            foreach (var other in players)
            {
                if (Vector3.DistanceSquared(
                        candidate.Position, other.Position) > radiusSquared)
                    continue;

                sum += other.Position;
                count++;
            }

            if (count == 0)
                continue;

            Vector3 center = sum / count;
            float distanceSquared =
                Vector3.DistanceSquared(chickenPos, center);

            // Im bliższa grupa i im więcej graczy w jej pobliżu,
            // tym lepszy wynik. Odległość pozostaje najważniejsza.
            float groupBonus = 1.0f + (count - 1) * 0.25f;
            float score = distanceSquared / groupBonus;

            if (score < bestScore)
            {
                bestScore = score;
                bestCenter = center;
            }
        }

        return bestCenter;
    }

    private void KnockBackNearbyPlayers(
        CChicken chicken,
        Vector3 chickenPos,
        Vector3 chargeDirection,
        List<(IPlayer Player, CCSPlayerPawn Pawn, Vector3 Position)> players)
    {
        float radiusSquared = ChargeDistance * ChargeDistance;

        foreach (var (player, pawn, position) in players)
        {
            if (!player.IsValid || !pawn.IsValid)
                continue;

            if (Vector3.DistanceSquared(chickenPos, position) > radiusSquared)
                continue;

            // Tylko jedno trafienie na gracza w danej szarży.
            if (!_knockedPlayers.Add(pawn.Address))
                continue;

            Vector3 pushDirection = position - chickenPos;
            pushDirection.Z = 0;

            if (pushDirection.LengthSquared() < 1.0f)
                pushDirection = chargeDirection;
            else
                pushDirection = Vector3.Normalize(pushDirection);

            pawn.BaseVelocity.X = pushDirection.X * KnockbackSpeed;
            pawn.BaseVelocity.Y = pushDirection.Y * KnockbackSpeed;
            pawn.BaseVelocity.Z = KnockbackUpward;
            pawn.BaseVelocityUpdated();

            Core.Logger.LogInformation(
                "[GigaChicken] Hit {Player} for 10 damage.",
                player.Name);
        }
    }
}