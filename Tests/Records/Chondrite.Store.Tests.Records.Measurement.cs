// User-editable partial declaration. The schema compiler never overwrites this file.
#nullable enable
using System;
using System.Collections.Immutable;

namespace Chondrite.Store.Tests.Records;

public readonly partial record struct Measurement
{
    public Measurement(
        global::Chondrite.Store.Nonce idNonce,
        global::System.Numerics.Vector3 vecPosition,
        global::Chondrite.Store.Tests.Records.Pascals uPressure,
        ImmutableArray<string> astrTags,
        string? strDescription,
        System.Collections.Immutable.ImmutableArray<byte> abyData,
        global::Chondrite.Core.StorageObjectId idReference,
        ImmutableArray<global::System.Numerics.Vector2> avecBounds,
        global::System.Numerics.Vector4? vecColor,
        ImmutableArray<global::Chondrite.Store.Tests.Records.Pascals> auHistory)
    {
        m_oData = new SchemaData(idNonce, vecPosition, uPressure, astrTags, strDescription, abyData, idReference, avecBounds, vecColor, auHistory);
    }

    // Local state is deliberately outside the persisted SchemaData.
    public string? strLocalLabel { get; init; }

    public global::Chondrite.Store.Nonce idNonce => m_oData.idNonce;
    public global::System.Numerics.Vector3 vecPosition => m_oData.vecPosition;
    public global::Chondrite.Store.Tests.Records.Pascals uPressure => m_oData.uPressure;
    public ImmutableArray<string> astrTags => m_oData.astrTags;
    public string? strDescription => m_oData.strDescription;
    public System.Collections.Immutable.ImmutableArray<byte> abyData => m_oData.abyData;
    public global::Chondrite.Core.StorageObjectId idReference => m_oData.idReference;
    public ImmutableArray<global::System.Numerics.Vector2> avecBounds => m_oData.avecBounds;
    public global::System.Numerics.Vector4? vecColor => m_oData.vecColor;
    public ImmutableArray<global::Chondrite.Store.Tests.Records.Pascals> auHistory => m_oData.auHistory;
}
