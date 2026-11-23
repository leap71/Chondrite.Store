using System.Numerics;
using System.Text;
using Chondrite.Core;
using Chondrite.Store.Serialization;
using Xunit;

namespace Chondrite.Store.Tests;

public class BinarySerializationTests
{
    [Fact]
    public void IntegerAndBooleanEncodingIsLittleEndian()
    {
        StorageBinaryWriter oWriter = new();
        oWriter.WriteBool(false);
        oWriter.WriteBool(true);
        oWriter.WriteInt8(-2);
        oWriter.WriteUInt8(254);
        oWriter.WriteInt16(-2);
        oWriter.WriteUInt16(0x1234);
        oWriter.WriteInt32(-2);
        oWriter.WriteUInt32(0x12345678);
        oWriter.WriteInt64(-2);
        oWriter.WriteUInt64(0x0102030405060708);

        byte[] abyExpected = Convert.FromHexString(
            "0001FEFEFEFF3412FEFFFFFF78563412FEFFFFFFFFFFFFFF0807060504030201");
        Assert.Equal(abyExpected, oWriter.aToArray());

        StorageBinaryReader oReader = new(abyExpected);
        Assert.False(oReader.ReadBool());
        Assert.True(oReader.ReadBool());
        Assert.Equal((sbyte)-2, oReader.ReadInt8());
        Assert.Equal((byte)254, oReader.ReadUInt8());
        Assert.Equal((short)-2, oReader.ReadInt16());
        Assert.Equal((ushort)0x1234, oReader.ReadUInt16());
        Assert.Equal(-2, oReader.ReadInt32());
        Assert.Equal(0x12345678U, oReader.ReadUInt32());
        Assert.Equal(-2L, oReader.ReadInt64());
        Assert.Equal(0x0102030405060708UL, oReader.ReadUInt64());
        Assert.Equal(0, oReader.nRemaining);
        oReader.EnsureEnd();
    }

    [Fact]
    public void FloatingPointEncodingPreservesSignedZeroAndNanPayloads()
    {
        int[] anFloat32Bits =
        [
            0x3FC00000,
            unchecked((int)0x80000000),
            0x7F800000,
            unchecked((int)0xFF800000),
            0x7FC12345
        ];
        long[] anFloat64Bits =
        [
            0x3FF8000000000000,
            unchecked((long)0x8000000000000000),
            0x7FF0000000000000,
            unchecked((long)0xFFF0000000000000),
            0x7FF8123456789ABC
        ];

        StorageBinaryWriter oWriter = new();
        foreach (int nBits in anFloat32Bits)
            oWriter.WriteFloat32(BitConverter.Int32BitsToSingle(nBits));
        foreach (long nBits in anFloat64Bits)
            oWriter.WriteFloat64(BitConverter.Int64BitsToDouble(nBits));

        byte[] abyExpected = Convert.FromHexString(
            "0000C03F000000800000807F000080FF4523C17F" +
            "000000000000F83F0000000000000080000000000000F07F" +
            "000000000000F0FFBC9A78563412F87F");
        Assert.Equal(abyExpected, oWriter.aToArray());

        StorageBinaryReader oReader = new(abyExpected);
        foreach (int nBits in anFloat32Bits)
            Assert.Equal(nBits, BitConverter.SingleToInt32Bits(oReader.ReadFloat32()));
        foreach (long nBits in anFloat64Bits)
            Assert.Equal(nBits, BitConverter.DoubleToInt64Bits(oReader.ReadFloat64()));
        oReader.EnsureEnd();
    }

    [Fact]
    public void VectorsUsePackedComponentsWithoutRuntimePadding()
    {
        Vector2 vec2 = new(1, -2);
        Vector3 vec3 = new(3, 4, -5);
        Vector4 vec4 = new(6, 7, 8, -9);
        StorageBinaryWriter oWriter = new();
        oWriter.WriteVector2f(vec2);
        oWriter.WriteVector3f(vec3);
        oWriter.WriteVector4f(vec4);

        byte[] abyExpected = Convert.FromHexString(
            "0000803F000000C000004040000080400000A0C0" +
            "0000C0400000E04000000041000010C1");
        Assert.Equal(abyExpected, oWriter.aToArray());

        StorageBinaryReader oReader = new(abyExpected);
        Assert.Equal(vec2, oReader.ReadVector2f());
        Assert.Equal(vec3, oReader.ReadVector3f());
        Assert.Equal(vec4, oReader.ReadVector4f());
        oReader.EnsureEnd();
    }

    [Fact]
    public void GuidAndObjectIdentityUseTheirCanonicalByteOrder()
    {
        Guid idGuid = new("00112233-4455-6677-8899-aabbccddeeff");
        byte[] abyObjectId = Convert.FromHexString(
            "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");
        StorageObjectId idObject = new(abyObjectId);
        StorageBinaryWriter oWriter = new();
        oWriter.WriteGuid(idGuid);
        oWriter.WriteObjectId(idObject);

        byte[] abyExpected = Convert.FromHexString(
            "00112233445566778899AABBCCDDEEFF" +
            "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");
        Assert.Equal(abyExpected, oWriter.aToArray());
        StorageBinaryReader oReader = new(abyExpected);
        Assert.Equal(idGuid, oReader.ReadGuid());
        Assert.Equal(idObject, oReader.ReadObjectId());
        oReader.EnsureEnd();
    }

