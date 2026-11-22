using System.Collections.Immutable;

namespace Chondrite.Store;

/// <summary>
/// Opaque position in a storage backend's bounded traversal or indexed query.
/// </summary>
/// <remarks>
/// The bytes have meaning only to the storage backend that issued the cursor. Consumers
/// may persist and later restore them, but must not interpret or modify them. A cursor
/// is bound to the issuing store and the traversal or query that created it; for
/// type-filtered queries it is additionally bound to the selected type.
/// An invalid or foreign cursor must be rejected rather than silently restarting.
    /// The default value is uninitialized and is not a valid cursor; use null to start traversal.
/// </remarks>
public readonly struct StorageCursor : IEquatable<StorageCursor>
{
    private readonly ImmutableArray<byte> m_abyData;

    /// <summary>Creates an opaque cursor from a serialized representation.</summary>
    /// <remarks>The supplied bytes are copied to prevent subsequent mutation.</remarks>
    public StorageCursor(ReadOnlySpan<byte> abyData)
    {
        if (abyData.IsEmpty)
            throw new ArgumentException("A storage cursor cannot be empty.", nameof(abyData));
        m_abyData = ImmutableArray.CreateRange(abyData.ToArray());
    }

    /// <summary>Gets the opaque cursor bytes for storage or transmission.</summary>
    public ReadOnlySpan<byte> abyData => m_abyData.AsSpan();

    /// <summary>Compares the complete opaque byte representations.</summary>
    public bool Equals(StorageCursor oOther) => abyData.SequenceEqual(oOther.abyData);

    /// <inheritdoc/>
    public override bool Equals(object? oObject) =>
        oObject is StorageCursor oOther && Equals(oOther);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode oHash = new();
        foreach (byte byValue in abyData)
            oHash.Add(byValue);
        return oHash.ToHashCode();
    }

    /// <summary>Tests whether two cursors have identical bytes.</summary>
    public static bool operator ==(StorageCursor oLeft, StorageCursor oRight) => oLeft.Equals(oRight);
    /// <summary>Tests whether two cursors have different bytes.</summary>
    public static bool operator !=(StorageCursor oLeft, StorageCursor oRight) => !oLeft.Equals(oRight);
}
