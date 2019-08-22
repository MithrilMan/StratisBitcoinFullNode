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

        protected readonly IWalletStore walletStore;

        public WalletService(LoggerFactory loggerFactory, Network network, IDateTimeProvider dateTimeProvider, ISignals signals, ChainIndexer chainIndexer,
            WalletSettings walletSettings, IWalletLockTracker walletLockTracker, IHdAddressLookup hdAddressLookup, IWalletStore walletStore)
        {
            this.logger = Guard.NotNull(loggerFactory, nameof(loggerFactory)).CreateLogger(this.GetType().FullName);

            this.network = Guard.NotNull(network, nameof(network));
            this.dateTimeProvider = Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.chainIndexer = Guard.NotNull(chainIndexer, nameof(chainIndexer));
            this.walletSettings = Guard.NotNull(walletSettings, nameof(walletSettings));
            this.walletLockTracker = Guard.NotNull(walletLockTracker, nameof(walletLockTracker));
            this.hdAddressLookup = Guard.NotNull(hdAddressLookup, nameof(hdAddressLookup));
            this.walletStore = Guard.NotNull(walletStore, nameof(walletStore));

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
            using (var storeTransaction = this.walletStore.Begin())
            {
                wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode);

                CreateWalletAccount(wallet, password);

                storeTransaction.Commit();
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
            using (var storeTransaction = this.walletStore.Begin())
            {
                // Create a wallet.
                string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
                wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode, creationTime);

                HdAccount account = CreateWalletAccount(wallet, password);

                storeTransaction.Commit();
            }

            this.OnWalletRecovered(wallet, creationTime);

            return wallet;
        }

        /// <inheritdoc />
        public IWallet RecoverWallet(string walletName, ExtPubKey extPubKey, int accountIndex, DateTime creationTime)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotNull(extPubKey, nameof(extPubKey));
            this.logger.LogDebug("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(walletName), walletName, nameof(extPubKey), extPubKey, nameof(accountIndex), accountIndex);

            IWallet wallet = null;
            using (var storeTransaction = this.walletStore.Begin())
            {
                // Create a wallet file.
                wallet = this.GenerateExtPubKeyOnlyWallet(walletName, creationTime);

                // Generate account
                HdAccount account = wallet.AddNewAccount(extPubKey, accountIndex, this.dateTimeProvider.GetTimeOffset());
                this.walletStore.AddAccount(walletName, account);

                this.FillAddressPool(account);

                storeTransaction.Commit();
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
            HdAddress hdAddress = this.walletStore.GetAddress(walletName, address).ThrowIfNull();

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
            HdAddress hdAddress = this.walletStore.GetAddress(walletName, externalAddress).ThrowIfNull();

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

            IWallet wallet = this.GetWalletByName(name);

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
            IEnumerable<IWallet> wallets = this.walletStore.GetAllWallets();
            foreach (var wallet in wallets)
            {
                foreach (HdAccount account in this.walletStore.GetWalletAccounts(wallet.Name))
                {
                    using (var storeTransaction = this.walletStore.Begin())
                    {
                        this.FillAddressPool(account);
                        storeTransaction.Commit();
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

            HdAccount account = this.walletStore.GetFirstUnusedAccount(walletName);
            if (account != null)
            {
                this.logger.LogTrace("(-)[ACCOUNT_FOUND]");
                return account;
            }
            else
            {
                using (var storeTransaction = this.walletStore.Begin())
                {
                    // No unused account was found, create a new one.
                    account = CreateWalletAccount(wallet, password);
                    this.OnWalletAccountCreated(wallet, account);
                }
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

            this.walletStore.SetWalletTip(wallet.Name, chainedHeader);
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

        /// <inheritdoc />
        public IWallet GetWallet(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            IWallet wallet = this.GetWalletByName(walletName);

            return wallet;
        }


        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            // In order to calculate the fee properly we need to retrieve all the transactions with spending details.
            IWallet wallet = this.GetWalletByName(walletName);


            var accounts = new List<HdAccount>();
            if (!string.IsNullOrEmpty(accountName))
            {
                HdAccount account = this.GetAccountByName(wallet, accountName);
                accounts.Add(account);
            }
            else
            {
                IEnumerable<HdAccount> walletAccounts = this.walletStore.GetWalletAccounts(walletName, AccountFilters.NormalAccounts);
                accounts.AddRange(walletAccounts);
            }

            IEnumerable<AccountHistory> accountsHistory = this.walletStore.GetHistory(walletName, accounts);

            return accountsHistory;
        }

        /// <inheritdoc />
        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null)
        {
            IWallet wallet = this.GetWalletByName(walletName);

            var accounts = new List<HdAccount>();
            if (!string.IsNullOrEmpty(accountName))
            {
                HdAccount account = this.GetAccountByName(wallet, accountName);
                accounts.Add(account);
            }
            else
            {
                IEnumerable<HdAccount> walletAccounts = this.walletStore.GetWalletAccounts(walletName, AccountFilters.NormalAccounts);
                accounts.AddRange(walletAccounts);
            }

            IEnumerable<AccountBalance> balances = this.walletStore.GetBalances(walletName, accounts);

            return balances;
        }

        /// <inheritdoc />
        public AddressBalance GetAddressBalance(string walletName, string address)
        {
            Guard.NotEmpty(address, nameof(address));

            AddressBalance balance = this.walletStore.GetAddressBalance(walletName, address);

            return balance;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1).Single();

            return res;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1, true).Single();

            return res;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isInternal = false)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);

            IWallet wallet = this.GetWalletByName(accountReference.WalletName);

            using (var storeTransaction = this.walletStore.Begin())
            {
                // Get the account.
                HdAccount account = this.GetAccountByName(wallet, accountReference.AccountName);

                var unusedAddresses = this.walletStore.GetUnusedAddresses(accountReference, count, isInternal);

                //if there aren't enough unused address, fill them with the required amount
                int missingAddresses = count - unusedAddresses.Count();
                if (missingAddresses > 0)
                {
                    IEnumerable<HdAddress> newAddresses = account.CreateAddresses(this.network, missingAddresses, isInternal);
                    storeTransaction.Commit();

                    this.signals.Publish(new Events.WalletAddressesCreated(wallet, account, newAddresses));
                    return unusedAddresses.Concat(newAddresses);
                }
                else
                {
                    return unusedAddresses;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            IWallet wallet = this.GetWalletByName(walletName);

            return this.walletStore.GetWalletAccounts(walletName);
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            IWallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
            HdAccount account = this.GetAccountByName(wallet, walletAccountReference.AccountName);

            return this.walletStore.GetSpendableTransactions(walletAccountReference, this.chainIndexer.Tip.Height, this.network.Consensus.CoinbaseMaturity, confirmations);
        }

        /// <inheritdoc />
        public IEnumerable<TransactionData> GetAllUnspentTransactions(string walletName)
        {
            return this.walletStore.GetAllUnspentTransactions(walletName);
        }

        /// <inheritdoc />
        public IEnumerable<Script> GetAllPubKeys(string walletName)
        {
            return this.walletStore.GetAllPubKeys(walletName);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetAllAddresses(string walletName)
        {
            return this.walletStore.GetAllAddresses(walletName);
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
        /// <param name="accountReference">The account.</param>
        /// <returns>New change and receiving address that has been generated.</returns>
        private (IEnumerable<HdAddress> newChangeAddresses, IEnumerable<HdAddress> newReceivingAddresses) FillAddressPool(WalletAccountReference accountReference)
        {
            IEnumerable<HdAddress> newChangeAddresses = this.AddAddressesToMaintainBuffer(accountReference, true);
            this.walletStore.AddAddress(accountReference, newChangeAddresses);
            this.hdAddressLookup.TrackAddresses(newChangeAddresses);

            IEnumerable<HdAddress> newReceivingAddresses = this.AddAddressesToMaintainBuffer(accountReference, false);
            this.walletStore.AddAddress(accountReference, newReceivingAddresses);
            this.hdAddressLookup.TrackAddresses(newReceivingAddresses);

            return (newChangeAddresses, newReceivingAddresses);
        }

        /// <summary>
        /// Ensures that the specified account has enough available internal (when <paramref name="isInternal"/> is true) or external (when <paramref name="isInternal"/> is false) available addresses.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account</param>
        /// <param name="isInternal">Specifies the address set to check: internals (if set to <c>true</c>) or externals (if set to <c>false</c>).</param>
        /// <returns>List of created addresses to fill the internal/external addresses buffer.</returns>
        private IEnumerable<HdAddress> AddAddressesToMaintainBuffer(WalletAccountReference accountReference, bool isInternal)
        {
            HdAddress lastUsedAddress = this.walletStore.GetLastUsedAddress(accountReference, isInternal);
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
        /// <remarks>Doesn't commit changes to the store, need an explicit call to Commit.</remarks>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private IWallet GenerateWallet(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.walletStore.GetWalletByName(name) != null)
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletDuplicateNameException($"Wallet with name '{name}' already exists.");
            }

            if (this.walletStore.GetWalletByEncryptedSeed(encryptedSeed) != null)
            {
                this.logger.LogTrace("(-)[SAME_PK_ALREADY_EXISTS]");
                throw new WalletDuplicateEncryptedSeedException("A wallet with the same private key already exists.");
            }

            IWallet wallet = this.walletStore.AddWallet(new Wallet
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
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private IWallet GenerateExtPubKeyOnlyWallet(string walletName, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.walletStore.GetWalletByName(walletName) != null)
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{walletName}' already exists.");
            }

            var wallet = this.walletStore.AddWallet(new Wallet
            {
                Name = walletName,
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
            this.walletStore.AddAccount(wallet.Name, account);

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
            return this.walletStore.GetWalletByName(walletName).ThrowIfNull();
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
            return this.walletStore.GetAccountByName(wallet.Name, accountName).ThrowIfNull();
        }
        #endregion
    }
}
