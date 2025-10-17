using System;
using System.Data;
using System.Globalization;
using Dapper;

namespace FlexGuard.Data.Infrastructure;

internal sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        => parameter.Value = value.ToString("O"); // ISO 8601 "round-trip"

    public override DateTimeOffset Parse(object value) => value switch
    {
        string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        DateTime d => d.Kind switch
        {
            DateTimeKind.Unspecified => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)),
            DateTimeKind.Local => new DateTimeOffset(d),
            _ => new DateTimeOffset(d)
        },
        long ticks => new DateTimeOffset(ticks, TimeSpan.Zero),
        _ => (DateTimeOffset)value
    };
}

internal sealed class NullableDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
{
    private static readonly DateTimeOffsetHandler _inner = new();

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
        => parameter.Value = value.HasValue ? value.Value.ToString("O") : DBNull.Value;

    public override DateTimeOffset? Parse(object value)
        => value is null or DBNull ? (DateTimeOffset?)null : _inner.Parse(value);
}

public static class DapperTypeHandlers
{
    private static bool _initialized;

    public static void EnsureRegistered()
    {
        if (_initialized) return;
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeOffsetHandler());
        // defensivt: registrér også via non-generic overloads
        SqlMapper.AddTypeHandler(typeof(DateTimeOffset), new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(typeof(DateTimeOffset?), new NullableDateTimeOffsetHandler());
        _initialized = true;
    }
}
