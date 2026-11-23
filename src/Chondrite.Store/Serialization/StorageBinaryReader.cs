using Chondrite.Core;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text;

namespace Chondrite.Store.Serialization;

/// <summary>
/// Bounds-checked reader for the canonical binary format. Count fields are unsigned
/// 64-bit values; a .NET materialization rejects lengths outside its supported range.
/// </summary>
public ref struct StorageBinaryReader
{
    private static readonly UTF8Encoding c_oUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> m_abyData;
    private int m_nOffset;
    private int m_nRemainingCollectionElements;
    private long m_nRemainingMaterializedBytes;

    /// <summary>Reads from the supplied encoded record without copying its bytes.</summary>
    public StorageBinaryReader(ReadOnlySpan<byte> abyData, StorageDecodeLimits? oLimits = null)
    {
        StorageDecodeLimits oEffective = oLimits ?? StorageDecodeLimits.oDefault;
        if (oEffective.nMaxCollectionElements < 0 || oEffective.nMaxMaterializedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(oLimits));
        m_abyData = abyData;
        m_nOffset = 0;
        m_nRemainingCollectionElements = oEffective.nMaxCollectionElements;
        m_nRemainingMaterializedBytes = oEffective.nMaxMaterializedBytes;
    }

    /// <summary>The number of unread bytes.</summary>
    public int nRemaining => m_abyData.Length - m_nOffset;

    /// <summary>Rejects any unread bytes after a complete record has been decoded.</summary>
    public void EnsureEnd()
    {
        if (nRemaining != 0)
            throw new InvalidDataException("Unexpected trailing bytes in persisted record.");
    }

    private ReadOnlySpan<byte> Read(int nCount)
    {
        if (nCount < 0 || nCount > nRemaining)
            throw new InvalidDataException("Truncated or invalid persisted record.");
        ReadOnlySpan<byte> aby = m_abyData.Slice(m_nOffset, nCount);
        m_nOffset += nCount;
        return aby;
    }

    /// <summary>Reads an unsigned 8-bit integer.</summary>
    public byte ReadUInt8() => Read(1)[0];
    /// <summary>Reads a signed 8-bit integer.</summary>
    public sbyte ReadInt8() => unchecked((sbyte)ReadUInt8());
    /// <summary>Reads a single byte, accepting only zero or one.</summary>
    public bool ReadBool() => ReadUInt8() switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException("Boolean value must be 0 or 1.")
    };
    /// <summary>Reads a signed 16-bit integer in little-endian order.</summary>
    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(Read(2));
    /// <summary>Reads an unsigned 16-bit integer in little-endian order.</summary>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Read(2));
    /// <summary>Reads a signed 32-bit integer in little-endian order.</summary>
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Read(4));
    /// <summary>Reads an unsigned 32-bit integer in little-endian order.</summary>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Read(4));
    /// <summary>Reads a signed 64-bit integer in little-endian order.</summary>
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Read(8));
    /// <summary>Reads an unsigned 64-bit integer in little-endian order.</summary>
    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Read(8));
    /// <summary>Reads an IEEE 754 binary32 value, preserving its stored bits.</summary>
    public float ReadFloat32() => BitConverter.Int32BitsToSingle(ReadInt32());
    /// <summary>Reads an IEEE 754 binary64 value, preserving its stored bits.</summary>
    public double ReadFloat64() => BitConverter.Int64BitsToDouble(ReadInt64());
    /// <summary>Reads two float32 components in X, Y order.</summary>
    public Vector2 ReadVector2f() => new(ReadFloat32(), ReadFloat32());
    /// <summary>Reads three float32 components in X, Y, Z order.</summary>
    public Vector3 ReadVector3f() => new(ReadFloat32(), ReadFloat32(), ReadFloat32());
    /// <summary>Reads four float32 components in X, Y, Z, W order.</summary>
    public Vector4 ReadVector4f() => new(ReadFloat32(), ReadFloat32(), ReadFloat32(), ReadFloat32());

    /// <summary>Reads a GUID as 16 bytes in network order.</summary>
    public Guid ReadGuid() => new(Read(16), bigEndian: true);
    /// <summary>Reads a 16-byte RFC 4122 nonce, rejecting an empty GUID.</summary>
    public Nonce ReadNonce()
    {
        Guid idValue = ReadGuid();
        if (idValue == Guid.Empty)
            throw new InvalidDataException("An empty GUID is not a valid persisted nonce.");
        return new Nonce(idValue);
    }

    /// <summary>Reads the 32 canonical bytes of an object identity.</summary>
    public StorageObjectId ReadObjectId() => new(Read(StorageObjectId.nByteCount));

    /// <summary>Reads an unsigned 64-bit count, rejecting values above <see cref="int.MaxValue"/>.</summary>
    public int ReadCount()
    {
        ulong nCount = ReadUInt64();
        if (nCount > int.MaxValue)
            throw new InvalidDataException("Collection exceeds the capacity of a .NET array.");
        return (int)nCount;
    }

    /// <summary>
    /// Accounts for an entire collection before the generated codec allocates its builder.
    /// A positive lower bound also establishes that enough encoded bytes remain.
    /// The element budget is shared by all nested arrays using this reader.
    /// </summary>
    public void GuardCollectionCount(int nCount, int nMinimumElementBytes)
    {
        if (nCount < 0 || nMinimumElementBytes < 0)
            throw new ArgumentOutOfRangeException();
        if (nCount > m_nRemainingCollectionElements)
            throw new InvalidDataException("Collection exceeds the remaining decode element budget.");
        if (nMinimumElementBytes > 0 && nCount > nRemaining / nMinimumElementBytes)
            throw new InvalidDataException("Collection cannot fit in the remaining encoded payload.");
        m_nRemainingCollectionElements -= nCount;
    }

    private void GuardMaterializedBytes(int nCount)
    {
        if (nCount < 0 || nCount > nRemaining)
            throw new InvalidDataException("Truncated or invalid persisted record.");
        if (nCount > m_nRemainingMaterializedBytes)
            throw new InvalidDataException("Materialized data exceeds the decode byte budget.");
        m_nRemainingMaterializedBytes -= nCount;
    }

    /// <summary>Reads a byte-count-prefixed UTF-8 string, rejecting malformed UTF-8.</summary>
    public string ReadUtf8()
    {
        int nCount = ReadCount();
        GuardMaterializedBytes(nCount);
        return c_oUtf8.GetString(Read(nCount));
    }
    /// <summary>Reads an unsigned 64-bit count followed by that many bytes.</summary>
    public ImmutableArray<byte> ReadBytes()
    {
        int nCount = ReadCount();
        GuardMaterializedBytes(nCount);
        return ImmutableArray.CreateRange(Read(nCount).ToArray());
    }
}
