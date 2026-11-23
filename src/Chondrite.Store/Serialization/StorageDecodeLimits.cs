namespace Chondrite.Store.Serialization;

/// <summary>
/// Per-top-level-decode resource limits. Defaults permit large file payloads but
/// bound the total number of collection elements. Nested codecs share one reader.
/// Limits do not affect the canonical wire format.
/// </summary>
public readonly record struct StorageDecodeLimits(
    int nMaxCollectionElements,
    long nMaxMaterializedBytes)
{
    /// <summary>Default resource limits for decoding a persisted record.</summary>
    public static StorageDecodeLimits oDefault { get; } =
        new(1_000_000, int.MaxValue);
}
