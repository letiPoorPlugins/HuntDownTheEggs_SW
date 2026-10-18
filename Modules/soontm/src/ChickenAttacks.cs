using System.Numerics;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HuntDownTheEggs_ChickenMiniBoss_SW;

public sealed partial class HuntDownTheEggs_ChickenMiniBoss_SW
{
    private void StartRandomAttack()
    {
        if (_gigaChicken is not { IsValid: true })
            return;

        if (_isCharging || _isAiming || _chickenAttack != ChickenAttacks.Idle)
            return;

        var attacks = new[]
        {
                    ChickenAttacks.EggBombardment,
        /*
        ChickenAttacks.Charge,
        ChickenAttacks.Stomp,
        ChickenAttacks.Peck,
        ChickenAttacks.Bombs,

        ChickenAttacks.GroundWave,
        ChickenAttacks.EggSniper
        */
    };

        var attack = attacks[Random.Shared.Next(attacks.Length)];

        Core.PlayerManager.SendChat($"Attack: {attack}");

        switch (attack)
        {
            case ChickenAttacks.Charge:
                StartCharge();
                break;

            case ChickenAttacks.Stomp:
                StartStomp();
                break;

            case ChickenAttacks.Peck:
                StartPeck();
                break;

            case ChickenAttacks.Bombs:
                StartBombAttack();
                break;
            case ChickenAttacks.EggBombardment:
                StartEggBombardment();
                break;

            case ChickenAttacks.GroundWave:
                StartGroundWave();
                break;

            case ChickenAttacks.EggSniper:
                StartEggSniper();
                break;

        }
    }

    private void StartCharge()
    {
        if (_gigaChicken is not { IsValid: true } || _isCharging || _isAiming) return;

        _isAiming = true;
        _isCharging = false;
        _aimTicksRemaining = AimDurationTicks;
        _chargeTicksRemaining = ChargeDurationTicks;

        _knockedPlayers.Clear();

        _chickenAttack = ChickenAttacks.Charge;

        Core.Logger.LogInformation("[GigaChicken] Target acquired! Preparing to charge.");
        _gigaChicken.Render.R = 255;

    }
    private void EndCharge(CChicken chicken)
    {
        chicken.BaseVelocity.X = 0;
        chicken.BaseVelocity.Y = 0;
        chicken.BaseVelocityUpdated();

        _isCharging = false;
        _isAiming = false;
        _aimTicksRemaining = 0;
        _chargeTicksRemaining = 0;

        chicken.DesiredActivity = EChickenActivity.Walk;

        _chickenAttack = ChickenAttacks.Idle;

        Core.Logger.LogInformation("[GigaChicken] Charge ended.");
        _gigaChicken!.Render.R = 0;
    }


    private void StartStomp()
    {
        if (_gigaChicken is not { IsValid: true } chicken)
            return;

        _chickenAttack = ChickenAttacks.Stomp;

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Kurczak przygotowuje uderzenie!");

        ScheduleChickenAttack(1.5f, chicken, () =>
        {
            if (!chicken.IsValid)
                return;

            var origin = chicken.AbsOrigin;
            if (origin is null)
            {
                _chickenAttack = ChickenAttacks.Idle;
                return;
            }

            Vector3 center = new(
                origin.Value.X,
                origin.Value.Y,
                origin.Value.Z);

            foreach (var player in Core.PlayerManager.GetAlive())
            {
                if (!player.IsValid ||
                    player.PlayerPawn is not { IsValid: true } pawn)
                    continue;

                var playerOrigin = pawn.AbsOrigin;
                if (playerOrigin is null)
                    continue;

                Vector3 pos = new(
                    playerOrigin.Value.X,
                    playerOrigin.Value.Y,
                    playerOrigin.Value.Z);

                Vector3 offset = pos - center;
                offset.Z = 0;

                if (offset.LengthSquared() > 450f * 450f)
                    continue;

                Vector3 direction = offset.LengthSquared() > 1f
                    ? Vector3.Normalize(offset)
                    : Vector3.UnitX;

                pawn.BaseVelocity.X = direction.X * 650f;
                pawn.BaseVelocity.Y = direction.Y * 650f;
                pawn.BaseVelocity.Z = 500f;
                pawn.BaseVelocityUpdated();

                player.TakeDamage(
                    20f,
                    DamageTypes_t.DMG_GENERIC,
                    chicken,
                    chicken);
            }

            Core.PlayerManager.SendChat(
                " \x02[GIGA CHICKEN]\x01 Uderzenie!");

            _chickenAttack = ChickenAttacks.Idle;
        });
    }

