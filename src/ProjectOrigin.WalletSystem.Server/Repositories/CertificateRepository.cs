using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

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

    public async Task InsertCertificate(Certificate certificate)
    {
        await _connection.ExecuteAsync(@"INSERT INTO Certificates(Id, RegistryId, StartDate, EndDate, GridArea, CertificateType) VALUES (@id, @registryId, @startDate, @endDate, @gridArea, @certificateType)",
            new { certificate.Id, certificate.RegistryId, startDate = certificate.StartDate.ToUtcTime(), endDate = certificate.EndDate.ToUtcTime(), certificate.GridArea, certificate.CertificateType });

        foreach (var atr in certificate.Attributes)
        {
            var atrId = Guid.NewGuid();
            await _connection.ExecuteAsync(@"INSERT INTO Attributes(Id, KeyAtr, ValueAtr, CertificateId, RegistryId) VALUES (@atrId, @key, @value, @id, @registryId)",
                new { atrId, atr.Key, atr.Value, certificate.Id, certificate.RegistryId });
        }
    }

    public async Task<Certificate?> GetCertificate(Guid registryId, Guid certificateId)
    {
        var sql = @"SELECT c.Id, c.RegistryId, c.StartDate, c.EndDate, c.GridArea, c.CertificateType, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
                    FROM Certificates c
                    LEFT JOIN Attributes a ON c.Id = a.CertificateId AND c.RegistryId = a.RegistryId
                    WHERE c.Id = @certificateId AND c.RegistryId = @registryId";

        var certsDictionary = new Dictionary<Guid, Certificate>();
        var res = await _connection.QueryAsync<Certificate?, CertificateAttribute, Certificate?>(sql,
            (cert, atr) =>
            {
                if (cert == null) return null;

                if (!certsDictionary.TryGetValue(cert.Id, out var certificate))
                {
                    certsDictionary.Add(cert.Id, certificate = cert);
                }

                if (atr != null)
                    certificate.Attributes.Add(atr);

                return certificate;
            }, splitOn: "AttributeId", param: new { certificateId, registryId });

        return certsDictionary.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner)
    {
        var sql = @"SELECT c.Id, r.Name as Registry, c.StartDate, c. EndDate, c.GridArea, c.CertificateType, s.Id AS SliceId, s.Quantity as Quantity, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
                    FROM Wallets w
                    LEFT JOIN WalletSections ws ON w.Id = ws.WalletId
                    LEFT JOIN Slices s ON ws.Id = s.WalletSectionId
                    LEFT JOIN Certificates c ON s.CertificateId = c.Id
                    LEFT JOIN Attributes a ON c.Id = a.CertificateId AND c.RegistryId = a.RegistryId
                    LEFT JOIN Registries r ON c.RegistryId = r.Id
                    WHERE w.Owner = @owner";

        var certsDictionary = new Dictionary<Guid, CertificateViewModel>();
        var res = await _connection.QueryAsync<CertificateViewModel, SliceViewModel, CertificateAttribute, CertificateViewModel>(sql,
            (cert, slice, atr) =>
            {
                if (!certsDictionary.TryGetValue(cert.Id, out var certificate))
                {
                    certsDictionary.Add(cert.Id, certificate = cert);
                }

                if (slice != null && !certificate.Slices.Contains(slice))
                    certificate.Slices.Add(slice);

                if (atr != null && !certificate.Attributes.Contains(atr))
                    certificate.Attributes.Add(atr);

                return certificate;
            },
            splitOn: "SliceId, AttributeId",
            param: new { owner });

        return certsDictionary.Values;
    }

    public Task<IEnumerable<ReceivedSlice>> GetAllReceivedSlices()
    {
        return _connection.QueryAsync<ReceivedSlice>("SELECT * FROM ReceivedSlices");
    }

    public Task<ReceivedSlice?> GetTop1ReceivedSlice()
    {
        return _connection.QueryFirstOrDefaultAsync<ReceivedSlice?>("SELECT * FROM ReceivedSlices LIMIT 1");
    }

    public Task RemoveReceivedSlices(List<ReceivedSlice> receivedSlices)
    {
        var ids = receivedSlices.Select(x => x.Id).ToList();
        return _connection.ExecuteAsync("DELETE FROM ReceivedSlices WHERE Id = ANY(@ids)", new { ids });
    }

    public Task RemoveReceivedSlice(ReceivedSlice receivedSlice)
    {
        return _connection.ExecuteAsync("DELETE FROM ReceivedSlices WHERE Id = @id", new { receivedSlice.Id });
    }

    public Task<IEnumerable<ReceivedSlice>> GetReceivedSlices(List<Guid> ids)
    {
        return _connection.QueryAsync<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE Id = ANY(@ids)", new { ids });
    }

    internal Task<Certificate> GetCertificate(object id, Guid certificateId)
    {
        throw new NotImplementedException();
    }
}
