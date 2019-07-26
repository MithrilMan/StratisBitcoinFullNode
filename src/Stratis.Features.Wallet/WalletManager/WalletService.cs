using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Signals;
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
        /// <summary>Logger instance.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The network instance.
        /// </summary>
        protected readonly Network network;

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        protected IWalletUnitOfWork walletUnitOfWork;

        /// <summary>The settings for the wallet feature.</summary>
        protected readonly WalletSettings walletSettings;

        /// <summary>The type of coin used in this manager.</summary>
        protected readonly CoinType coinType;

        /// <summary>The event bus</summary>
        private readonly ISignals signals;

        public WalletService(LoggerFactory loggerFactory, Network network, IDateTimeProvider dateTimeProvider, ISignals signals, IWalletUnitOfWork walletUnitOfWork, WalletSettings walletSettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.network = Guard.NotNull(network, nameof(network));
            this.dateTimeProvider = Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.walletUnitOfWork = Guard.NotNull(walletUnitOfWork, nameof(walletUnitOfWork));
            this.walletSettings = Guard.NotNull(walletSettings, nameof(walletSettings));

            this.coinType = (CoinType)network.Consensus.CoinType;
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            IWallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // Generate multiple accounts and addresses from the get-go.
            HdAccount account = wallet.AddNewAccount(password, this.dateTimeProvider.GetTimeOffset());

            IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer);
            this.walletUnitOfWork.HdAddressRepository.Add(newReceivingAddresses);

            IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer, true);
            this.walletUnitOfWork.HdAddressRepository.Add(newChangeAddresses);

            this.walletUnitOfWork.Save();

            this.signals.Publish(new Events.WalletCreated(wallet));

            return mnemonic;
        }

        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="encryptedSeed">The seed for this wallet, password encrypted.</param>
        /// <param name="chainCode">The chain code.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <remarks>Doesn't commit current unit of work.</remarks>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private IWallet GenerateWalletFile(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.walletUnitOfWork.WalletRepository.GetByName(name) != null)
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletDuplicateNameException($"Wallet with name '{name}' already exists.");
            }

            if (this.walletUnitOfWork.WalletRepository.GetByEncryptedSeed(encryptedSeed) != null)
            {
                this.logger.LogTrace("(-)[SAME_PK_ALREADY_EXISTS]");
                throw new WalletDuplicateEncryptedSeedException("A wallet with the same private key already exists.");
            }

            var wallet = this.walletUnitOfWork.WalletRepository.Add(new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            });

            return wallet;
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
