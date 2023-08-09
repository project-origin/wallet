using System.Collections.Generic;
using System.Linq;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class IEnumerableExtensions
{
    public static bool IsEmpty<TSource>(this IEnumerable<TSource> source) => !source.Any();
}
