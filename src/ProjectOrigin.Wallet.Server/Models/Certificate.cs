using System;
using ProjectOrigin.Wallet.Server.Models.Primitives;

namespace ProjectOrigin.Wallet.Server.Models
{
    public class Certificate
    {
        public Guid Id { get; }
        public Guid RegistryId { get; }
        public Technology Technology { get; }
        public Period Period { get; }
        public string GridArea { get; }
        public bool Loaded { get; }

        public Certificate(Guid id, Guid registryId, Technology technology, Period period, string gridArea)
        {
            Id = id;
            RegistryId = registryId;
            Technology = technology;
            Period = period;
            GridArea = gridArea;
            Loaded = false;
        }
    }
}
