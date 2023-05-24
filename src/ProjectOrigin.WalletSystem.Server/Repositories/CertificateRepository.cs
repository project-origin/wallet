using System;
using System.Collections.Generic;
using System.Data;
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
        return _connection.ExecuteAsync(@"INSERT INTO Slices(Id, WalletSectionId, WalletSectionPosition, RegistryId, CertificateId, Quantity, RandomR) VALUES (@id, @walletSectionId, @walletSectionPosition, @registryId, @certificateId, @quantity, @randomR)", new { newSlice.Id, newSlice.WalletSectionId, newSlice.WalletSectionPosition, newSlice.RegistryId, newSlice.CertificateId, newSlice.Quantity, newSlice.RandomR, });
    }

    public Task InsertReceivedSlice(ReceivedSlice receivedSlice)
    {
        return _connection.ExecuteAsync(@"INSERT INTO ReceivedSlices(Id, WalletSectionId, WalletSectionPosition, Registry, CertificateId, Quantity, RandomR) VALUES (@id, @walletSectionId, @walletSectionPosition, @registry, @certificateId, @quantity, @randomR)", new { receivedSlice.Id, receivedSlice.WalletSectionId, receivedSlice.WalletSectionPosition, receivedSlice.Registry, receivedSlice.CertificateId, receivedSlice.Quantity, receivedSlice.RandomR });
    }

    public Task InsertCertificate(Certificate certificate)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Certificates(Id, RegistryId) VALUES (@id, @registryId)", new { certificate.Id, certificate.RegistryId });
    }

    public Task<Certificate?> GetCertificate(Guid registryId, Guid certificateId)
    {
        return _connection.QueryFirstOrDefaultAsync<Certificate?>("SELECT * FROM Certificates WHERE Id = @certificateId AND RegistryId = @registryId", new { certificateId, registryId });
    }

    public Task<IEnumerable<CertificateEntity>> GetAllOwnedCertificates(string owner)
    {
        var sql = @"SELECT c.Id, r.Name as Registry, SUM(s.Quantity) as Quantity
                    FROM Wallets w
                    LEFT JOIN WalletSections ws ON w.Id = ws.WalletId
                    LEFT JOIN Slices s ON ws.Id = s.WalletSectionId
                    LEFT JOIN Certificates c ON s.CertificateId = c.Id
                    LEFT JOIN Registries r ON c.RegistryId = r.Id
                    WHERE w.Owner = @owner
                    GROUP BY c.Id, r.Name
                    ";
        return _connection.QueryAsync<CertificateEntity, decimal, CertificateEntity>(sql, (cert, quantity) =>
        {
            cert.Quantity = (long)quantity;
            return cert;
        }, splitOn: nameof(CertificateEntity.Quantity), param: new { owner });
    }
}
