using Chondrite.Core;
using System.Collections.Immutable;

namespace Chondrite.Store;

/// <summary>
/// Bounded page of object identities and a continuation cursor.
/// </summary>
/// <param name="aidObjects">The returned identities, without object payloads.</param>
/// <param name="oNextCursor">
/// Backend-defined continuation position; unchanged if the page is empty.
/// Null is the initial position before the first object.
/// </param>
public readonly record struct StorageTraversalPage(
    ImmutableArray<StorageObjectId> aidObjects,
    StorageCursor? oNextCursor)
{
    /// <summary>Maximum number of identities returned by a single traversal request.</summary>
    public const int c_nMaxPageSize = 4096;
}
