using System;
using NBitcoin;

namespace ProjectOrigin.Wallet.Server.HDWallet;

public class Secp256k1Algorithm : IHDAlgorithm
{
    public IHDPrivateKey GenerateNewPrivateKey()
    {
        return new Secp256k1PrivateKey(new ExtKey());
    }

    public IHDPrivateKey ImportPrivateKey(ReadOnlySpan<byte> privateKeyBytes)
    {
        ExtKey extKey = ExtKey.CreateFromBytes(privateKeyBytes);
        return new Secp256k1PrivateKey(extKey);
    }

    public IHDPublicKey ImportPublicKey(ReadOnlySpan<byte> publicKeyBytes)
    {
        ExtPubKey extPubKey = new ExtPubKey(publicKeyBytes);
        return new Secp256k1PublicKey(extPubKey);
    }

    private static uint256 HashData(ReadOnlySpan<byte> data)
    {
        return new uint256(NBitcoin.Crypto.Hashes.SHA256(data));
    }

    internal class Secp256k1PrivateKey : IHDPrivateKey
    {
        private readonly ExtKey _key;

        public Secp256k1PrivateKey(ExtKey key)
        {
            _key = key;
        }

        public ReadOnlySpan<byte> Sign(ReadOnlySpan<byte> data)
        {
            return _key.PrivateKey.Sign(HashData(data)).ToDER();
        }

        public IHDPublicKey PublicKey => new Secp256k1PublicKey(_key.Neuter());

        public ReadOnlySpan<byte> Export() => _key.ToBytes();

        public IHDPrivateKey Derive(int position) => new Secp256k1PrivateKey(_key.Derive((uint)position));
    }

    internal class Secp256k1PublicKey : IHDPublicKey
    {
        private ExtPubKey _extPubKey;

        public Secp256k1PublicKey(ExtPubKey extPubKey)
        {
            _extPubKey = extPubKey;
        }

        public IHDPublicKey Derive(int position) => new Secp256k1PublicKey(_extPubKey.Derive((uint)position));

        public ReadOnlySpan<byte> Export() => this._extPubKey.ToBytes();

        public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        {
            return _extPubKey.PubKey.Verify(HashData(data), new NBitcoin.Crypto.ECDSASignature(signature));
        }
    }
}
