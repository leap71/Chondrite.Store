using Chondrite.Store.Serialization;
using Xunit;

namespace Chondrite.Store.Tests;

public class NonceTests
{
    [Fact]
    public void ExplicitNonceHasStableNetworkOrderEncoding()
    {
        Guid idValue = new("00112233-4455-6677-8899-aabbccddeeff");
        Nonce idNonce = new(idValue);
        Assert.Equal(idValue, idNonce.idValue);
        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", idNonce.ToString());

        StorageBinaryWriter oWriter = new();
        oWriter.WriteNonce(idNonce);
        byte[] abyExpected = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        Assert.Equal(abyExpected, oWriter.aToArray());
        StorageBinaryReader oReader = new(abyExpected);
        Assert.Equal(idNonce, oReader.ReadNonce());
        oReader.EnsureEnd();

        StorageBinaryWriter oRepeated = new();
        oRepeated.WriteNonce(idNonce);
        Assert.Equal(oWriter.aToArray(), oRepeated.aToArray());
    }

    [Fact]
    public void NoncesAreGeneratedExplicitlyAndCannotPersistAsDefault()
    {
        Assert.NotEqual(default, Nonce.oNew());
        Assert.Throws<ArgumentException>(() => new Nonce(Guid.Empty));
        Assert.Throws<InvalidDataException>(() => new StorageBinaryWriter().WriteNonce(default));
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(new byte[16]);
            _ = oReader.ReadNonce();
        });
        Assert.Throws<InvalidDataException>(() =>
        {
            StorageBinaryReader oReader = new(new byte[15]);
            _ = oReader.ReadNonce();
        });
    }
}
