
using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Plugins;

namespace HuntDownTheEggsApiTest;

[PluginMetadata(
    Id = "HuntDownTheEggsApiTest",
    Version = "1.0.0",
    Name = "Hunt Down The Eggs API Test",
    Author = "Letaryat",
    Description = "Tests the HuntDownTheEggs shared API"
)]
public sealed class HuntDownTheEggsApiTest : BasePlugin
{
    private const string ApiKey = "HuntDownTheEggs.Api.v1";

    private IHuntDownTheEggsApi? _api;
    private Guid _commandId;
    private Action<EggCollectedEvent>? _eggCollectedHandler;

    public HuntDownTheEggsApiTest(ISwiftlyCore core) : base(core)
    {
    }

    public override void UseSharedInterface(
        IInterfaceManager interfaceManager)
    {
        // Reset references because this callback can run again on reload.
        if (_api is not null && _eggCollectedHandler is not null)
            _api.EggCollected -= _eggCollectedHandler;

        _api = null;
        _eggCollectedHandler = null;

        if (!interfaceManager.TryGetSharedInterface<IHuntDownTheEggsApi>(
                ApiKey, out var api))
        {
            Core.Logger.LogWarning(
                "HuntDownTheEggs API not found. Is the main plugin loaded?");
            return;
        }

        _api = api;

        _eggCollectedHandler = OnEggCollected;
        _api.EggCollected += _eggCollectedHandler;

        Core.Logger.LogInformation(
            "Connected to HuntDownTheEggs API successfully.");
    }

    public override void Load(bool hotReload)
    {
        _commandId = Core.Command.RegisterCommand(
            "apitest",
            OnApiTestCommand,
            false,
            "",
            "Tests the HuntDownTheEggs shared API"
        );

        Core.Logger.LogInformation(
            "HuntDownTheEggsApiTest loaded.");
    }

    private void OnApiTestCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command must be used by a player.");
            return;
        }

        if (_api is null)
        {
            context.Reply(
                "[API Test] HuntDownTheEggs API is not available.");
            return;
        }

        ulong steamId = context.Sender.SteamID;

        int eggs = _api.GetEggCount(steamId);
        int killEggs = _api.GetKillEggCount(steamId);
        int randomEggs = _api.GetRandomEggCount(steamId);

        context.Sender.SendChat(
            $"[API Test] API connected! Eggs: {eggs}, " +
            $"Kill eggs: {killEggs}, Random eggs: {randomEggs}"
        );

        Core.Logger.LogInformation(
            "API test requested by {Player}: eggs={Eggs}, kill={Kill}, random={Random}",
            context.Sender.Name,
            eggs,
            killEggs,
            randomEggs
        );
    }

    private void OnEggCollected(EggCollectedEvent data)
    {
        Core.Logger.LogInformation(
            "[API Test] EggCollected received: player={Player}, steamId={SteamId}, kind={Kind}, eggId={EggId}, map={Map}",
            data.PlayerName,
            data.SteamId,
            data.Kind,
            data.EggId,
            data.Map
        );
    }

    public override void Unload()
    {
        if (_api is not null && _eggCollectedHandler is not null)
            _api.EggCollected -= _eggCollectedHandler;

        Core.Command.UnregisterCommand(_commandId);

        Core.Logger.LogInformation(
            "HuntDownTheEggsApiTest unloaded.");
    }
}
