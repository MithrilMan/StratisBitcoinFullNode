using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
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
        // <summary>As per RPC method definition this should be the max allowable expiry duration.</summary>
        private const int MaxWalletUnlockDurationInSeconds = 1073741824;

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

        protected readonly IHdAddressLookup hdAddressLookup;

        /// <summary>The type of coin used in this manager.</summary>
        protected readonly CoinType coinType;

        /// <summary>The event bus</summary>
        private readonly ISignals signals;

        private readonly IWalletLockTracker walletLockTracker;

        /// <summary>The chain of headers.</summary>
        protected readonly ChainIndexer chainIndexer;

        public WalletService(LoggerFactory loggerFactory, Network network, IDateTimeProvider dateTimeProvider, ISignals signals, ChainIndexer chainIndexer,
            IWalletUnitOfWork walletUnitOfWork, WalletSettings walletSettings, IWalletLockTracker walletLockTracker, IHdAddressLookup hdAddressLookup)
        {
            this.logger = Guard.NotNull(loggerFactory, nameof(loggerFactory)).CreateLogger(this.GetType().FullName);

            this.network = Guard.NotNull(network, nameof(network));
            this.dateTimeProvider = Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.chainIndexer = Guard.NotNull(chainIndexer, nameof(chainIndexer));
            this.walletUnitOfWork = Guard.NotNull(walletUnitOfWork, nameof(walletUnitOfWork));
            this.walletSettings = Guard.NotNull(walletSettings, nameof(walletSettings));
            this.walletLockTracker = Guard.NotNull(walletLockTracker, nameof(walletLockTracker));
            this.hdAddressLookup = Guard.NotNull(hdAddressLookup, nameof(hdAddressLookup));

            this.coinType = (CoinType)network.Consensus.CoinType;
        }

        /// <inheritdoc />
        public (Mnemonic mnemonic, IWallet wallet) CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
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

            IWallet wallet = null;
            using (var uowSession = this.walletUnitOfWork.Begin())
            {
                wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode);

                CreateWalletAccount(wallet, password);

                uowSession.Commit();
            }

            this.OnWalletCreated(wallet);

            return (mnemonic, wallet);
        }

        /// <inheritdoc />
        public IWallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys.
            ExtKey extendedKey;
            try
            {
                extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
            }
            catch (NotSupportedException ex)
            {
                this.logger.LogDebug("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");

                if (ex.Message == "Unknown")
                    throw new WalletException("Please make sure you enter valid mnemonic words.");

                throw;
            }

            IWallet wallet = null;
            using (var uowSession = this.walletUnitOfWork.Begin())
            {
                // Create a wallet.
                string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
                wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode, creationTime);

                HdAccount account = CreateWalletAccount(wallet, password);

                uowSession.Commit();
            }

            this.OnWalletRecovered(wallet, creationTime);

            return wallet;
        }

        /// <inheritdoc />
        public IWallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(extPubKey, nameof(extPubKey));
            this.logger.LogDebug("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(name), name, nameof(extPubKey), extPubKey, nameof(accountIndex), accountIndex);

            IWallet wallet = null;
            using (var uowSession = this.walletUnitOfWork.Begin())
            {
                // Create a wallet file.
                wallet = this.GenerateExtPubKeyOnlyWallet(name, creationTime);

                // Generate account
                HdAccount account = wallet.AddNewAccount(extPubKey, accountIndex, this.dateTimeProvider.GetTimeOffset());
                this.walletUnitOfWork.HdAccountRepository.Add(account);

                this.FillAddressPool(account);

                uowSession.Commit();
            }

            this.OnWalletRecovered(wallet, creationTime);

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
            IWallet wallet = this.GetWalletByName(walletName);
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

            IWallet wallet = this.GetWalletByName(walletName);
            HdAddress hdAddress = this.walletUnitOfWork.HdAddressRepository.GetAddress(externalAddress).ThrowIfNull();

            // get wallet seed
            Key seed = HdOperations.DecryptSeed(wallet.EncryptedSeed, password, this.network);

            // get extended private key
            Key privateKey = HdOperations.GetExtendedPrivateKey(seed, wallet.ChainCode, hdAddress.HdPath, this.network).PrivateKey;

            // Sign the message.
            return privateKey.SignMessage(message);
        }

        /// <inheritdoc />
        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            // TODO: this method doesn't check if the external address belongs to one of our wallet, shouldn't it check it?
            Guard.NotEmpty(message, nameof(message));
            Guard.NotEmpty(externalAddress, nameof(externalAddress));
            Guard.NotEmpty(signature, nameof(signature));

            bool result = false;

            try
            {
                var bitcoinPubKeyAddress = new BitcoinPubKeyAddress(externalAddress, this.network);
                result = bitcoinPubKeyAddress.VerifyMessage(message, signature);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug("Failed to verify message: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
            return result;
        }

        /// <inheritdoc />
        public bool LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            IWallet wallet = this.walletUnitOfWork.WalletRepository.GetByName(name);
            if (wallet == null)
            {
                throw new WalletNotFoundException($"Wallet {name} not found.");
            }

            // Check the password.
            try
            {
                if (!wallet.IsExtPubKeyWallet)
                    Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            this.OnWalletLoaded(wallet);

            return true;
        }

        /// <inheritdoc />
        public int LoadWallets()
        {
            IEnumerable<IWallet> wallets = this.walletUnitOfWork.WalletRepository.GetAll();
            foreach (var wallet in wallets)
            {
                foreach (HdAccount account in this.walletUnitOfWork.HdAccountRepository.GetWalletAccounts(wallet.Name))
                {
                    using (var uowSession = this.walletUnitOfWork.Begin())
                    {
                        this.FillAddressPool(account);
                        uowSession.Commit();
                    }
                }

                this.OnWalletLoaded(wallet);
            }

            if (this.walletSettings.IsDefaultWalletEnabled())
            {
                IWallet defaultWallet = wallets.FirstOrDefault(w => w.Name == this.walletSettings.DefaultWalletName);
                // Check if it already exists, if not, create one.
                if (defaultWallet == null)
                {
                    var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                    defaultWallet = this.CreateWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, string.Empty, mnemonic).wallet;
                }

                // Make sure both unlock is specified, and that we actually have a default wallet name specified.
                if (this.walletSettings.UnlockDefaultWallet)
                {
                    this.walletLockTracker.UnlockWallet(defaultWallet, this.walletSettings.DefaultWalletPassword, TimeSpan.FromSeconds(MaxWalletUnlockDurationInSeconds));
                }
            }

            return wallets.Count();
        }

        /// <inheritdoc />
        [NoTrace]
        public ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false)
        {
            // ensures wallet exists.
            IWallet wallet = this.GetWalletByName(accountReference.WalletName);

            Key privateKey;
            SecureString walletSecretValue = this.walletLockTracker.GetSecret(wallet);
            if (walletSecretValue != null)
            {
                privateKey = wallet.Network.CreateBitcoinSecret(walletSecretValue.FromSecureString()).PrivateKey;
            }
            else
            {
                privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }

            if (cache)
            {
                this.walletLockTracker.UnlockWallet(wallet, password, TimeSpan.FromMinutes(5));
            }

            return new ExtKey(privateKey, wallet.ChainCode);
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));

            IWallet wallet = this.GetWalletByName(walletName);

            if (wallet.IsExtPubKeyWallet)
            {
                this.logger.LogTrace("(-)[CANNOT_ADD_ACCOUNT_TO_EXTPUBKEY_WALLET]");
                throw new CannotAddAccountToXpubKeyWalletException("Use recover-via-extpubkey instead.");
            }

            HdAccount account = null;

            account = wallet.GetFirstUnusedAccount();

            if (account != null)
            {
                this.logger.LogTrace("(-)[ACCOUNT_FOUND]");
                return account;
            }

            using (var uowSession = this.walletUnitOfWork.Begin())
            {
                // No unused account was found, create a new one.
                account = CreateWalletAccount(wallet, password);
                this.OnWalletAccountCreated(wallet, account);
            }

            return account;
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(IWallet wallet, ChainedHeader chainedHeader)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            wallet.BlockLocator = chainedHeader.GetLocator().Blocks;

            this.walletUnitOfWork.WalletRepository.SetWalletTip(wallet.Name, chainedHeader);
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));

            IWallet wallet = this.GetWalletByName(accountReference.WalletName);

            string extPubKey;
            // Get the account.
            HdAccount account = this.GetAccountByName(wallet, accountReference.AccountName);
            extPubKey = account.ExtendedPubKey;

            return extPubKey;
        }

        /// <inheritdoc />
        public virtual int GetSpecialAccountIndex(string purpose)
        {
            return Wallet.SpecialPurposeAccountIndexesStart;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            return this.GetSpendableTransactionsInWallet(walletName, confirmations, AccountFilters.NormalAccounts);
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations, Func<HdAccount, bool> accountFilter)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            IWallet wallet = this.GetWalletByName(walletName);

            // TODO: the commented code blow shows an approach with current uow+repositor-per-entity and have performance implications because having single repository
            // for every entity doesn't allow to create proper queries that join tables or use good indexes.
            // IMO would be better to have a repository per domain entity, (in this scenario the domain entity is the wallet) and that repository could handle multiple entities
            // so we can do complex joins easily. Of course in both ways we have to re-model current wallet models to include keys to be used to join tables

            //IEnumerable<HdAccount> accounts = this.walletUnitOfWork.HdAccountRepository.GetWalletAccounts(walletName)
            //    .Where(accountFilter ?? AccountFilters.NormalAccounts);

            //List<HdAddress> addresses = new List<HdAddress>();
            //foreach (HdAccount account in accounts)
            //{
            //    // TODO: Account table Id not decided yet, when the wallet model are settled down this should be changed
            //    // it's here just as a stub
            //    string accoundId = $"{account.ExtendedPubKey}_{account.Index}";
            //    this.walletUnitOfWork.HdAddressRepository.GetAccountAddresses(accoundId);

            //    // TODO: get all unspent outputs of each address
            //}

            UnspentOutputReference[] unspentOutputs = null;
            unspentOutputs = wallet.GetAllSpendableTransactions(this.chainIndexer.Tip.Height, confirmations, accountFilter).ToArray();

            return unspentOutputs;
        }

        /// <summary>
        /// called whenever an operation of wallet loading has been performed.
        /// </summary>
        /// <param name="wallet">The wallet that has been loaded.</param>
        protected virtual void OnWalletLoaded(IWallet wallet)
        {
            this.signals.Publish(new Events.WalletLoaded(wallet));
        }

        /// <summary>
        /// called whenever a wallet has been created.
        /// </summary>
        /// <param name="wallet">The wallet that has been created.</param>
        protected virtual void OnWalletCreated(IWallet wallet)
        {
            this.signals.Publish(new Events.WalletCreated(wallet));
        }

        protected virtual void OnWalletRecovered(IWallet wallet, DateTime creationTime)
        {
            this.signals.Publish(new Events.WalletRecovered(wallet, creationTime));
        }

        /// <summary>
        /// Called whenever a new account has been created (not raised during wallet creation/recover).
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="account">The account.</param>
        protected virtual void OnWalletAccountCreated(IWallet wallet, HdAccount account)
        {
            this.signals.Publish(new Events.WalletAccountCreated(wallet, account));
        }

        #region Helpers

        /// <summary>
        /// Fills both the internal and external address pool for a specific account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <returns>New change and receiving address that has been generated.</returns>
        private (IEnumerable<HdAddress> newChangeAddresses, IEnumerable<HdAddress> newReceivingAddresses) FillAddressPool(HdAccount account)
        {
            IEnumerable<HdAddress> newChangeAddresses = this.AddAddressesToMaintainBuffer(account, true);
            this.walletUnitOfWork.HdAddressRepository.Add(newChangeAddresses);
            this.hdAddressLookup.TrackAddresses(newChangeAddresses);

            IEnumerable<HdAddress> newReceivingAddresses = this.AddAddressesToMaintainBuffer(account, false);
            this.walletUnitOfWork.HdAddressRepository.Add(newReceivingAddresses);
            this.hdAddressLookup.TrackAddresses(newReceivingAddresses);

            return (newChangeAddresses, newReceivingAddresses);
        }

        /// <summary>
        /// Ensures that the specified account has enough available internal (when <paramref name="isInternal"/> is true) or external (when <paramref name="isInternal"/> is false) available addresses.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="isInternal">Specifies the address set to check: internals (if set to <c>true</c>) or externals (if set to <c>false</c>).</param>
        /// <returns>List of created addresses to fill the internal/external addresses buffer.</returns>
        private IEnumerable<HdAddress> AddAddressesToMaintainBuffer(HdAccount account, bool isInternal)
        {
            HdAddress lastUsedAddress = account.GetLastUsedAddress(isInternal);
            int lastUsedAddressIndex = lastUsedAddress?.Index ?? -1;
            int addressesCount = isInternal ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
            int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
            int addressesToAdd = this.walletSettings.UnusedAddressesBuffer - emptyAddressesCount;

            return addressesToAdd > 0 ? account.CreateAddresses(this.network, addressesToAdd, isInternal) : new List<HdAddress>();
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
        private IWallet GenerateWallet(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
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

        /// <summary>
        /// Generates the wallet file without private key and chain code.
        /// For use with only the extended public key.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private IWallet GenerateExtPubKeyOnlyWallet(string name, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.walletUnitOfWork.WalletRepository.GetByName(name) != null)
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }

            var wallet = this.walletUnitOfWork.WalletRepository.Add(new Wallet
            {
                Name = name,
                IsExtPubKeyWallet = true,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            });

            return wallet;
        }

        /// <summary>
        /// Creates a wallet account and fill it with a minimum set of internal and external addresses.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="password">The password.</param>
        /// <returns>The new account.</returns>
        private HdAccount CreateWalletAccount(IWallet wallet, string password)
        {
            HdAccount account = wallet.AddNewAccount(password, this.dateTimeProvider.GetTimeOffset());
            this.walletUnitOfWork.HdAccountRepository.Add(account);

            this.FillAddressPool(account);

            return account;
        }

        /// <summary>
        /// Gets the name of the wallet by.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>The wallet.</returns>
        /// <exception cref="WalletNotFoundException">Thrown when the wallet is not found.</exception>
        private IWallet GetWalletByName(string walletName)
        {
            return this.walletUnitOfWork.WalletRepository.GetByName(walletName).ThrowIfNull();
        }

        /// <summary>
        /// Gets the name of the wallet by.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="accountName">Name of the account.</param>
        /// <returns>
        /// The HdAccount.
        /// </returns>
        /// <exception cref="WalletNotFoundException">Thrown when the wallet is not found.</exception>
        private HdAccount GetAccountByName(IWallet wallet, string accountName)
        {
            return this.walletUnitOfWork.HdAccountRepository.GetByName(wallet.Name, accountName).ThrowIfNull();
        }
        #endregion
    }
}
