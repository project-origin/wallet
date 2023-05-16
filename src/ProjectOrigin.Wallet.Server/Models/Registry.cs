using System;

namespace ProjectOrigin.Wallet.Server.Models;

public class Registry
{
    public Guid Id { get; }
    public string Name { get; }

    public Registry(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}
