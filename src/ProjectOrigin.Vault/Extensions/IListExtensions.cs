using System.Collections.Generic;

namespace ProjectOrigin.Vault.Extensions;

public static class IListExtensions
{
    public static T PopFirst<T>(this IList<T> list)
    {
        var r = list[0];
        list.RemoveAt(0);
        return r;
    }
}
