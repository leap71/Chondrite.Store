// User-editable partial declaration. The schema compiler never overwrites this file.
#nullable enable
using System;
using System.Collections.Immutable;

namespace Chondrite.Store.Tests.Records;

public readonly partial record struct Pascals
{
    public Pascals(
        double fValue)
    {
        m_oData = new SchemaData(fValue);
    }

    public double fValue => m_oData.fValue;
}
