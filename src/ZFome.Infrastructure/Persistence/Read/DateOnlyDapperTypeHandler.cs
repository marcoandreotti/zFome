using System.Data;
using Dapper;

namespace ZFome.Infrastructure.Persistence.Read;

internal sealed class DateOnlyDapperTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value switch
    {
        DateOnly date => date,
        DateTime date => DateOnly.FromDateTime(date),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to {nameof(DateOnly)}.")
    };

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
