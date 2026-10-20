using SwiftlyS2.Shared.SchemaDefinitions;
public enum EggKind { Placed, Kill, Random }
public sealed class EggInstance
{
    public required CDynamicProp Prop { get; init; }
    public CTriggerMultiple? Trigger { get; set; }
    public CDynamicProp? Glow { get; set; }
    public required EggKind Kind { get; init; }
    public int EggId { get; init; } = -1;      // Only for eggs that are placed
    public ulong OwnerSteamId { get; init; }   // Only for killEggs
    public bool Collected { get; set; }
}