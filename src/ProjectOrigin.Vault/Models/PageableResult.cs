using System;
using System.Collections.Generic;
using System.Linq;
using ProjectOrigin.Vault.Services.REST.v1;

namespace ProjectOrigin.Vault.Models;

public record PageResult<T>
{
    public required IEnumerable<T> Items { get; init; }

    public required int Count { get; init; }
    public required int Offset { get; init; }
    public required int Limit { get; init; }
    public required int TotalCount { get; init; }

    public ResultList<TR, PageInfo> ToResultList<TR>(Func<T, TR> map) => new()
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


public record PageResultCursor<T>
{
    public required IEnumerable<T> Items { get; init; }

    public required int Count { get; init; }
    public required long? updatedAt { get; init; }
    public required int Limit { get; init; }
    public required int TotalCount { get; init; }

    public ResultList<TR, PageInfoCursor> ToResultList<TR>(Func<T, TR> map) => new()
    {
        Result = Items.Select(map),
        Metadata = new PageInfoCursor
        {
            Count = Count,
            UpdatedAt = updatedAt,
            Limit = Limit,
            Total = TotalCount
        }
    };
}
