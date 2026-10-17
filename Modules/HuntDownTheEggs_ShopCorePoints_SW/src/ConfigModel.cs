namespace HuntDownTheEggs_ShopCorePoints_SW;
public sealed class ShopPointsConfig
{
    public bool Enabled { get; set; } = true;
    public string PresentType { get; set; } = "rare";
    public string ProviderId { get; set; } = "ShopPoints";
    public Dictionary<string, string> Rewards { get; set; } = new()
    {
        ["10000 Shop Points"] = "10000",
        ["5000 Shop Points"] = "5000",
        ["1000 Shop Points"] = "1000"
    };
}