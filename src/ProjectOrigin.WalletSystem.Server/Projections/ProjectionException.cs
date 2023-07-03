namespace ProjectOrigin.WalletSystem.Server.Projections;

public class ProjectionException : System.Exception
{
    public ProjectionException() { }
    public ProjectionException(string message) : base(message) { }
    public ProjectionException(string message, System.Exception inner) : base(message, inner) { }
    protected ProjectionException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
