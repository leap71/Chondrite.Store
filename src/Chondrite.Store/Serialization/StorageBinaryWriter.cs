using Chondrite.Core;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Chondrite.Store.Serialization;

/// <summary>
/// Canonical, little-endian binary writer used by generated persistence codecs.
/// The format is independent of CLR memory layout, property names, and locale.
/// </summary>
public sealed class StorageBinaryWriter
{
    private static readonly UTF8Encoding c_oUtf8 = new(false, true);
    private readonly MemoryStream m_oBuffer = new();

    /// <summary>Copies the accumulated encoded bytes into a new array.</summary>
    public byte[] aToArray() => m_oBuffer.ToArray();

    /// <summary>Writes zero for false or one for true.</summary>
    public void WriteBool(bool bValue) => WriteUInt8(bValue ? (byte)1 : (byte)0);
    /// <summary>Writes a signed 8-bit integer.</summary>
    public void WriteInt8(sbyte nValue) => WriteUInt8(unchecked((byte)nValue));
    /// <summary>Writes an unsigned 8-bit integer.</summary>
    public void WriteUInt8(byte nValue) => m_oBuffer.WriteByte(nValue);

    /// <summary>Writes a signed 16-bit integer in little-endian order.</summary>
    public void WriteInt16(short nValue)
    {
        Span<byte> aby = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes an unsigned 16-bit integer in little-endian order.</summary>
    public void WriteUInt16(ushort nValue)
    {
        Span<byte> aby = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes a signed 32-bit integer in little-endian order.</summary>
    public void WriteInt32(int nValue)
    {
        Span<byte> aby = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes an unsigned 32-bit integer in little-endian order.</summary>
    public void WriteUInt32(uint nValue)
    {
        Span<byte> aby = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes a signed 64-bit integer in little-endian order.</summary>
    public void WriteInt64(long nValue)
    {
        Span<byte> aby = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes an unsigned 64-bit integer in little-endian order.</summary>
    public void WriteUInt64(ulong nValue)
    {
        Span<byte> aby = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(aby, nValue);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes the exact IEEE 754 binary32 bits in little-endian order.</summary>
    public void WriteFloat32(float fValue) => WriteInt32(BitConverter.SingleToInt32Bits(fValue));
    /// <summary>Writes the exact IEEE 754 binary64 bits in little-endian order.</summary>
    public void WriteFloat64(double fValue) => WriteInt64(BitConverter.DoubleToInt64Bits(fValue));

    /// <summary>Writes X, Y as consecutive little-endian float32 values (8 bytes).</summary>
    public void WriteVector2f(Vector2 oValue)
    {
        WriteFloat32(oValue.X);
        WriteFloat32(oValue.Y);
    }

    /// <summary>Writes X, Y, Z as consecutive little-endian float32 values (12 bytes).</summary>
    public void WriteVector3f(Vector3 oValue)
    {
        WriteFloat32(oValue.X);
        WriteFloat32(oValue.Y);
        WriteFloat32(oValue.Z);
    }

    /// <summary>Writes X, Y, Z, W as consecutive little-endian float32 values (16 bytes).</summary>
    public void WriteVector4f(Vector4 oValue)
    {
        WriteFloat32(oValue.X);
        WriteFloat32(oValue.Y);
        WriteFloat32(oValue.Z);
        WriteFloat32(oValue.W);
    }

    /// <summary>Writes a GUID as 16 bytes in network order.</summary>
    public void WriteGuid(Guid idValue)
    {
        Span<byte> aby = stackalloc byte[16];
        if (!idValue.TryWriteBytes(aby, bigEndian: true, out int nWritten) || nWritten != 16)
            throw new InvalidOperationException("Could not encode GUID.");
        m_oBuffer.Write(aby);
    }
    /// <summary>
    /// Writes a nonce as exactly 16 bytes in RFC 4122 GUID byte order.
    /// A default/uninitialized nonce is not a valid persisted value.
    /// </summary>
    public void WriteNonce(Nonce oValue)
    {
        if (oValue.idValue == Guid.Empty)
            throw new InvalidDataException("Cannot serialize an uninitialized nonce.");
        WriteGuid(oValue.idValue);
    }

    /// <summary>Writes the 32 canonical bytes of an object identity.</summary>
    public void WriteObjectId(StorageObjectId idValue)
    {
        Span<byte> aby = stackalloc byte[StorageObjectId.nByteCount];
        idValue.WriteBytes(aby);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes a UTF-8 byte count and bytes, rejecting malformed UTF-16 input.</summary>
    public void WriteUtf8(string strValue)
    {
        ArgumentNullException.ThrowIfNull(strValue);
        byte[] aby = c_oUtf8.GetBytes(strValue);
        WriteUInt64((ulong)aby.Length);
        m_oBuffer.Write(aby);
    }
    /// <summary>Writes an unsigned 64-bit count followed by the supplied bytes.</summary>
    public void WriteBytes(ReadOnlySpan<byte> abyValue)
    {
        WriteUInt64((ulong)abyValue.Length);
        m_oBuffer.Write(abyValue);
    }
}
