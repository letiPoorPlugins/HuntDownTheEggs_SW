using System.Text.Json;
using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using ShopCore.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HuntDownTheEggs_ShopCorePoints_SW;

[PluginMetadata(Id = "HuntDownTheEggs_ShopCorePoints_SW", Version = "1.0.0", Name = "HuntDownTheEggs_ShopCorePoints_SW", Author = "Letaryat", Description = "HuntDownTheEggs_ShopCorePoints_SW")]
public partial class HuntDownTheEggs_ShopCorePoints_SW : BasePlugin
{
  private const string ApiKey = "HuntDownTheEggs.Api.v1";
  private const string ShopCoreInterfaceKey = "ShopCore.API.v2";
  private IHuntDownTheEggsApi? _api;
  private IShopCoreApiV2? _shopApi;
  private ShopPointsConfig _config = new();
  private Guid? _registrationId;
  private bool _configLoaded;
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  private string _configPath = string.Empty;

  public HuntDownTheEggs_ShopCorePoints_SW(ISwiftlyCore core) : base(core)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
    DisconnectApi();
    if (!interfaceManager.TryGetSharedInterface<IHuntDownTheEggsApi>(ApiKey, out var api))
    {
      Core.Logger.LogWarning("[ShopCorePoints] HuntDownTheEggs API not found. Make sure the main plugin is loaded.");
      return;
    }

    if (!interfaceManager.TryGetSharedInterface<IShopCoreApiV2>(ShopCoreInterfaceKey, out var apiShop))
    {
      Core.Logger.LogWarning("[ShopCorePoints] ShopCore API not found. Make sure the main plugin is loaded.");
      return;
    }

    _api = api;
    _shopApi = apiShop;

    Core.Logger.LogInformation("[ShopCorePoints] Connected to HuntDownTheEggs API.");

    TryRegisterRewards();
  }

  public override void Load(bool hotReload)
  {
    LoadConfig();

    _configLoaded = true;

    TryRegisterRewards();

    Core.Logger.LogInformation("[ShopCorePoints] Loaded. Config: {ConfigPath}", _configPath);
  }

  public override void Unload()
  {
    DisconnectApi();

    Core.Logger.LogInformation(
        "[ShopCorePoints] Unloaded.");
  }

  private void LoadConfig()
  {
    var pluginConfigBase = Core.Configuration.BasePath;
    var configsRoot = Directory.GetParent(
        pluginConfigBase)?.FullName;

    if (string.IsNullOrWhiteSpace(configsRoot))
    {
      throw new InvalidOperationException("Could not determine the SwiftlyS2 configs directory.");
    }

    var moduleDirectory = Path.Combine(configsRoot, "HuntDownTheEggs", "modules");

    Directory.CreateDirectory(moduleDirectory);

    _configPath = Path.Combine(moduleDirectory, "ShopPoints.jsonc");

    if (!File.Exists(_configPath))
    {
      var defaultJson = JsonSerializer.Serialize(new ShopPointsConfig(), _jsonOptions);

      File.WriteAllText(_configPath, defaultJson);

      Core.Logger.LogInformation("[ShopCorePoints] Created default config: {ConfigPath}", _configPath);
    }

    var json = File.ReadAllText(_configPath);

    _config = JsonSerializer.Deserialize<ShopPointsConfig>(json, _jsonOptions) ?? new ShopPointsConfig();

    Core.Logger.LogInformation(
        "[ShopCorePoints] Config loaded. Enabled={Enabled}, " +
        "PresentType={PresentType}, Rewards={RewardCount}",
        _config.Enabled,
        _config.PresentType,
        _config.Rewards.Count);
  }
  private void TryRegisterRewards()
  {
    if (!_configLoaded || _api is null)
      return;

    if (_registrationId.HasValue)
      return;

    if (!_config.Enabled)
    {
      Core.Logger.LogInformation("[ShopCorePoints] Disabled in configuration.");

      return;
    }

    if (_config.Rewards.Count == 0)
    {
      Core.Logger.LogWarning("[ShopCorePoints] No rewards configured.");

      return;
    }

    try
    {
      _registrationId = _api.RegisterPrizeProvider(
          _config.PresentType,
          _config.ProviderId,
          _config.Rewards,
          GiveReward);

      Core.Logger.LogInformation(
          "[ShopCorePoints] Reward provider registered. " +
          "RegistrationId={RegistrationId}, " +
          "ProviderId={ProviderId}, PresentType={PresentType}, " +
          "RewardCount={RewardCount}",
          _registrationId,
          _config.ProviderId,
          _config.PresentType,
          _config.Rewards.Count);

      foreach (var reward in _config.Rewards)
      {
        Core.Logger.LogInformation("[ShopCorePoints] Reward registered: " + "Name={RewardName}, Value={RewardValue}", reward.Key, reward.Value);
      }
    }
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "[ShopCorePoints] Failed to register reward provider.");
    }
  }
  private void DisconnectApi()
  {
    if (_api is not null)
    {

      if (_registrationId is Guid id)
      {
        _api.UnregisterPrizeProvider(id);

        Core.Logger.LogInformation("[ShopCorePoints] Reward provider unregistered: {Id}", id);
      }
    }
    _registrationId = null;
    _api = null;
  }
}