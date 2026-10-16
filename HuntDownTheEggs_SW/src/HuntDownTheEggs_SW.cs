using HuntDownTheEggs_SW;
using HuntDownTheEggsEggs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using HuntDownTheEggs.Api;

namespace HuntDownTheEggs;

[PluginMetadata(Id = "HuntDownTheEggs", Version = "1.0.1", Name = "Hunt Down The Eggs", Author = "Letaryat", Description = "Egg hunting game mode - SwiftlyS2 port")]
public sealed partial class HuntDownTheEggsPlugin : BasePlugin
{
  public PluginConfig Config { get; private set; } = new();
  public EventManager EventManager { get; private set; } = null!;
  public DatabaseManager Database { get; private set; } = null!;
  public PlayerManager Players { get; private set; } = null!;
  public EggManager Eggs { get; private set; } = null!;
  public CommandManager CommandManager { get; private set; } = null!;
  public Helpers Helper {get; private set;} = null!;
  public string MapName { get; set; } = string.Empty;
  public bool WarmupRunning { get; set; }
  public HuntDownTheEggsApi Api { get; }
  public HuntDownTheEggsPlugin(ISwiftlyCore core) : base(core)
  {
    Api = new HuntDownTheEggsApi(this);
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
    interfaceManager.AddSharedInterface<IHuntDownTheEggsApi,HuntDownTheEggsApi>("HuntDownTheEggs.Api.v1",Api);
  }

  public override void Load(bool hotReload)
  {
    Core.Configuration
        .InitializeJsonWithModel<PluginConfig>("config.jsonc", "HuntDownTheEggs")
        .Configure(builder => builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true));

    Config = Core.Configuration.Manager.GetSection("HuntDownTheEggs").Get<PluginConfig>() ?? new PluginConfig();

    MapName = TryGetCurrentMap();

    EventManager = new EventManager(this, Core);
    Database = new DatabaseManager(this, Core);
    Players = new PlayerManager(this, Core);
    Eggs = new EggManager(this, Core);
    CommandManager = new CommandManager(this, Core);
    Helper = new Helpers(this, Core);

    Database.Initialize();

    EventManager.RegisterEvents();
    CommandManager.RegisterCommands();

    NavMesh.Init(this, Core);

    if (hotReload && !string.IsNullOrEmpty(MapName))
    {
      Eggs.LoadEggsFromMap();
      Players.LoadTopLists(MapName);
      foreach (var p in Core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
        Players.EnsureLoaded(p.SteamID, p.Controller.PlayerName);
    }

    Core.Logger.LogInformation("HuntDownTheEggs v2.0.0 (SwiftlyS2) loaded");
  }

  public override void Unload()
  {
    EventManager.UnregisterEvents();
    CommandManager.UnRegisterCommands();

    try { Players.SaveAllBlocking(); }
    catch (Exception ex) { Core.Logger.LogError(ex, "HuntDownTheEggs: error while saving players on unload"); }
    Core.Logger.LogInformation("HuntDownTheEggs unloaded");
  }

  public void DebugLog(string message)
  {
    if (Config.Debug) Core.Logger.LogInformation("[HDTE] {Message}", message);
  }
  private string TryGetCurrentMap()
  {
    try { return Core.Engine.GlobalVars.MapName.ToString() ?? string.Empty; }
    catch { return string.Empty; }
  }
}
