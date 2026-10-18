namespace HuntDownTheEggs_ChickenMiniBoss_SW;

public enum ChickenAttacks
{
    Idle = 0,
    Charge = 1,
    Bombs = 2,
    Stomp = 3,
    Peck = 4,
    EggBombardment = 5,
    GroundWave = 6,
    EggSniper = 7,
}
public sealed partial class HuntDownTheEggs_ChickenMiniBoss_SW
{
    private const float ThinkInterval = 0.1f;
    private const float DamageToEnrage = 150.0f;
    private const float ChargeSpeed = 650.0f;
    private const float KnockbackSpeed = 850.0f;
    private const float KnockbackUpward = 800.0f;
    private const float ChargeDistance = 120.0f;
    private const float GroupRadius = 450.0f;
    private const int ChargeDurationTicks = 15;
    private const int AimDurationTicks = 10; // 5 sekund przy ThinkInterval = 0.1s
    private const float ChickenSpeed = 700.0f;
    private const float ChaseInterval = 0.1f;
    private const float BombardmentDelay = 1.5f;
    private const float BombardmentRadius = 220.0f;
    private const float BombardmentDamage = 35.0f;
    private const int BombardmentTargets = 4;

    private const float GroundWaveWarning = 0.8f;
    private const float GroundWaveDuration = 1.5f;
    private const float GroundWaveMaxRadius = 1200.0f;
    private const float GroundWaveDamage = 25.0f;

    private const float SniperWarning = 1.2f;
    private const float SniperRange = 1800.0f;
    private const float SniperWidth = 75.0f;
    private const float SniperDamage = 40.0f;

}