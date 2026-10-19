using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Numerics;
using SwiftlyS2.Shared.Commands;
using Microsoft.Extensions.Logging;
using System.Threading;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.EntitySystem;

namespace HuntDownTheEggs_ChickenMiniBoss_SW;

[PluginMetadata(Id = "HuntDownTheEggs_ChickenMiniBoss_SW", Version = "1.0.0", Name = "HuntDownTheEggs_ChickenMiniBoss_SW", Author = "Letaryat", Description = "HuntDownTheEggs_ChickenMiniBoss_SW")]
public sealed partial class HuntDownTheEggs_ChickenMiniBoss_SW : BasePlugin
{
  private CChicken? _gigaChicken;
  private ChickenAttacks _chickenAttack;
  private CancellationTokenSource? _chaseTimer;
  private readonly HashSet<nint> _knockedPlayers = new();
  private Dictionary<IPlayer, int> _damageDealt = new();
  private bool _isAiming;
  private int _aimTicksRemaining;
  private Vector3 _chargeDirection;
  private float _accumulatedDamage;
  private int _chargeTicksRemaining;
  private bool _isCharging;

  private readonly List<CancellationTokenSource> _attackTimers = new();
  private bool _chickenDead;
  private uint _gigaChickenIndex;

  private string[] _particleList = [
    "particles/line_attack.vpcf",
    "particles/chicken_bombs.vpcf",
    "particles/double_jump_rings.vpcf",
    "particles/health_burst.vpcf",
    "particles/explosions_fx/explosion_hegrenade_distort.vpcf"
  ];

  private int _particleTest = 0;

  public HuntDownTheEggs_ChickenMiniBoss_SW(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    Core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
    Core.GameEvent.HookPre<EventOtherDeath>(OnOtherDeath);

    Core.Event.OnPrecacheResource += OnPreCacheResources;

    Core.Command.RegisterCommand("kurczak", OnChickenCommand, registerRaw: true);
    Core.Command.RegisterCommand("part", ParticleTestCommand, registerRaw: true);
    Core.GameHooks.Entities.TakeDamage.Pre += OnTakeDamagePre;

  }

  public void ParticleTestCommand(ICommandContext context)
  {
    var args = context.Args;
    var sender = context.Sender;
    if (sender == null) return;
    var pawn = sender.PlayerPawn;
    if (pawn == null) return;
    var pos = pawn.AbsOrigin;
    if (pos == null) return;
    if (args.Length < 1 ||
        !int.TryParse(args[0], out var value))
    {
      context.Reply("Użycie: !particletest <liczba>");
      return;
    }

    _particleTest = value;

    CreateParticle(new Vector3(pos.Value.X, pos.Value.Y, pos.Value.Z), _particleTest);

    context.Reply($"particleTest ustawiono na: {_particleTest}");
  }

  private void OnPreCacheResources(IOnPrecacheResourceEvent @event)
  {
    foreach (var particle in _particleList)
    {
      @event.AddItem(particle);
    }
  }

  private HookResult OnOtherDeath(EventOtherDeath eventObj)
  {
    if (_gigaChicken is null || _chickenDead)
      return HookResult.Continue;

    if (!eventObj.OtherType.Contains(
            "chicken",
            StringComparison.OrdinalIgnoreCase))
    {
      return HookResult.Continue;
    }

    if (eventObj.OtherID != (short)_gigaChickenIndex)
      return HookResult.Continue;

    ResetChicken(announce: true);

    return HookResult.Continue;
  }

  private HookResult OnRoundStart(EventRoundStart eventObj)
  {

    return HookResult.Continue;
  }

  public override void Unload()
  {
    Core.GameHooks.Entities.TakeDamage.Pre -= OnTakeDamagePre;

    ResetChicken();
  }

  private void OnChickenCommand(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender?.Pawn is not { } pawn)
    {
      context.Reply("Ta komenda jest dostępna tylko dla gracza.");
      return;
    }

    if (!pawn.IsValid) return;