    private void StartPeck()
    {
        if (_gigaChicken is not { IsValid: true } chicken)
            return;

        _chickenAttack = ChickenAttacks.Peck;

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Kurczak szykuje atak dziobem!");

        ScheduleChickenAttack(0.5f, chicken, () =>
        {
            if (!chicken.IsValid)
                return;

            var chickenOrigin = chicken.AbsOrigin;
            if (chickenOrigin is null)
            {
                _chickenAttack = ChickenAttacks.Idle;
                return;
            }

            Vector3 chickenPos = new(
                chickenOrigin.Value.X,
                chickenOrigin.Value.Y,
                chickenOrigin.Value.Z);

            IPlayer? nearestPlayer = null;
            CCSPlayerPawn? nearestPawn = null;
            float nearestDistance = 300f * 300f;

            foreach (var player in Core.PlayerManager.GetAlive())
            {
                if (!player.IsValid ||
                    player.PlayerPawn is not { IsValid: true } pawn)
                    continue;

                var origin = pawn.AbsOrigin;
                if (origin is null)
                    continue;

                Vector3 pos = new(
                    origin.Value.X,
                    origin.Value.Y,
                    origin.Value.Z);

                float distance = Vector3.DistanceSquared(chickenPos, pos);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = player;
                    nearestPawn = pawn;
                }
            }

            if (nearestPlayer is { IsValid: true } target &&
                nearestPawn is { IsValid: true } targetPawn)
            {
                target.TakeDamage(
                    25f,
                    DamageTypes_t.DMG_GENERIC,
                    chicken,
                    chicken);

                Vector3 direction = new(
                    targetPawn.AbsOrigin!.Value.X - chickenPos.X,
                    targetPawn.AbsOrigin.Value.Y - chickenPos.Y,
                    0);

                if (direction.LengthSquared() > 1f)
                {
                    direction = Vector3.Normalize(direction);
                    targetPawn.BaseVelocity.X = direction.X * 400f;
                    targetPawn.BaseVelocity.Y = direction.Y * 400f;
                    targetPawn.BaseVelocity.Z = 180f;
                    targetPawn.BaseVelocityUpdated();
                }
            }

            _chickenAttack = ChickenAttacks.Idle;
        });
    }

    private void StartBombAttack()
    {
        if (_gigaChicken is not { IsValid: true } chicken)
            return;

        _chickenAttack = ChickenAttacks.Bombs;

        var targets = new List<Vector3>();

        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid ||
                player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            var origin = pawn.AbsOrigin;
            if (origin is null)
                continue;

            targets.Add(new Vector3(
                origin.Value.X,
                origin.Value.Y,
                origin.Value.Z));
        }

        if (targets.Count == 0)
        {
            _chickenAttack = ChickenAttacks.Idle;
            return;
        }

        // Ograniczamy liczbę stref wybuchu do trzech.
        targets = targets
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Uwaga! Nadlatują jajka!");

        ScheduleChickenAttack(1.5f, chicken, () =>
        {
            if (!chicken.IsValid)
                return;

            foreach (var targetPos in targets)
            {
                foreach (var player in Core.PlayerManager.GetAlive())
                {
                    if (!player.IsValid ||
                        player.PlayerPawn is not { IsValid: true } pawn)
                        continue;

                    var origin = pawn.AbsOrigin;
                    if (origin is null)
                        continue;

                    Vector3 pos = new(
                        origin.Value.X,
                        origin.Value.Y,
                        origin.Value.Z);

                    if (Vector3.DistanceSquared(pos, targetPos) > 200f * 200f)
                        continue;

                    player.TakeDamage(
                        30f,
                        DamageTypes_t.DMG_GENERIC,
                        chicken,
                        chicken);

                    Vector3 direction = pos - targetPos;
                    direction.Z = 0;

                    if (direction.LengthSquared() > 1f)
                    {
                        direction = Vector3.Normalize(direction);
                        pawn.BaseVelocity.X = direction.X * 300f;
                        pawn.BaseVelocity.Y = direction.Y * 300f;
                        pawn.BaseVelocity.Z = 250f;
                        pawn.BaseVelocityUpdated();
                    }
                }
            }

            _chickenAttack = ChickenAttacks.Idle;
        });
    }

    private void ScheduleChickenAttack(
        float delaySeconds,
        CChicken chicken,
        Action attack)
    {
        CancellationTokenSource? timer = null;

        timer = Core.Scheduler.DelayBySeconds(delaySeconds, () =>
        {
            if (timer != null)
                _attackTimers.Remove(timer);

            if (_chickenDead ||
                _gigaChicken is not { IsValid: true } current ||
                !chicken.IsValid ||
                current.Address != chicken.Address ||
                chicken.Health <= 0)
            {
                return;
            }

            attack();
        });

        _attackTimers.Add(timer);
    }

    private void StartEggBombardment()
    {
        if (_gigaChicken is not { IsValid: true } chicken ||
            _chickenAttack != ChickenAttacks.Idle)
            return;

        var targets = new List<Vector3>();

        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid ||
                player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            var origin = pawn.AbsOrigin;
            if (origin is null)
                continue;

            targets.Add(new Vector3(
                origin.Value.X,
                origin.Value.Y,
                origin.Value.Z));
        }

        if (targets.Count == 0)
            return;

        targets = targets
            .OrderBy(_ => Random.Shared.Next())
            .Take(BombardmentTargets)
            .ToList();

        _chickenAttack = ChickenAttacks.EggBombardment;

        foreach(var target in targets)
        {
            CreateParticle(new Vector3(target.X, target.Y, target.Z));
        }

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Uwaga! Nadlatuje bombardowanie jajkami!");

        ScheduleChickenAttack(BombardmentDelay, chicken, () =>
        {
            if (_chickenAttack != ChickenAttacks.EggBombardment)
                return;

            var damagedPlayers = new HashSet<nint>();
            float radiusSquared = BombardmentRadius * BombardmentRadius;

            foreach (var player in Core.PlayerManager.GetAlive())
            {
                if (!player.IsValid ||
                    player.PlayerPawn is not { IsValid: true } pawn)
                    continue;

                var origin = pawn.AbsOrigin;
                if (origin is null)
                    continue;

                Vector3 playerPos = new(
                    origin.Value.X,
                    origin.Value.Y,
                    origin.Value.Z);

                foreach (var targetPos in targets)
                {
                    Vector3 offset = playerPos - targetPos;
                    offset.Z = 0;

                    if (offset.LengthSquared() > radiusSquared)
                        continue;

                    if (!damagedPlayers.Add(pawn.Address))
                        break;

                    player.TakeDamage(
                        BombardmentDamage,
                        DamageTypes_t.DMG_BLAST,
                        chicken,
                        chicken);

                    Vector3 push = offset.LengthSquared() > 1.0f
                        ? Vector3.Normalize(offset)
                        : Vector3.UnitX;

                    pawn.BaseVelocity.X = push.X * 350.0f;
                    pawn.BaseVelocity.Y = push.Y * 350.0f;
                    pawn.BaseVelocity.Z = 250.0f;
                    pawn.BaseVelocityUpdated();

                    break;
                }
            }

            _chickenAttack = ChickenAttacks.Idle;

            Core.Logger.LogInformation(
                "[GigaChicken] Egg Bombardment completed.");
        });
    }

    private void StartGroundWave()
    {
        if (_gigaChicken is not { IsValid: true } chicken ||
            _chickenAttack != ChickenAttacks.Idle)
            return;

        var origin = chicken.AbsOrigin;
        if (origin is null)
            return;

        Vector3 center = new(
            origin.Value.X,
            origin.Value.Y,
            origin.Value.Z);

        _chickenAttack = ChickenAttacks.GroundWave;

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Nadchodzi fala uderzeniowa! Skacz!");

        ScheduleChickenAttack(GroundWaveWarning, chicken, () =>
        {
            if (_chickenAttack != ChickenAttacks.GroundWave)
                return;

            GroundWaveTick(
                chicken,
                center,
                0.0f,
                new HashSet<nint>());
        });
    }

    private void GroundWaveTick(
        CChicken chicken,
        Vector3 center,
        float elapsed,
        HashSet<nint> hitPlayers)
    {
        if (_chickenAttack != ChickenAttacks.GroundWave)
            return;

        const float tickInterval = 0.1f;

        elapsed += tickInterval;

        float progress = Math.Clamp(
            elapsed / GroundWaveDuration,
            0.0f,
            1.0f);

        float radius = GroundWaveMaxRadius * progress;
        float innerRadius = Math.Max(0.0f, radius - 110.0f);

        float outerSquared = radius * radius;
        float innerSquared = innerRadius * innerRadius;

        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid ||
                player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            if (hitPlayers.Contains(pawn.Address))
                continue;

            var origin = pawn.AbsOrigin;
            if (origin is null)
                continue;

            Vector3 pos = new(
                origin.Value.X,
                origin.Value.Y,
                origin.Value.Z);

            Vector3 offset = pos - center;
            offset.Z = 0;

            float distanceSquared = offset.LengthSquared();

            if (distanceSquared > outerSquared ||
                distanceSquared < innerSquared)
                continue;

            // Skok może pozwolić uniknąć fali.
            var verticalDifference = Math.Abs(
                origin.Value.Z - center.Z);

            if (verticalDifference > 100.0f)
                continue;

            if (!hitPlayers.Add(pawn.Address))
                continue;

            Vector3 direction = offset.LengthSquared() > 1.0f
                ? Vector3.Normalize(offset)
                : Vector3.UnitX;

            player.TakeDamage(
                GroundWaveDamage,
                DamageTypes_t.DMG_BLAST,
                chicken,
                chicken);

            pawn.BaseVelocity.X = direction.X * 500.0f;
            pawn.BaseVelocity.Y = direction.Y * 500.0f;
            pawn.BaseVelocity.Z = 350.0f;
            pawn.BaseVelocityUpdated();
        }

        if (elapsed >= GroundWaveDuration)
        {
            _chickenAttack = ChickenAttacks.Idle;

            Core.Logger.LogInformation(
                "[GigaChicken] Ground Wave completed.");

            return;
        }

        ScheduleChickenAttack(
            tickInterval,
            chicken,
            () => GroundWaveTick(
                chicken,
                center,
                elapsed,
                hitPlayers));
    }

    private void StartEggSniper()
    {
        if (_gigaChicken is not { IsValid: true } chicken ||
            _chickenAttack != ChickenAttacks.Idle)
            return;

        var targets = new List<(IPlayer Player, Vector3 Position)>();

        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid ||
                player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            var origin = pawn.AbsOrigin;
            if (origin is null)
                continue;

            targets.Add((
                player,
                new Vector3(
                    origin.Value.X,
                    origin.Value.Y,
                    origin.Value.Z)));
        }

        if (targets.Count == 0)
            return;

        var selected = targets[Random.Shared.Next(targets.Count)];

        var chickenOrigin = chicken.AbsOrigin;
        if (chickenOrigin is null)
            return;

        Vector3 start = new(
            chickenOrigin.Value.X,
            chickenOrigin.Value.Y,
            chickenOrigin.Value.Z);

        Vector3 direction = selected.Position - start;
        direction.Z = 0;

        if (direction.LengthSquared() < 1.0f)
            return;

        direction = Vector3.Normalize(direction);

        float yaw = MathF.Atan2(
            direction.Y,
            direction.X) * (180.0f / MathF.PI);

        chicken.Teleport(
            null,
            new SwiftlyS2.Shared.Natives.QAngle(0, yaw, 0),
            null);

        _chickenAttack = ChickenAttacks.EggSniper;

        selected.Player.SendCenter(
            "GIGA CHICKEN NAMIERZA CIĘ! ZMIEŃ KIERUNEK!");

        Core.PlayerManager.SendChat(
            " \x02[GIGA CHICKEN]\x01 Przygotowuje precyzyjny strzał!");

        ScheduleChickenAttack(SniperWarning, chicken, () =>
        {
            if (_chickenAttack != ChickenAttacks.EggSniper)
                return;

            float rangeSquared = SniperRange * SniperRange;
            float widthSquared = SniperWidth * SniperWidth;

            foreach (var player in Core.PlayerManager.GetAlive())
            {
                if (!player.IsValid ||
                    player.PlayerPawn is not { IsValid: true } pawn)
                    continue;

                var origin = pawn.AbsOrigin;
                if (origin is null)
                    continue;

                Vector3 pos = new(
                    origin.Value.X,
                    origin.Value.Y,
                    origin.Value.Z);

                Vector3 offset = pos - start;
                float verticalDifference = Math.Abs(offset.Z);
                offset.Z = 0;

                float projection = Vector3.Dot(offset, direction);

                if (projection < 0 || projection * projection > rangeSquared)
                    continue;

                if (verticalDifference > 120.0f)
                    continue;

                float perpendicularSquared =
                    Math.Max(0.0f, offset.LengthSquared() -
                        projection * projection);

                if (perpendicularSquared > widthSquared)
                    continue;

                player.TakeDamage(
                    SniperDamage,
                    DamageTypes_t.DMG_BULLET,
                    chicken,
                    chicken);

                pawn.BaseVelocity.X = direction.X * 400.0f;
                pawn.BaseVelocity.Y = direction.Y * 400.0f;
                pawn.BaseVelocity.Z = 200.0f;
                pawn.BaseVelocityUpdated();
            }

            _chickenAttack = ChickenAttacks.Idle;

            Core.Logger.LogInformation(
                "[GigaChicken] Egg Sniper fired.");
        });
    }


}