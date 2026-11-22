using Chondrite.Core;
namespace Chondrite.Store;

/// <summary>
/// Stores replaceable, local operational state under stable GUID keys.
/// </summary>
/// <remarks>
/// Values use exactly the same typed, immutable StorageObject representation as IObjectStore,
/// but the stable key is independent of the value's SHA-256 identity. Calling Set replaces the
/// association atomically; it does not modify the StorageObject itself. Previous values are
/// not retained. Local state does not appear in object traversal results and is not automatically
/// replicated. Disk-backed implementations commit Set locally before returning.
/// </remarks>
public interface ILocalStateStore
{
    /// <summary>Gets the current value at a key, or null if the key has no value.</summary>
    StorageObject? oGet(Guid idKey);

    /// <summary>Atomically creates or replaces a value at the specified key.</summary>
    /// <param name="idKey">Stable, caller-defined key.</param>
    /// <param name="oObject">Complete replacement value, with an initialized immutable payload.</param>
    void Set(Guid idKey, StorageObject oObject);
}
