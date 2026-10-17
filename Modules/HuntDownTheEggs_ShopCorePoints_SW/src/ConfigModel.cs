namespace HuntDownTheEggs_ShopCorePoints_SW;

public sealed class ShopPointsConfig
{
    public bool Enabled { get; set; } = true;

    public string ProviderId { get; set; } = "ShopPoints";

    public Dictionary<string, Dictionary<string, string>> Rewards { get; set; } = new()
    {
        ["rare"] = new()
        {
            ["100 Shop Points"] = "100",
            ["500 Shop Points"] = "500"
        },
        ["common"] = new()
        {
            ["1000 Shop Points"] = "1000"
        }
    };
}