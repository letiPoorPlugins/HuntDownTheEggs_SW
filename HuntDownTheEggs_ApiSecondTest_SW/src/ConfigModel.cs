namespace ShopPoints;
public sealed class ShopPointsConfig
{
    public bool Enabled { get; set; } = true;
    public string PresentType { get; set; } = "rare";
    public string ProviderId { get; set; } = "ShopPoints";
    public Dictionary<string, string> Rewards { get; set; } = new()
    {
        ["10000 ShopPoints"] = "10000",
        ["5000 ShopPoints"] = "5000",
        ["1000 ShopPoints"] = "1000"
    };
}