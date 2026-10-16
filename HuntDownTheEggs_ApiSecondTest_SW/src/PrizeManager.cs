using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace ShopPoints;

public sealed partial class ShopPoints
{
    private bool GiveReward(ulong steamId, string playerName, string rewardName, string rewardValue)
    {
        // To wywołuje główny HuntDownTheEggs, kiedy
        // nagroda tego modułu zostanie wylosowana.
        Core.Logger.LogInformation(
            "[ShopPoints] ===== REWARD CALLBACK =====");

        Core.Logger.LogInformation(
            "[ShopPoints] PlayerName: {PlayerName}",
            playerName);

        Core.Logger.LogInformation(
            "[ShopPoints] SteamID: {SteamId}",
            steamId);

        Core.Logger.LogInformation(
            "[ShopPoints] RewardName: {RewardName}",
            rewardName);

        Core.Logger.LogInformation(
            "[ShopPoints] RewardValue: {RewardValue}",
            rewardValue);

        Core.Logger.LogInformation(
            "[ShopPoints] ProviderId: {ProviderId}",
            _config.ProviderId);

        Core.Logger.LogInformation(
            "[ShopPoints] PresentType: {PresentType}",
            _config.PresentType);

        Core.Logger.LogInformation(
            "[ShopPoints] TEST MODE: no points were awarded.");

        return true;
    }

    private void OnEggCollected(EggCollectedEvent data)
    {
        Core.Logger.LogInformation(
            "[ShopPoints] ===== EGG COLLECTED =====");

        Core.Logger.LogInformation(
            "[ShopPoints] PlayerName: {PlayerName}",
            data.PlayerName);

        Core.Logger.LogInformation(
            "[ShopPoints] SteamID: {SteamId}",
            data.SteamId);

        Core.Logger.LogInformation(
            "[ShopPoints] EggId: {EggId}",
            data.EggId?.ToString() ?? "null");

        Core.Logger.LogInformation(
            "[ShopPoints] Map: {Map}",
            data.Map);

        Core.Logger.LogInformation(
            "[ShopPoints] CollectionKind: {Kind}",
            data.Kind);
    }

    private void OnShopTestCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply(
                "[ShopPoints] This command must be used by a player.");

            return;
        }

        if (_api is null)
        {
            context.Reply(
                "[ShopPoints] HuntDownTheEggs API is unavailable.");

            return;
        }

        context.Sender.SendChat(
            $"[ShopPoints] API connected! " +
            $"Enabled: {_config.Enabled}, " +
            $"Type: {_config.PresentType}, " +
            $"Rewards: {_config.Rewards.Count}");

        Core.Logger.LogInformation(
            "[ShopPoints] Test command used by {Player}. " +
            "Enabled={Enabled}, PresentType={PresentType}, " +
            "RewardCount={RewardCount}, Registered={Registered}",
            context.Sender.Name,
            _config.Enabled,
            _config.PresentType,
            _config.Rewards.Count,
            _registrationId.HasValue);
    }
}