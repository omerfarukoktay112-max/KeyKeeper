namespace KeyKeeper.Core.Data;

/// <summary>A vault tag, as returned by <see cref="ITagRepository"/>.</summary>
public sealed record TagRecord(string Id, string Name);
