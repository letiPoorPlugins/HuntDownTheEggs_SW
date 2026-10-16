namespace HuntDownTheEggs;

// UWAGA: Microsoft.Extensions.Configuration.Binder ignoruje [JsonPropertyName],
// dlatego nazwy właściwości = nazwy kluczy w config.jsonc.
public sealed class PluginConfig
{
    /// <summary>Nazwa połączenia z database.jsonc SwiftlyS2 (zastępuje DBHost/DBPort/DBUsername/...).</summary>
    public string DatabaseConnection { get; set; } = "hunt_down_the_eggs";

    public EggSetup EggSetup { get; set; } = new();
    public ModesSetup ModesSetup { get; set; } = new();
    public PrizeSetup PrizeSetup { get; set; } = new();
    public CommandsSetup CommandsSetup { get; set; } = new();

    public bool Debug { get; set; } = true;
}

public sealed class EggSetup
{
    /// <summary>Uprawnienie SwiftlyS2 (dawniej flaga CSS "@egg/root").</summary>
    public string EggRootPermission { get; set; } = "hdte.root";
    public string EggModel { get; set; } = "models/chicken/chicken.vmdl";
    public string TriggerModel { get; set; } = "models/chicken/chicken.vmdl";
    public string EggAnimation { get; set; } = "challenge_coin_idle";
    public string EggCollectAnimation { get; set; } = "challenge_coin_collect";
    public float EggModelHeight { get; set; } = 0;
    public float EggModelScale { get; set; } = 1;
    public bool Glowing { get; set; } = true;
    public string GlowingColor { get; set; } = "Red";
    public int GlowingRange { get; set; } = 1500;
    public bool HidePickedEggsForPlayer { get; set; } = false;
    public bool KillEggOnlyForKiller { get; set; } = false;
}

public sealed class ModesSetup
{
    public bool SpawnEggsOnWarmup { get; set; } = false;
    public bool DeathMode { get; set; } = true;
    public bool SearchMode { get; set; } = true;
    public bool ShootEggMode { get; set; } = true;
    public int ShootEggHealth { get; set; } = 100;
    public bool SpawnDeathEggOnVictim { get; set; } = true;
    public float ChanceToSpawn { get; set; } = 100.0f;
    public bool RemoveOnFind { get; set; } = false;
    public bool SpawnPlacedEggsOnce { get; set; } = false;
    public bool SpawnRandomEggs { get; set; } = false;
    public bool SpawnRandomEggsOnWarmup { get; set; } = false;
    public int MinPlayersToSpawnRandomEggs { get; set; } = 1;
    public int NumberOfRandomEggs { get; set; } = 20;
    public bool SpawnRandomEggsByPercentageOfPlayers { get; set; } = true;
}

public sealed class PrizeSetup
{
    public bool ReceivePrize { get; set; } = true;
    public Dictionary<string, EggTypeConfig> PresentTypes { get; set; } = new();
}

public sealed class EggTypeConfig
{
    public float Chance { get; set; } = 20.0f;
    public string Color { get; set; } = "white";
    public Dictionary<string, string> Rewards { get; set; } = new();
}

public sealed class CommandsSetup
{
    public bool EnableCommands { get; set; } = true;

    // Nazwy bez prefiksu "css_" – SwiftlyS2 sam rejestruje wersję konsolową (sw_) i czatową (!).
    public List<string> PlaceEggCommands { get; set; } = ["eggplace"];
    public List<string> TeleportToEggCommands { get; set; } = ["tpegg"];
    public List<string> RemoveEggCommands { get; set; } = ["removeegg"];
    public List<string> ReloadEggsCommands { get; set; } = ["reloadeggs"];
    public List<string> PlacingModeCommands { get; set; } = ["placingmode"];
    public List<string> ShowMyEggsCommands { get; set; } = ["myeggs"];
    public List<string> ShowTopEggsCommands { get; set; } = ["topeggs"];
    public List<string> ShowTopKillEggsCommands { get; set; } = ["topkilleggs"];
    public List<string> ShowTopRandomEggsCommands { get; set; } = ["toprandomeggs"];

}
