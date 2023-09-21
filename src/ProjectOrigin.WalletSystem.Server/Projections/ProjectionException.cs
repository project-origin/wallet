using System;

namespace ProjectOrigin.WalletSystem.Server.Projections;

public class ProjectionException : Exception
{
    public ProjectionException(string message) : base(message) { }
}
