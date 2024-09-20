using System;

namespace ProjectOrigin.Vault.Projections;

public class ProjectionException : Exception
{
    public ProjectionException(string message) : base(message) { }
}
