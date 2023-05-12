using System;

namespace ProjectOrigin.Wallet.Server.HDWallet;

public interface IHDAlgorithm
{
    public IHDPrivateKey GenerateNewPrivateKey();
    public IHDPrivateKey ImportPrivateKey(ReadOnlySpan<byte> privateKeyBytes);
    public IHDPublicKey ImportPublicKey(ReadOnlySpan<byte> publicKeyBytes);
}

public interface IHDPrivateKey
{
    public ReadOnlySpan<byte> Sign(ReadOnlySpan<byte> data);
    public IHDPublicKey PublicKey { get; }
    public ReadOnlySpan<byte> Export();
    public IHDPrivateKey Derive(int position);
}

public interface IHDPublicKey
{
    public ReadOnlySpan<byte> Export();
    public IHDPublicKey Derive(int position);
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}