    [Fact]
    public void TextAndBytesHaveUnsigned64BitByteCounts()
    {
        StorageBinaryWriter oWriter = new();
        oWriter.WriteUtf8("A\u00e9\U0001F642");
        oWriter.WriteBytes([0, 128, 255]);
        oWriter.WriteUtf8(string.Empty);
        oWriter.WriteBytes([]);
        byte[] abyExpected = Convert.FromHexString(
            "070000000000000041C3A9F09F9982" +
            "03000000000000000080FF" +
            "00000000000000000000000000000000");
        Assert.Equal(abyExpected, oWriter.aToArray());

        StorageBinaryReader oReader = new(abyExpected);
        Assert.Equal("A\u00e9\U0001F642", oReader.ReadUtf8());
        Assert.Equal(new byte[] { 0, 128, 255 }, oReader.ReadBytes());
        Assert.Equal(string.Empty, oReader.ReadUtf8());
        Assert.Empty(oReader.ReadBytes());
        oReader.EnsureEnd();
    }

    [Fact]
    public void Utf8RejectsInvalidTextAndByteSequences()
    {
        Assert.Throws<ArgumentNullException>(() => new StorageBinaryWriter().WriteUtf8(null!));
        Assert.Throws<EncoderFallbackException>(() => new StorageBinaryWriter().WriteUtf8("\uD800"));
        Assert.Throws<DecoderFallbackException>(() =>
        {
            StorageBinaryReader oReader = new(Convert.FromHexString("0200000000000000C080"));
            _ = oReader.ReadUtf8();
        });
    }

    [Fact]
    public void CountsOutsideDotNetMaterializationRangeAreRejected()
    {
        StorageBinaryReader oReader = new(Convert.FromHexString("FFFFFF7F00000000"));
        Assert.Equal(int.MaxValue, oReader.ReadCount());
        oReader.EnsureEnd();

        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oInvalid = new(Convert.FromHexString("0000008000000000"));
            _ = oInvalid.ReadCount();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oInvalid = new(Convert.FromHexString("FFFFFFFFFFFFFFFF"));
            _ = oInvalid.ReadCount();
        });
    }

    [Fact]
    public void CollectionGuardRejectsBeforeAllocationAndSharesNestedBudget()
    {
        StorageBinaryReader oReader = new(new byte[16],
            new StorageDecodeLimits(nMaxCollectionElements: 3, nMaxMaterializedBytes: 50));
        oReader.GuardCollectionCount(2, 4);
        bool bRejected = false;
        try { oReader.GuardCollectionCount(2, 0); }
        catch (InvalidDataException) { bRejected = true; }
        Assert.True(bRejected);
        oReader.GuardCollectionCount(1, 8);
        oReader.GuardCollectionCount(0, 0);
        bRejected = false;
        try { oReader.GuardCollectionCount(1, 0); }
        catch (InvalidDataException) { bRejected = true; }
        Assert.True(bRejected);
    }

    [Fact]
    public void CollectionGuardChecksRemainingBytesWithoutOverflow()
    {
        StorageBinaryReader oReader = new(new byte[12],
            new StorageDecodeLimits(int.MaxValue, long.MaxValue));
        oReader.GuardCollectionCount(3, 4);
        bool bTooShort = false;
        try { oReader.GuardCollectionCount(1, 13); }
        catch (InvalidDataException) { bTooShort = true; }
        Assert.True(bTooShort);
        bTooShort = false;
        try { oReader.GuardCollectionCount(int.MaxValue, int.MaxValue); }
        catch (InvalidDataException) { bTooShort = true; }
        Assert.True(bTooShort);
        bool bInvalidWidth = false;
        try { oReader.GuardCollectionCount(1, -1); }
        catch (ArgumentOutOfRangeException) { bInvalidWidth = true; }
        Assert.True(bInvalidWidth);
    }

    [Fact]
    public void MaterializedByteLimitsAccumulateAcrossFields()
    {
        StorageBinaryWriter oWriter = new();
        oWriter.WriteUtf8("AB");
        oWriter.WriteBytes([1, 2, 3]);
        byte[] aby = oWriter.aToArray();
        StorageBinaryReader oReader = new(aby, new StorageDecodeLimits(10, 4));
        Assert.Equal("AB", oReader.ReadUtf8());
        bool bRejected = false;
        try { _ = oReader.ReadBytes(); }
        catch (InvalidDataException) { bRejected = true; }
        Assert.True(bRejected);

        StorageBinaryReader oExact = new(aby, new StorageDecodeLimits(10, 5));
        Assert.Equal("AB", oExact.ReadUtf8());
        Assert.Equal(new byte[] { 1, 2, 3 }, oExact.ReadBytes());
        oExact.EnsureEnd();
    }

    [Fact]
    public void MalformedValuesTruncationAndTrailingDataAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new([2]);
            _ = oReader.ReadBool();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(new byte[7]);
            _ = oReader.ReadUInt64();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(new byte[11]);
            _ = oReader.ReadVector3f();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(new byte[31]);
            _ = oReader.ReadObjectId();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(Convert.FromHexString("0400000000000000010203"));
            _ = oReader.ReadBytes();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(Convert.FromHexString("020000000000000041"));
            _ = oReader.ReadUtf8();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new([1]);
            oReader.EnsureEnd();
        });
    }
}
