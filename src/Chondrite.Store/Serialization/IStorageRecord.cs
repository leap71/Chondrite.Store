using Chondrite.Core;
namespace Chondrite.Store.Serialization;

/// <summary>
/// Compile-time contract implemented by generated, strongly typed persistence records.
/// The underlying immutable schema is independent of this C# representation.
/// </summary>
/// <typeparam name="TSelf">The implementing record type itself.</typeparam>
public interface IStorageRecord<TSelf>
    where TSelf : IStorageRecord<TSelf>
{
    /// <summary>Identity of this type's exact published schema.</summary>
    static abstract StorageType idSchema { get; }

    /// <summary>Idempotently publishes the schema and its prerequisites.</summary>
    static abstract void EnsureSchema(IObjectStore oStore);

    /// <summary>Serializes a value into an immutable, schema-typed storage object.</summary>
    static abstract StorageObject oSerialize(in TSelf oValue);

    /// <summary>Reads a storage object using the exact schema of this type.</summary>
    static abstract TSelf oDeserialize(StorageObject oObject);
}

/// <summary>
/// Generic operations available for any generated persistence record.
/// No reflection, boxing, or runtime codec registry is required.
/// </summary>
public static class StorageRecordExtensions
{
    /// <summary>Publishes a strongly typed record, ensuring its schema is present first.</summary>
    public static StorageObjectId idPut<T>(
        this IObjectStore oStore,
        in T oValue,
        StorageObjectId? idPredecessorHint = null)
        where T : IStorageRecord<T>
    {
        ArgumentNullException.ThrowIfNull(oStore);
        T.EnsureSchema(oStore);
        return oStore.idPut(T.oSerialize(in oValue), idPredecessorHint);
    }

    /// <summary>Retrieves and deserializes a record of the requested schema.</summary>
    public static T oGet<T>(this IObjectStore oStore, StorageObjectId idObject)
        where T : IStorageRecord<T>
    {
        ArgumentNullException.ThrowIfNull(oStore);
        StorageObject oObject = oStore.oGet(idObject) ?? throw new KeyNotFoundException();
        return T.oDeserialize(oObject);
    }
}
