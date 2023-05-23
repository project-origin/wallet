using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Models.Database;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

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

    public Task<IEnumerable<CertificateEntity>> GetAllOwnedCertificates(string owner)
    {
        var sql = @"SELECT c.Id, c.RegistryId, c.State, SUM(s.Quantity) as Quantity
                    FROM Wallets w
                    LEFT JOIN WalletSections ws ON w.Id = ws.WalletId
                    LEFT JOIN Slices s ON ws.Id = s.WalletSectionId
                    LEFT JOIN Certificates c ON s.CertificateId = c.Id
                    WHERE w.Owner = @owner
                    GROUP BY c.Id, c.RegistryId, c.State
                    ";
        return _connection.QueryAsync<CertificateEntity, decimal, CertificateEntity>(sql, (cert, quantity) =>
        {
            cert.Quantity = (long)quantity;
            return cert;
        }, splitOn: "Quantity", param: new { owner });
    }
}
