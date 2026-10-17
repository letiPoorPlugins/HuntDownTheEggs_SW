using HuntDownTheEggs.Api;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;
using ShopCore.Contract;

namespace HuntDownTheEggs_ShopCorePoints_SW;

public sealed partial class HuntDownTheEggs_ShopCorePoints_SW
{
    private bool GiveReward(ulong steamId, string playerName, string rewardName, string rewardValue)
    {
        var player = Core.PlayerManager.GetPlayerFromSteamId(steamId);
        if (player == null) return false;
        if (!decimal.TryParse(rewardValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture,
            out var amount))
        {
            Core.Logger.LogWarning("[ShopPoints] Invalid credit amount: {Value}", rewardValue);
            return false;
        }
        return _shopApi!.AddCredits(player, amount);
    }
}