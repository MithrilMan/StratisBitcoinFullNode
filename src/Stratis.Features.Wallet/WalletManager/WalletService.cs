using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Wallet.Repository;
using Stratis.Features.Wallet.Repository.Extensions;
using TracerAttributes;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Service interface that operates on wallets.
    /// </summary>
    /// <seealso cref="Stratis.Features.Wallet.IWalletUseCases" />
    public class WalletService : IWalletService
    {
        private readonly Network network;

        public IWalletUnitOfWork walletUnitOfWork { get; }

        public WalletService(Network network, IWalletUnitOfWork walletUnitOfWork)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.walletUnitOfWork = Guard.NotNull(walletUnitOfWork, nameof(walletUnitOfWork));
        }


        /// <inheritdoc />
        [NoTrace]
        public ISecret GetExtendedPrivateKeyForAddress(string password, string walletName, string address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(address, nameof(address));

            // ensures wallet exists.
            IWallet wallet = this.walletUnitOfWork.WalletRepository.GetByName(walletName).ThrowIfNull();
            // ensures address is a known wallet address.
            HdAddress hdAddress = this.walletUnitOfWork.HdAddressRepository.GetAddress(address).ThrowIfNull();

            // get wallet seed
            Key seed = HdOperations.DecryptSeed(wallet.EncryptedSeed, password, this.network);

            // get extended private key
            return HdOperations.GetExtendedPrivateKey(seed, wallet.ChainCode, hdAddress.HdPath, this.network);
        }

        /// <inheritdoc cref="GetExtendedPrivateKeyForAddress" />
        [NoTrace]
        public ISecret GetExtendedPrivateKeyForAddress(string password, string walletName, HdAddress address)
        {
            return this.GetExtendedPrivateKeyForAddress(password, walletName, address?.Address);
        }

        /// <inheritdoc />
        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(externalAddress, nameof(externalAddress));

            IWallet wallet = this.walletUnitOfWork.WalletRepository.GetByName(walletName).ThrowIfNull();
            HdAddress hdAddress = this.walletUnitOfWork.HdAddressRepository.GetAddress(externalAddress).ThrowIfNull();

            // get wallet seed
            Key seed = HdOperations.DecryptSeed(wallet.EncryptedSeed, password, this.network);

            // get extended private key
            Key privateKey = HdOperations.GetExtendedPrivateKey(seed, wallet.ChainCode, hdAddress.HdPath, this.network).PrivateKey;

            // Sign the message.
            return privateKey.SignMessage(message);
        }
    }
}
