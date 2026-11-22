using Chondrite.Core;
using System.Collections.Immutable;

namespace Chondrite.Store;

/// <summary>
/// Convenience operations for publishing schema definitions through an object store.
/// </summary>
public static class StorageTypeRegistration
{
    /// <summary>
    /// Idempotently publishes the supplied canonical schema bytes as an intrinsic schema object.
    /// </summary>
    /// <param name="oStore">The destination object store.</param>
    /// <param name="abyCanonicalSchema">The canonical schema definition, already encoded as bytes.</param>
    /// <returns>The strongly typed identity of the published schema.</returns>
    /// <remarks>
    /// This helper does not parse or validate the schema definition or its dependencies.
    /// It delegates the publication and commit guarantees to <see cref="IObjectStore.idPut"/>.
    /// Publishing identical bytes again returns the same identity without adding another object.
    /// </remarks>
    public static StorageType idRegisterType(
        this IObjectStore oStore,
        ImmutableArray<byte> abyCanonicalSchema)
    {
        ArgumentNullException.ThrowIfNull(oStore);

        if (abyCanonicalSchema.IsDefault)
            throw new ArgumentException(
                "Schema bytes are uninitialized.", nameof(abyCanonicalSchema));

        StorageObject oSchema = new(
            StorageType.idIntrinsicSchema,
            abyCanonicalSchema);

        return new StorageType(oStore.idPut(oSchema));
    }
}
