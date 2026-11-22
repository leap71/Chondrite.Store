using Chondrite.Core;
namespace Chondrite.Store;

/// <summary>
/// Provides identity-based access to immutable <see cref="StorageObject"/> instances.
/// </summary>
/// <remarks>
/// This contract is independent of filesystem, database, index, and replication details.
/// A successful <see cref="idPut"/> makes its object immediately readable from this store
/// and means the backend has completed its own publication/commit operation. For a
/// persistent backend this must include its required local durability boundary; a memory
/// backend commits only to memory. Callers should not interpret a memory-store acknowledgment
/// as proof of crash durability. Slow backends may be invoked from a separate worker.
/// No operation performs an implicit remote fetch or replication.
/// </remarks>
public interface IObjectStore
{
    /// <summary>Publishes an immutable object and returns its canonical SHA-256 identity.</summary>
    /// <param name="oObject">The object, including immutable schema identity and payload.</param>
    /// <param name="idPredecessorHint">
    /// Optional advisory identity of an object that may have similar contents. A backend may
    /// use it for delta compression or another physical storage optimization, or ignore it.
    /// It is not a lineage, ancestry, or ownership relationship; it does not participate in
    /// object identity or change the object's logical representation. The hinted object is
    /// not required to exist, and its absence must not prevent publication.
    /// </param>
    /// <returns>The result of <see cref="StorageObject.idCalculateIdentity()"/>.</returns>
    /// <remarks>
    /// Publishing identical objects is idempotent. A detected collision between different
    /// objects is an integrity failure, never permission to replace existing content.
    /// An uninitialized type or immutable payload must be rejected. Except for the intrinsic
    /// schema type, the object's type must refer to an already published object in this store
    /// whose own type is <see cref="StorageType.idIntrinsicSchema"/>. Schema definitions are
    /// therefore published before the objects that use them.
    /// </remarks>
    StorageObjectId idPut(StorageObject oObject, StorageObjectId? idPredecessorHint = null);

    /// <summary>Retrieves an object by identity, or returns null if it is not available locally.</summary>
    /// <remarks>Implementations must reject detected corruption instead of returning incorrect bytes.</remarks>
    StorageObject? oGet(StorageObjectId idObject);

    /// <summary>Returns whether an object with the given identity is available locally.</summary>
    bool bContains(StorageObjectId idObject);
}

/// <summary>
/// Optional capability for durable, insertion-ordered traversal of an object store.
/// </summary>
/// <remarks>
/// The ordering is local to each store: it records first successful insertion, not
/// creation timestamps, hashes, or domain ancestry. Repeating idPut for an existing
/// identity does not create another entry. Each request returns a bounded page and an
/// opaque cursor that can be saved and reused after reopening a persistent store.
/// A null cursor starts before the first insertion. An empty page returns the same
/// cursor, allowing subsequent calls to discover later insertions. Cursors cannot be
/// exchanged between stores. A new in-memory store is a new storage instance.
/// The backend must reject invalid or foreign cursors. A page need not be a snapshot
/// of insertions made concurrently with that request; all committed insertions must
/// remain discoverable from an earlier cursor. Consumers should only checkpoint a
/// cursor after processing its returned objects successfully.
/// </remarks>
public interface ITraversableObjectStore : IObjectStore
{
    /// <summary>Returns at most nMaxCount identities inserted after oCursor.</summary>
    /// <param name="oCursor">Opaque previous position, or null to begin.</param>
    /// <param name="nMaxCount">Positive maximum, not exceeding StorageTraversalPage.c_nMaxPageSize.</param>
    StorageTraversalPage xReadNext(StorageCursor? oCursor, int nMaxCount);
}

/// <summary>
/// Optional capability for finding objects of one exact persisted schema type.
/// </summary>
/// <remarks>
/// Both operations filter by exact schema type and use first-successful-local-insertion
/// order, not domain ancestry, creation timestamps, or schema inheritance.
/// xReadByType traverses backward from newest to oldest; restarting from null
/// discovers recently inserted objects. xReadNextByType traverses forward from
/// oldest to newest, allowing incremental consumers to discover later insertions.
/// An empty page retains its cursor. Cursors survive reopening a persistent store
/// and belong to exactly one store, type, and direction; they are not interchangeable
/// with each other or with unfiltered traversal cursors.
/// </remarks>
public interface IIndexedObjectStore : IObjectStore
{
    /// <summary>Returns up to nMaxCount identities of this exact type, newest first.</summary>
    /// <param name="idType">Exact published schema identity to select.</param>
    /// <param name="oCursor">Opaque previous position, or null to start at newest.</param>
    /// <param name="nMaxCount">Positive maximum, not exceeding StorageTraversalPage.c_nMaxPageSize.</param>
    StorageTraversalPage xReadByType(
        StorageType idType,
        StorageCursor? oCursor,
        int nMaxCount);

    /// <summary>Returns up to nMaxCount identities of this exact type, oldest first.</summary>
    /// <param name="idType">Exact published schema identity to select.</param>
    /// <param name="oCursor">Opaque previous position, or null to start before the first insertion.</param>
    /// <param name="nMaxCount">Positive maximum, not exceeding StorageTraversalPage.c_nMaxPageSize.</param>
    /// <remarks>
    /// Intended for incremental index construction. A returned cursor is suitable for
    /// checkpointing only after processing its page successfully. It remains valid across
    /// later insertions, including insertions of other types. Empty pages preserve it.
    /// </remarks>
    StorageTraversalPage xReadNextByType(
        StorageType idType,
        StorageCursor? oCursor,
        int nMaxCount);
}
