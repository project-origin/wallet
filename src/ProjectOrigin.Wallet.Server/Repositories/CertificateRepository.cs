using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Repositories;

public class CertificateRepository
{
    private IDbConnection _connection;

    public CertificateRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public Task InsertSlice(Slice newSlice)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Slices(Id, WalletSectionId, WalletSectionPosition, RegistryId, CertificateId, Quantity, RandomR, State) VALUES (@id, @walletSectionId, @walletSectionPosition, @registryId, @certificateId, @quantity, @randomR, @state)", new { newSlice.Id, newSlice.WalletSectionId, newSlice.WalletSectionPosition, newSlice.RegistryId, newSlice.CertificateId, newSlice.Quantity, newSlice.RandomR, newSlice.State });
    }

    public Task<Registry?> GetRegistryFromName(string registry)
    {
        return _connection.QueryFirstOrDefaultAsync<Registry?>("SELECT * FROM Registries WHERE Name = @registry", new { registry });
    }

    public Task InsertRegistry(Registry registry)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Registries(Id, Name) VALUES (@id, @name)", new { registry.Id, registry.Name });
    }

    public Task InsertCertificate(Certificate certificate)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Certificates(Id, RegistryId, State) VALUES (@id, @registryId, @state)", new { certificate.Id, certificate.RegistryId, certificate.State });
    }

    public Task<Certificate?> GetCertificate(Guid registryId, Guid certificateId)
    {
        return _connection.QueryFirstOrDefaultAsync<Certificate?>("SELECT * FROM Certificates WHERE Id = @certificateId AND RegistryId = @registryId", new { certificateId, registryId });
    }
}
