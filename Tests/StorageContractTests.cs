using System.Collections.Immutable;
using Xunit;

namespace Chondrite.Store.Tests;

public sealed class StorageContractTests
{
    [Fact]
    public void CursorCopiesInputAndRestoresByValue()
    {
        byte[] abySource = [1, 2, 3, 4];
        StorageCursor oCursor = new(abySource);
        abySource[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, oCursor.abyData.ToArray());
        StorageCursor oRestored = new(oCursor.abyData);
        Assert.Equal(oCursor, oRestored);
        Assert.Equal(oCursor.GetHashCode(), oRestored.GetHashCode());
        Assert.True(oCursor == oRestored);
        Assert.True(oCursor != new StorageCursor([1, 2, 3, 5]));
        Assert.NotEqual(default, oCursor);
    }

    [Fact]
    public void CursorRejectsEmptyRepresentation()
    {
        Assert.Throws<ArgumentException>(() => new StorageCursor([]));
    }

    [Fact]
    public void SchemaRegistrationRejectsNullStore()
    {
        IObjectStore oStore = null!;
        Assert.Throws<ArgumentNullException>(() => oStore.idRegisterType(ImmutableArray<byte>.Empty));
    }

    [Fact]
    public void SchemaRegistrationRejectsUninitializedBytes()
    {
        TestObjectStore oStore = new();
        Assert.Throws<ArgumentException>(() => oStore.idRegisterType(default));
    }
}
