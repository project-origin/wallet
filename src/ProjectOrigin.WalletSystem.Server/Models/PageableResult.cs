using System;
using System.Collections.Generic;
using System.Linq;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;

public record PageResult<T>
{
    public required IEnumerable<T> Items { get; init; }

    public required int Count { get; init; }
    public required int Offset { get; init; }
    public required int Limit { get; init; }
    public required int TotalCount { get; init; }

    public ResultList<TR> ToResultList<TR>(Func<T, TR> map) => new()
    {
        Result = Items.Select(map),
        Metadata = new PageInfo
        {
            Count = Count,
            Offset = Offset,
            Limit = Limit,
            Total = TotalCount
        }
    };
}
