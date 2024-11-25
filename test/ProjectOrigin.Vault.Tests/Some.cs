using System;
using System.Text;

namespace ProjectOrigin.Vault.Tests;

public static class Some
{
    public static string Gsrn()
    {
        var rand = new Random();
        var sb = new StringBuilder();
        sb.Append("57");
        for (var i = 0; i < 16; i++)
        {
            sb.Append(rand.Next(0, 9));
        }

        return sb.ToString();
    }

    public const string TechCode = "T070000";
    public const string FuelCode = "F00000000";
}
