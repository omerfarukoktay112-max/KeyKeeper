namespace KeyKeeper.Core.Models;

/// <summary>
/// The kind of a vault item. Stored as a plain integer in <c>VaultItems.ItemType</c>
/// (see <c>KeyKeeper.Core.Data.VaultSchema</c>) — the type-specific fields themselves live
/// inside each item's encrypted JSON payload, not as separate columns, so adding a new type
/// or field never requires a schema migration.
/// </summary>
public enum VaultItemType
{
    Login = 0,
    Card = 1,
    WiFi = 2,
    Note = 3,
    License = 4,
}
