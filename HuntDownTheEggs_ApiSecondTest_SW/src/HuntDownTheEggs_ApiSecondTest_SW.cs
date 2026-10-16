
using System.Text.Json;
using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace ShopPoints;

[PluginMetadata(Id = "ShopPoints", Version = "1.0.0", Name = "Hunt Down The Eggs - ShopPoints", Author = "Letaryat", Description = "Example reward provider for HuntDownTheEggs API")]
public sealed partial class ShopPoints : BasePlugin
{
    private const string ApiKey = "HuntDownTheEggs.Api.v1";
    private IHuntDownTheEggsApi? _api;
    private Action<EggCollectedEvent>? _eggCollectedHandler;
    private ShopPointsConfig _config = new();
    private Guid? _registrationId;
    private Guid _commandId;
    private bool _configLoaded;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private string _configPath = string.Empty;

    public ShopPoints(ISwiftlyCore core) : base(core)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        DisconnectApi();
        if (!interfaceManager.TryGetSharedInterface<IHuntDownTheEggsApi>(ApiKey, out var api))
        {
            Core.Logger.LogWarning("[ShopPoints] HuntDownTheEggs API not found. Make sure the main plugin is loaded.");
            return;
        }
        _api = api;
        _eggCollectedHandler = OnEggCollected;
        _api.EggCollected += _eggCollectedHandler;

        Core.Logger.LogInformation("[ShopPoints] Connected to HuntDownTheEggs API.");

        TryRegisterRewards();
    }

    public override void Load(bool hotReload)
    {
        LoadConfig();

        _commandId = Core.Command.RegisterCommand("shoptest", OnShopTestCommand, false, "", "Tests the ShopPoints module and HuntDownTheEggs API");

        _configLoaded = true;

        TryRegisterRewards();

        Core.Logger.LogInformation("[ShopPoints] Loaded. Config: {ConfigPath}", _configPath);
    }

    public override void Unload()
    {
        DisconnectApi();

        Core.Command.UnregisterCommand(_commandId);

        Core.Logger.LogInformation(
            "[ShopPoints] Unloaded.");
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

            Core.Logger.LogInformation("[ShopPoints] Created default config: {ConfigPath}", _configPath);
        }

        var json = File.ReadAllText(_configPath);

        _config = JsonSerializer.Deserialize<ShopPointsConfig>(json, _jsonOptions) ?? new ShopPointsConfig();

        Core.Logger.LogInformation(
            "[ShopPoints] Config loaded. Enabled={Enabled}, " +
            "PresentType={PresentType}, Rewards={RewardCount}",
            _config.Enabled,
            _config.PresentType,
            _config.Rewards.Count);
    }
    private void TryRegisterRewards()
    {
        if (!_configLoaded || _api is null)
            return;

        // Zabezpieczenie przed wielokrotną rejestracją.
        if (_registrationId.HasValue)
            return;

        if (!_config.Enabled)
        {
            Core.Logger.LogInformation("[ShopPoints] Disabled in configuration.");

            return;
        }

        if (_config.Rewards.Count == 0)
        {
            Core.Logger.LogWarning("[ShopPoints] No rewards configured.");

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
                "[ShopPoints] Reward provider registered. " +
                "RegistrationId={RegistrationId}, " +
                "ProviderId={ProviderId}, PresentType={PresentType}, " +
                "RewardCount={RewardCount}",
                _registrationId,
                _config.ProviderId,
                _config.PresentType,
                _config.Rewards.Count);

            foreach (var reward in _config.Rewards)
            {
                Core.Logger.LogInformation("[ShopPoints] Reward registered: " + "Name={RewardName}, Value={RewardValue}", reward.Key, reward.Value);
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[ShopPoints] Failed to register reward provider.");
        }
    }
    private void DisconnectApi()
    {
        if (_api is not null)
        {
            if (_eggCollectedHandler is not null)
                _api.EggCollected -= _eggCollectedHandler;

            if (_registrationId is Guid id)
            {
                _api.UnregisterPrizeProvider(id);

                Core.Logger.LogInformation("[ShopPoints] Reward provider unregistered: {Id}", id);
            }
        }

        _registrationId = null;
        _eggCollectedHandler = null;
        _api = null;
    }
}