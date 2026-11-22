using Chondrite.Core;

namespace Chondrite.Store.Tests;

/// <summary>
/// Test-only store used to exercise the public contracts without shipping a backend.
/// </summary>
internal sealed class TestObjectStore : IObjectStore
{
    private readonly Dictionary<StorageObjectId, StorageObject> m_aObjects = [];
    private readonly List<StorageObjectId> m_aidPublications = [];

    public IReadOnlyList<StorageObjectId> aidPublications => m_aidPublications;
    public int nCount => m_aObjects.Count;
    public StorageObjectId? idLastPredecessorHint { get; private set; }

    public StorageObjectId idPut(
        StorageObject oObject,
        StorageObjectId? idPredecessorHint = null)
    {
        ArgumentNullException.ThrowIfNull(oObject);
        StorageObjectId idObject = oObject.idCalculateIdentity();

        if (oObject.idType != StorageType.idIntrinsicSchema &&
            (!m_aObjects.TryGetValue(oObject.idType.idSchema, out StorageObject? oSchema) ||
             oSchema.idType != StorageType.idIntrinsicSchema))
            throw new InvalidOperationException("The exact schema must be published first.");

        if (m_aObjects.TryGetValue(idObject, out StorageObject? oExisting))
        {
            if (oExisting.idType != oObject.idType ||
                !oExisting.abyData.AsSpan().SequenceEqual(oObject.abyData.AsSpan()))
                throw new InvalidDataException("An object identity collision was detected.");
        }
        else
        {
            m_aObjects.Add(idObject, oObject);
            m_aidPublications.Add(idObject);
        }

        idLastPredecessorHint = idPredecessorHint;
        return idObject;
    }

    public StorageObject? oGet(StorageObjectId idObject)
        => m_aObjects.GetValueOrDefault(idObject);

    public bool bContains(StorageObjectId idObject)
        => m_aObjects.ContainsKey(idObject);
}
