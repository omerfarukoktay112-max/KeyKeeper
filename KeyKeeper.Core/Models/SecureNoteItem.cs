namespace KeyKeeper.Core.Models;

/// <summary>Free-form encrypted text. No field here is singled out as "the" secret -- the whole
/// point of a secure note is that <see cref="Content"/> itself is arbitrary sensitive text -- so,
/// unlike the other item types, nothing in the UI layer masks it by default.</summary>
public sealed record SecureNoteItem(string Title, string Content) : IVaultItemPayload
{
    public static VaultItemType ItemType => VaultItemType.Note;
}