    SpawnChicken(pawn);
  }

  private void SpawnChicken(CBasePlayerPawn pawn)
  {
    var entity = Core.EntitySystem.CreateEntityByDesignerName<CChicken>("chicken");

    if (entity is null || !entity.IsValid)
      return;

    var playerPosition = pawn.AbsOrigin!.Value;

    var spawnPosition = new SwiftlyS2.Shared.Natives.Vector(playerPosition.X, playerPosition.Y, playerPosition.Z - 10.0f);

    entity.Teleport(new SwiftlyS2.Shared.Natives.Vector(-570, -666, -208), null, null);
    entity.DispatchSpawn();

    entity.SetScale(20.0f);
    _chaseTimer?.Cancel();

    _gigaChicken = entity;
    _accumulatedDamage = 0;
    _isCharging = false;
    _chargeTicksRemaining = 0;
    _knockedPlayers.Clear();

    entity.Health = 5000;
    entity.HealthUpdated();

    _chaseTimer = Core.Scheduler.RepeatBySeconds(ThinkInterval, () => UpdateChicken(entity));

    entity.CurrentActivity = EChickenActivity.Idle;

    _gigaChicken = entity;
    _gigaChickenIndex = entity.Index;
    _chickenDead = false;

    Core.Logger.LogInformation("[GigaChicken] Spawned chicken at player position.");
  }

  private void OnTakeDamagePre(ref TakeDamageEntityPreContext ctx)
  {
    if (_gigaChicken is not { IsValid: true } chicken) return;

    var damagedEntity = ctx.Params.Entity;

    if (damagedEntity.Address != chicken.Address) return;

    float damage = ctx.Params.Info.Damage;

    if (damage <= 0) return;

    Core.PlayerManager.SendChat($"TEST {damagedEntity.DesignerName} {damagedEntity.Health}");

    _accumulatedDamage += damage;

    if (_accumulatedDamage < DamageToEnrage) return;

    _accumulatedDamage = 0;
    StartRandomAttack();
  }
  private void UpdateChicken(CChicken chicken)
  {
    if (_chickenDead)
      return;

    if (!chicken.IsValid)
    {
      ResetChicken();
      return;
    }

    if (_gigaChicken is not { IsValid: true } current ||
        current.Address != chicken.Address)
    {
      return;
    }


    if (!chicken.IsValid)
    {
      _chaseTimer?.Cancel();
      _chaseTimer = null;
      _gigaChicken = null;
      _isCharging = false;
      _isAiming = false;
      return;
    }

    // Normalny tryb: pozostawiamy AI kurczaka w spokoju.
    if (!_isAiming && !_isCharging) return;

    var chickenOrigin = chicken.AbsOrigin;
    if (chickenOrigin is null) return;

    Vector3 chickenPos = new(chickenOrigin.Value.X, chickenOrigin.Value.Y, chickenOrigin.Value.Z);

    var players = new List<(IPlayer Player, CCSPlayerPawn Pawn, Vector3 Position)>();

    foreach (var player in Core.PlayerManager.GetAlive())
    {
      if (!player.IsValid || player.PlayerPawn is not { IsValid: true } pawn) continue;

      var origin = pawn.AbsOrigin;
      if (origin is null) continue;

      players.Add((player, pawn, new Vector3(origin.Value.X, origin.Value.Y, origin.Value.Z)));
    }

    if (players.Count == 0)
    {
      _isAiming = false;
      _isCharging = false;

      chicken.BaseVelocity.X = 0;
      chicken.BaseVelocity.Y = 0;
      chicken.BaseVelocityUpdated();
      return;
    }
    if (_chickenAttack == ChickenAttacks.Charge)
    {
      if (_isAiming)
      {
        Vector3 target = FindTargetGroup(chickenPos, players);
        Vector3 direction = target - chickenPos;
        direction.Z = 0;

        if (direction.LengthSquared() > 1.0f)
        {
          _chargeDirection = Vector3.Normalize(direction);

          float yaw = MathF.Atan2(_chargeDirection.Y, _chargeDirection.X) * (180.0f / MathF.PI);

          chicken.Teleport(null, new SwiftlyS2.Shared.Natives.QAngle(0, yaw, 0), null);
          chicken.DesiredActivity = EChickenActivity.Idle;
        }

        CreateLineParticle(chickenPos, target);

        _aimTicksRemaining--;

        if (_aimTicksRemaining <= 0)
        {
          _isAiming = false;
          _isCharging = true;
          _chargeTicksRemaining = ChargeDurationTicks;
          _knockedPlayers.Clear();

          Core.Logger.LogInformation("[GigaChicken] Charging!");
        }

        return;
      }

      if (_isCharging)
      {
        chicken.BaseVelocity.X = _chargeDirection.X * ChargeSpeed;
        chicken.BaseVelocity.Y = _chargeDirection.Y * ChargeSpeed;
        chicken.BaseVelocityUpdated();

        chicken.DesiredActivity = EChickenActivity.Run;

        KnockBackNearbyPlayers(chicken, chickenPos, _chargeDirection, players);

        _chargeTicksRemaining--;

        if (_chargeTicksRemaining <= 0) EndCharge(chicken);
      }
    }
  }

  private void ResetChicken(bool announce = false)
  {
    // Zatrzymaj główny timer AI.
    _chaseTimer?.Cancel();
    _chaseTimer = null;

    // Anuluj wszystkie zaplanowane ataki.
    foreach (var timer in _attackTimers)
      timer.Cancel();

    _attackTimers.Clear();

    // Przywróć stan początkowy.
    _isAiming = false;
    _isCharging = false;
    _aimTicksRemaining = 0;
    _chargeTicksRemaining = 0;
    _accumulatedDamage = 0;
    _chargeDirection = Vector3.Zero;
    _chickenAttack = ChickenAttacks.Idle;
    _knockedPlayers.Clear();
    _damageDealt.Clear();

    _chickenDead = true;
    _gigaChicken = null;

    if (announce)
      Core.PlayerManager.SendChat(" \x02[GIGA CHICKEN]\x01 Kurczak został pokonany!");
  }


  private CParticleSystem? CreateParticle(Vector3 position, int _particle)
  {
    if (_particleTest < 0 || _particleTest >= _particleList.Length)
      return null;

    var particle = Core.EntitySystem
        .CreateEntityByDesignerName<CParticleSystem>("info_particle_system");

    if (particle == null || !particle.IsValid)
      return null;

    particle.EffectName = _particleList[_particle];
    particle.StartActive = true;

    particle.Teleport(
        new SwiftlyS2.Shared.Natives.Vector(
            position.X,
            position.Y,
            position.Z
        ),
        new QAngle(0, 0, 0),
        null
    );

    particle.SetScale(50.0f);
    particle.SetScaleAsync(50.0f);
    particle.DispatchSpawn();

    particle.Active = true;
    particle.ActiveUpdated();

    particle.AcceptInput<string>("Start", "", null, null, 0);

    return particle;
  }

  private CParticleSystem? CreateBombParticle(Vector3 position, int particleIndex, Vector3 _color)
  {
    if (particleIndex < 0 || particleIndex >= _particleList.Length)
      return null;

    var particle = Core.EntitySystem
        .CreateEntityByDesignerName<CParticleSystem>("info_particle_system");

    if (particle == null || !particle.IsValid)
      return null;

    // Ustaw efekt przed DispatchSpawn.
    particle.EffectName = _particleList[particleIndex];
    particle.StartActive = false;

    particle.DispatchSpawn();

    if (!particle.IsValid)
      return null;

    particle.Teleport(
        new SwiftlyS2.Shared.Natives.Vector(
            position.X,
            position.Y,
            position.Z
        ),
        new QAngle(0, 0, 0),
        null
    );

    particle.AcceptInput<string>(
        "SetControlPoint",
        value: $"0: {position.X} {position.Y} {position.Z}"
    );

    particle.AcceptInput<string>(
        "SetControlPoint",
        value: "1: 2 0 0"
    );

    particle.AcceptInput<string>(
        "SetControlPoint",
        value: $"2: {_color.X} {_color.Y} {_color.Z}"
    );

    particle.AcceptInput<string>("Start", "", null, null, 0);

    return particle;
  }


  private CParticleSystem? CreateLineParticle(Vector3 position, Vector3 target)
  {
    if (_particleTest < 0 || _particleTest >= _particleList.Length)
      return null;

    var particle = Core.EntitySystem
        .CreateEntityByDesignerName<CParticleSystem>("info_particle_system");

    if (particle == null || !particle.IsValid)
      return null;

    particle.EffectName = _particleList[0];
    particle.StartActive = true;

    particle.Teleport(new SwiftlyS2.Shared.Natives.Vector(position.X, position.Y, position.Z), new QAngle(0, 0, 0), null
    );

    particle.AcceptInput<string>("SetControlPoint", value: $"4: 0 30 0");
    particle.AcceptInput<string>("SetControlPoint", value: $"5: {position.X} {position.Y} {position.Z + 10}");
    particle.AcceptInput<string>("SetControlPoint", value: $"6: {target.X} {target.Y} {target.Z + 10}");

    particle.DispatchSpawn();

    particle.Active = true;
    particle.ActiveUpdated();

    particle.AcceptInput<string>("Start", "", null, null, 0);

    return particle;
  }

}