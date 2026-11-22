using System.Collections.Immutable;
using System.Numerics;
using System.Text;
using Chondrite.Core;
using Chondrite.Store.Serialization;
using Chondrite.Store.Tests.Records;
using Xunit;

namespace Chondrite.Store.Tests;

public sealed class GeneratedRecordTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublishedCompilerGeneratesRichRecordsThatRoundTrip(bool bPresent)
    {
        Measurement oExpected = oCreateMeasurement(bPresent);

        StorageObject oObject = Measurement.oSerialize(in oExpected);
        Measurement oActual = Measurement.oDeserialize(oObject);

        Assert.Equal(Measurement.idSchema, oObject.idType);
        AssertSamePersistentValues(oExpected, oActual);
        Assert.Equal(oObject.idCalculateIdentity(),
            Measurement.oSerialize(in oActual).idCalculateIdentity());
    }

    [Fact]
    public void GenericRecordContractPublishesDependenciesAndDeduplicates()
    {
        TestObjectStore oStore = new();
        Measurement oValue = oCreateMeasurement(bPresent: true);
        StorageObjectId idPredecessorHint = oValue.idReference;

        StorageObjectId idObject = oStore.idPut(in oValue, idPredecessorHint);

        Assert.Collection(oStore.aidPublications,
            id => Assert.Equal(Pascals.idSchema.idSchema, id),
            id => Assert.Equal(Measurement.idSchema.idSchema, id),
            id => Assert.Equal(idObject, id));
        Assert.Equal(idPredecessorHint, oStore.idLastPredecessorHint);
        Assert.Equal(Measurement.idSchema, oStore.oGet(idObject)!.idType);
        AssertSamePersistentValues(oValue, oStore.oGet<Measurement>(idObject));

        Assert.Equal(idObject, oStore.idPut(in oValue));
        Assert.Equal(idObject, Measurement.idPut(oStore, in oValue));
        Assert.Equal(3, oStore.nCount);
        AssertSamePersistentValues(oValue, Measurement.oGet(oStore, idObject));
    }

    [Fact]
    public void PublishedSchemaBytesReproduceGeneratedIdentitiesAndPinDependencies()
    {
        TestObjectStore oStore = new();
        Measurement.EnsureSchema(oStore);

        StorageObject oPascals = Assert.IsType<StorageObject>(
            oStore.oGet(Pascals.idSchema.idSchema));
        StorageObject oMeasurement = Assert.IsType<StorageObject>(
            oStore.oGet(Measurement.idSchema.idSchema));

        Assert.Equal(StorageType.idIntrinsicSchema, oPascals.idType);
        Assert.Equal(StorageType.idIntrinsicSchema, oMeasurement.idType);
        Assert.Equal(Pascals.idSchema.idSchema, oPascals.idCalculateIdentity());
        Assert.Equal(Measurement.idSchema.idSchema, oMeasurement.idCalculateIdentity());

        const string strExpectedPascals =
            "chondrite-schema 1\nschema Chondrite.Store.Tests.Records.Pascals\n" +
            "{\n    0: fValue float64\n}\n";
        string strExpectedMeasurement =
            "chondrite-schema 1\nschema Chondrite.Store.Tests.Records.Measurement\n" +
            "{\n    0: idNonce nonce\n    1: vecPosition vector3f\n" +
            $"    2: uPressure @{Pascals.idSchema.idSchema}\n" +
            "    3: astrTags utf8[]\n    4: strDescription optional<utf8>\n" +
            "    5: abyData bytes\n    6: idReference objectid\n" +
            "    7: avecBounds vector2f[2]\n    8: vecColor optional<vector4f>\n" +
            $"    9: auHistory @{Pascals.idSchema.idSchema}[]\n}}\n";

        Assert.Equal(strExpectedPascals, Encoding.UTF8.GetString(oPascals.abyData.AsSpan()));
        Assert.Equal(strExpectedMeasurement,
            Encoding.UTF8.GetString(oMeasurement.abyData.AsSpan()));
        Assert.Equal(2, oStore.nCount);
    }

    [Fact]
    public void SchemaPublicationIsIdempotentAcrossIndependentStores()
    {
        TestObjectStore oFirst = new();
        TestObjectStore oSecond = new();

        Measurement.EnsureSchema(oFirst);
        Measurement.EnsureSchema(oFirst);
        Measurement.EnsureSchema(oSecond);

        Assert.Equal(2, oFirst.nCount);
        Assert.Equal(2, oSecond.nCount);
        Assert.Equal(oFirst.aidPublications, oSecond.aidPublications);
        Assert.Equal(Measurement.idSchema,
            oSecond.idRegisterType(oFirst.oGet(Measurement.idSchema.idSchema)!.abyData));
        Assert.Equal(2, oSecond.nCount);
    }

    [Fact]
    public void UserFacingMembersAreIndependentOfPersistedState()
    {
        Measurement oOriginal = oCreateMeasurement(bPresent: true);
        Measurement oLabeled = oOriginal with { strLocalLabel = "display-only label" };
        StorageObject oOriginalObject = Measurement.oSerialize(in oOriginal);
        StorageObject oLabeledObject = Measurement.oSerialize(in oLabeled);

        Assert.Equal(oOriginalObject.idCalculateIdentity(), oLabeledObject.idCalculateIdentity());
        Assert.Null(Measurement.oDeserialize(oLabeledObject).strLocalLabel);
        Assert.Equal("display-only label", oLabeled.strLocalLabel);
    }

    [Fact]
    public void GeneratedDeserializerRequiresTheExactSchema()
    {
        Pascals uPressure = new(101325.0);
        StorageObject oOtherType = Pascals.oSerialize(in uPressure);

        Assert.Throws<InvalidDataException>(() => Measurement.oDeserialize(oOtherType));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedDeserializerRejectsTruncatedAndTrailingPayload(bool bTrailing)
    {
        Measurement oValue = oCreateMeasurement(bPresent: true);
        StorageObject oObject = Measurement.oSerialize(in oValue);
        byte[] abyInvalid = bTrailing
            ? [.. oObject.abyData, 0]
            : oObject.abyData.AsSpan(0, oObject.abyData.Length - 1).ToArray();
        StorageObject oInvalid = new(oObject.idType, ImmutableArray.CreateRange(abyInvalid));

        Assert.Throws<InvalidDataException>(() => Measurement.oDeserialize(oInvalid));
    }

    [Fact]
    public void GeneratedWriterChecksFixedArrayLengths()
    {
        Measurement oValue = oCreateMeasurement(bPresent: true, avecBounds: []);

        Assert.Throws<InvalidDataException>(() => Measurement.oSerialize(in oValue));
    }

    [Fact]
    public void GenericGetRejectsTheWrongTypeAndReportsMissingObjects()
    {
        TestObjectStore oStore = new();
        Measurement oValue = oCreateMeasurement(bPresent: false);
        StorageObjectId idObject = oStore.idPut(in oValue);

        Assert.Throws<InvalidDataException>(() => oStore.oGet<Pascals>(idObject));
        Assert.Throws<KeyNotFoundException>(() => oStore.oGet<Measurement>(default));
        Assert.Throws<KeyNotFoundException>(() => Measurement.oGet(oStore, default));
    }

    private static Measurement oCreateMeasurement(
        bool bPresent,
        ImmutableArray<Vector2>? avecBounds = null)
        => new(
            new Nonce(new Guid("00112233-4455-4677-8899-aabbccddeeff")),
            new Vector3(1.25f, -2.5f, 3.75f),
            new Pascals(101325.125),
            ["pressure", "温度", ""],
            bPresent ? "café / nozzle Δp" : null,
            [0, 1, 127, 128, 255],
            new StorageObjectId(Convert.FromHexString(
                "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20")),
            avecBounds ?? [new Vector2(-1f, -2f), new Vector2(3f, 4f)],
            bPresent ? new Vector4(0.25f, 0.5f, 0.75f, 1f) : null,
            [new Pascals(100000.5), new Pascals(101325.125)]);

    private static void AssertSamePersistentValues(Measurement oExpected, Measurement oActual)
    {
        Assert.Equal(oExpected.idNonce, oActual.idNonce);
        Assert.Equal(oExpected.vecPosition, oActual.vecPosition);
        Assert.Equal(oExpected.uPressure.fValue, oActual.uPressure.fValue);
        Assert.Equal(oExpected.astrTags.ToArray(), oActual.astrTags.ToArray());
        Assert.Equal(oExpected.strDescription, oActual.strDescription);
        Assert.Equal(oExpected.abyData.ToArray(), oActual.abyData.ToArray());
        Assert.Equal(oExpected.idReference, oActual.idReference);
        Assert.Equal(oExpected.avecBounds.ToArray(), oActual.avecBounds.ToArray());
        Assert.Equal(oExpected.vecColor, oActual.vecColor);
        Assert.Equal(oExpected.auHistory.ToArray(), oActual.auHistory.ToArray());
    }
}
