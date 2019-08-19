using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Wallet.Broadcasting;
using Stratis.Features.Wallet.Events;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        protected readonly object lockObject;

        /// <summary>Factory for creating background async loop tasks.</summary>
        protected readonly IAsyncProvider asyncProvider;

        /// <summary>The type of coin used in this manager.</summary>
        protected readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        protected readonly Network network;

        /// <summary>The chain of headers.</summary>
        protected readonly ChainIndexer chainIndexer;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        protected readonly INodeLifetime nodeLifetime;

        /// <summary>Instance logger.</summary>
        protected readonly ILogger logger;

        /// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
        protected readonly FileStorage<Wallet> fileStorage;

        /// <summary>The broadcast manager.</summary>
        protected readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the wallet feature.</summary>
        protected readonly WalletSettings walletSettings;

        /// <summary>The settings for the wallet feature.</summary>
        protected readonly IScriptAddressReader scriptAddressReader;
        protected readonly ISignals signals;
        protected readonly IWalletService walletService;

        public uint256 WalletTipHash { get; set; }
        public int WalletTipHeight { get; set; }

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. wallet addresses in order to allow faster look-ups of transactions affecting the wallets' addresses.
        // 3. a mapping of all inputs with their corresponding transactions, to facilitate rapid lookup
        private readonly IOutPointLookup outpointLookup;
        private readonly IHdAddressLookup hdAddressLookup;
        private readonly IOutPointLookup inputLookup;

        #region NEW members
        protected ConcurrentBag<IWallet> loadedWallets;

        /// <summary>
        /// The event subscriptions list that holds the component active subscriptions.
        /// </summary>
        protected List<SubscriptionToken> eventSubscriptions;
        #endregion

        public WalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ChainIndexer chainIndexer,
            WalletSettings walletSettings,
            DataFolder dataFolder,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IScriptAddressReader scriptAddressReader,
            ISignals signals,
            IWalletService walletService,
            IHdAddressLookup hdAddressLookup,
            IOutPointLookup outpointLookup,
            IOutPointLookup inputLookup,
            IBroadcasterManager broadcasterManager = null) // no need to know about transactions the node will broadcast to.
        {
            Guard.NotNull(dataFolder, nameof(dataFolder));

            this.network = Guard.NotNull(network, nameof(network));
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chainIndexer = Guard.NotNull(chainIndexer, nameof(chainIndexer));
            this.walletSettings = Guard.NotNull(walletSettings, nameof(walletSettings));
            this.asyncProvider = Guard.NotNull(asyncProvider, nameof(asyncProvider));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.fileStorage = new FileStorage<Wallet>(dataFolder.WalletPath);
            this.scriptAddressReader = Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.walletService = Guard.NotNull(walletService, nameof(walletService));
            this.dateTimeProvider = Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            this.hdAddressLookup = Guard.NotNull(hdAddressLookup, nameof(hdAddressLookup));
            this.outpointLookup = Guard.NotNull(outpointLookup, nameof(outpointLookup));
            this.inputLookup = Guard.NotNull(inputLookup, nameof(inputLookup));
            this.broadcasterManager = broadcasterManager;

            this.lockObject = new object();
            this.logger = Guard.NotNull(loggerFactory, nameof(loggerFactory)).CreateLogger(this.GetType().FullName);

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }

            this.loadedWallets = new ConcurrentBag<IWallet>();
            this.eventSubscriptions = new List<SubscriptionToken>();
        }

        private void subscribeToEvents()
        {
            lock (this.eventSubscriptions)
            {
                this.eventSubscriptions.Add(this.signals.Subscribe<Events.WalletLoaded>(OnWalletLoaded));
                this.eventSubscriptions.Add(this.signals.Subscribe<Events.WalletCreated>(OnWalletCreated));
                this.eventSubscriptions.Add(this.signals.Subscribe<Events.WalletRecovered>(OnWalletRecovered));
                this.eventSubscriptions.Add(this.signals.Subscribe<Events.WalletAccountCreated>(OnWalletAccountCreated));
            }
        }

        private void unsubscribeToEvents()
        {
            lock (this.eventSubscriptions)
            {
                this.eventSubscriptions?.ForEach(subscription => this.signals.Unsubscribe(subscription));

                this.eventSubscriptions.Clear();
            }
        }

        /// <inheritdoc />
        public virtual Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate> {
                { "P2PK", PayToPubkeyTemplate.Instance },
                { "P2PKH", PayToPubkeyHashTemplate.Instance } };
        }

        // <inheritdoc />
        public virtual IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            return new List<BuilderExtension>();
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
            }
            else
            {
                this.logger.LogDebug("Exception occurred: {0}", transactionEntry.ErrorMessage);
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
        }

        public virtual void Start()
        {
            this.walletService.LoadWallets();

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            HashHeightPair hashHeightPair = this.LastReceivedBlockInfo();
            this.WalletTipHash = hashHeightPair.Hash;
            this.WalletTipHeight = hashHeightPair.Height;

            this.subscribeToEvents();
        }

        /// <inheritdoc />
        public virtual void Stop()
        {
            this.unsubscribeToEvents();

            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;
        }



        /// <inheritdoc />
        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1, true).Single();

            return res;
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWalletByName(walletName);

            HdAccount[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAccounts().ToArray();
            }
            return res;
        }

        /// <inheritdoc />
        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                int height = this.chainIndexer.Tip.Height;
                this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
                return height;
            }

            int res;
            lock (this.lockObject)
            {
                res = this.Wallets.Min(w => w.AccountsRoot.Single().LastBlockSyncedHeight) ?? 0;
            }

            return res;
        }

        /// <inheritdoc />
        public bool ContainsWallets => this.Wallets.Any();

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public HashHeightPair LastReceivedBlockInfo()
        {
            if (!this.Wallets.Any())
            {
                ChainedHeader chainedHeader = this.chainIndexer.Tip;
                this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", chainedHeader);
                return new HashHeightPair(chainedHeader);
            }

            AccountRoot accountRoot;
            lock (this.lockObject)
            {
                accountRoot = this.Wallets
                    .Select(w => w.AccountsRoot.Single())
                    .Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault();

                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (accountRoot == null || accountRoot.LastBlockSyncedHash == null)
                {
                    this.logger.LogWarning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = this.Wallets.Min(c => c.CreationTime);
                    this.UpdateWhenChainDownloaded(this.Wallets, earliestWalletDate.DateTime);
                    return new HashHeightPair(this.chainIndexer.Tip);
                }
            }

            return new HashHeightPair(accountRoot.LastBlockSyncedHash, accountRoot.LastBlockSyncedHeight.Value);
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            Wallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccount(walletAccountReference.AccountName);

                if (account == null)
                {
                    this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
                    throw new WalletException(
                        $"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
                }

                res = account.GetSpendableTransactions(this.chainIndexer.Tip.Height, this.network.Consensus.CoinbaseMaturity, confirmations).ToArray();
            }

            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));

            lock (this.lockObject)
            {
                IEnumerable<HdAddress> allAddresses = this.scriptToAddressLookup.Values;
                foreach (HdAddress address in allAddresses)
                {
                    // Remove all the UTXO that have been reorged.
                    IEnumerable<TransactionData> makeUnspendable = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                    foreach (TransactionData transactionData in makeUnspendable)
                        address.Transactions.Remove(transactionData);

                    // Bring back all the UTXO that are now spendable after the reorg.
                    IEnumerable<TransactionData> makeSpendable = address.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                    foreach (TransactionData transactionData in makeSpendable)
                        transactionData.SpendingDetails = null;
                }

                this.UpdateLastBlockSyncedHeight(fork);

                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (!this.Wallets.Any())
            {
                this.WalletTipHash = chainedHeader.HashBlock;
                this.WalletTipHeight = chainedHeader.Height;
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != this.WalletTipHash)
            {
                this.logger.LogDebug("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, this.WalletTipHash);

                // The block coming in to the wallet should never be ahead of the wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > this.WalletTipHeight)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the wallet");
                }
            }

            lock (this.lockObject)
            {
                bool trxFoundInBlock = false;
                foreach (Transaction transaction in block.Transactions)
                {
                    bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                    if (trxFound)
                    {
                        trxFoundInBlock = true;
                    }
                }

                // Update the wallets with the last processed block height.
                // It's important that updating the height happens after the block processing is complete,
                // as if the node is stopped, on re-opening it will start updating from the previous height.
                this.UpdateLastBlockSyncedHeight(chainedHeader);

                if (trxFoundInBlock)
                {
                    this.logger.LogDebug("Block {0} contains at least one transaction affecting the user's wallet(s).", chainedHeader);
                }
            }
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this.lockObject)
            {
                if (block != null)
                {
                    // Do a pre-scan of the incoming transaction's inputs to see if they're used in other wallet transactions already.
                    foreach (TxIn input in transaction.Inputs)
                    {
                        // See if this input is being used by another wallet transaction present in the index.
                        // The inputs themselves may not belong to the wallet, but the transaction data in the index has to be for a wallet transaction.
                        TransactionData indexData = this.inputLookup.Get(input.PrevOut);
                        if (indexData != null)
                        {
                            // It's the same transaction, which can occur if the transaction had been added to the wallet previously. Ignore.
                            if (indexData.Id == hash)
                                continue;

                            if (indexData.BlockHash != null)
                            {
                                // This should not happen as pre checks are done in mempool and consensus.
                                throw new WalletException("The same inputs were found in two different confirmed transactions");
                            }

                            // This is a double spend we remove the unconfirmed trx
                            this.RemoveTransactionsByIds(new[] { indexData.Id });
                            this.inputLookup.Remove(input.PrevOut);
                        }
                    }
                }

                // Check the outputs, ignoring the ones with a 0 amount.
                foreach (TxOut utxo in transaction.Outputs.Where(o => o.Value != Money.Zero))
                {
                    // Check if the outputs contain one of our addresses.
                    if (this.scriptToAddressLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress _))
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                        this.logger.LogDebug("Transaction '{0}' contained funds received by the user's wallet(s).", hash);
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    TransactionData tTx = this.outpointLookup.Get(input.PrevOut);
                    if (tTx == null)
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.IsEmpty)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        bool found = this.scriptToAddressLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        return !addr.IsChangeAddress() && !transaction.IsCoinStake;
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                    this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's wallet(s).", hash);
                }
            }

            return foundSendingTrx || foundReceivingTrx;
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            this.scriptToAddressLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.LogDebug("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsPropagated = isPropagated,
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
                this.AddInputKeysLookupLocked(newTransaction);

                if (block == null)
                {
                    // Unconfirmed inputs track for double spends.
                    this.AddTxLookupLocked(newTransaction, transaction);
                }
            }
            else
            {
                this.logger.LogDebug("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                    foundTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                if (isPropagated)
                    foundTransaction.IsPropagated = true;

                if (block != null)
                {
                    // Inputs are in a block no need to track them anymore.
                    this.RemoveTxLookupLocked(transaction);
                }
            }


            this.TransactionFoundInternal(script);
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        private void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            uint256 transactionHash = transaction.GetHash();

            // Get the transaction being spent.
            TransactionData spentTransaction = this.scriptToAddressLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                this.logger.LogDebug("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                var payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, paidToOutput.ScriptPubKey);
                    if (string.IsNullOrEmpty(destinationAddress))
                        if (this.scriptToAddressLookup.TryGetValue(paidToOutput.ScriptPubKey, out HdAddress destination))
                            destinationAddress = destination.Address;

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value,
                        OutputIndex = transaction.Outputs.IndexOf(paidToOutput)
                    });
                }

                var spendingDetails = new SpendingDetails
                {
                    TransactionId = transactionHash,
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    BlockHeight = blockHeight,
                    BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                this.logger.LogDebug("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                    spentTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
                }
            }

            // If the transaction is spent and confirmed, we remove the UTXO from the lookup dictionary.
            if (spentTransaction.SpendingDetails.BlockHeight != null)
            {
                this.RemoveInputKeysLookupLock(spentTransaction);
            }
        }

        public virtual void TransactionFoundInternal(Script script, Func<HdAccount, bool> accountFilter = null)
        {
            foreach (Wallet wallet in this.Wallets)
            {
                foreach (HdAccount account in wallet.GetAccounts(accountFilter ?? Wallet.NormalAccounts))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = true;
                    }
                    else
                    {
                        continue;
                    }

                    IEnumerable<HdAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

                    this.UpdateKeysLookupLocked(newAddresses);
                }
            }
        }

        private IEnumerable<HdAddress> AddAddressesToMaintainBuffer(HdAccount account, bool isChange)
        {
            HdAddress lastUsedAddress = account.GetLastUsedAddress(isChange);
            int lastUsedAddressIndex = lastUsedAddress?.Index ?? -1;
            int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
            int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
            int addressesToAdd = this.walletSettings.UnusedAddressesBuffer - emptyAddressesCount;

            return addressesToAdd > 0 ? account.CreateAddresses(this.network, addressesToAdd, isChange) : new List<HdAddress>();
        }

        /// <inheritdoc />
        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }



        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // Update the wallets with the last processed block height.
            foreach (Wallet wallet in this.Wallets)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedHeader);
            }

            this.WalletTipHash = chainedHeader.HashBlock;
            this.WalletTipHeight = chainedHeader.Height;
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                foreach (IWallet wallet in this.loadedWallets)
                {
                    IEnumerable<HdAddress> walletAddresses = this.walletService.GetAllWalletAddresses(wallet.Name);
                    this.hdAddressLookup.TrackAddresses(walletAddresses);

                    IEnumerable<TransactionData> walletTransactions = this.walletService.GetAllWalletTransactions(wallet.Name);
                    this.hdAddressLookup.TrackAddresses(walletAddresses);

                    foreach (TransactionData transaction in walletTransactions)
                    {
                        // Get the UTXOs that are unspent or spent but not confirmed.
                        // We only exclude from the list the confirmed spent UTXOs.
                        if (transaction.SpendingDetails?.BlockHeight == null)
                        {
                            this.outpointLookup.Add(transaction);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        /// <param name="addresses">The addresses to keep track of.</param>
        public void UpdateKeysLookupLocked(IEnumerable<HdAddress> addresses)
        {
            // TODO: replace this method with direct call to hdAddressLookup component when refactor is done.
            this.hdAddressLookup.TrackAddresses(addresses);
        }

        /// <summary>
        /// Add to the list of unspent outputs kept in memory for faster lookups.
        /// </summary>
        private void AddInputKeysLookupLocked(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup.Add(transactionData);
            }
        }

        /// <summary>
        /// Remove from the list of unspent outputs kept in memory.
        /// </summary>
        private void RemoveInputKeysLookupLock(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));
            Guard.NotNull(transactionData.SpendingDetails, nameof(transactionData.SpendingDetails));

            lock (this.lockObject)
            {
                this.outpointLookup.Remove(transactionData);
            }
        }

        /// <summary>
        /// Reloads the UTXOs we're tracking in memory for faster lookups.
        /// </summary>
        public void RefreshInputKeysLookupLock()
        {
            lock (this.lockObject)
            {
                this.outpointLookup.Clear();

                foreach (Wallet wallet in this.loadedWallets)
                {
                    foreach (HdAddress address in wallet.GetAllAddresses(a => true))
                    {
                        // Get the UTXOs that are unspent or spent but not confirmed.
                        // We only exclude from the list the confirmed spent UTXOs.
                        foreach (TransactionData transaction in address.Transactions.Where(t => t.SpendingDetails?.BlockHeight == null))
                        {
                            this.outpointLookup.Add(transaction);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add to the mapping of transactions kept in memory for faster lookups.
        /// </summary>
        private void AddTxLookupLocked(TransactionData transactionData, Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                {
                    this.inputLookup.Add(input, transactionData);
                }
            }
        }

        private void RemoveTxLookupLocked(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            lock (this.lockObject)
            {
                foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                {
                    this.inputLookup.Remove(input);
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> GetWalletsNames()
        {
            return this.Wallets.Select(w => w.Name);
        }

        /// <inheritdoc />
        public Wallet GetWalletByName(string walletName)
        {
            Wallet wallet = this.Wallets.SingleOrDefault(w => w.Name == walletName);
            if (wallet == null)
            {
                this.logger.LogTrace("(-)[WALLET_NOT_FOUND]");
                throw new WalletException($"No wallet with name '{walletName}' could be found.");
            }

            return wallet;
        }

        /// <inheritdoc />
        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            return this.Wallets.First().BlockLocator;
        }

        /// <inheritdoc />
        public int? GetEarliestWalletHeight()
        {
            return this.Wallets.Min(w => w.AccountsRoot.Single().LastBlockSyncedHeight);
        }

        /// <inheritdoc />
        public DateTimeOffset GetOldestWalletCreationTime()
        {
            return this.Wallets.Min(w => w.CreationTime);
        }

        /// <summary>
        /// Search all wallets and removes the specified transactions from the wallet and persist it.
        /// </summary>
        private void RemoveTransactionsByIds(IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));

            foreach (Wallet wallet in this.Wallets)
            {
                this.RemoveTransactionsByIds(wallet.Name, transactionsIds);
            }
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));
            Guard.NotEmpty(walletName, nameof(walletName));

            List<uint256> idsToRemove = transactionsIds.ToList();
            Wallet wallet = this.GetWallet(walletName);

            var result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccounts(a => true);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        for (int i = 0; i < address.Transactions.Count; i++)
                        {
                            TransactionData transaction = address.Transactions.ElementAt(i);

                            // Remove the transaction from the list of transactions affecting an address.
                            // Only transactions that haven't been confirmed in a block can be removed.
                            if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
                            {
                                result.Add((transaction.Id, transaction.CreationTime));
                                address.Transactions = address.Transactions.Except(new[] { transaction }).ToList();
                                i--;
                            }

                            // Remove the spending transaction object containing this transaction id.
                            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                                address.Transactions.ElementAt(i).SpendingDetails = null;
                            }
                        }
                    }
                }
            }

            if (result.Any())
            {
                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();

                this.SaveWallet(wallet);
            }

            return result;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccounts();
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        removedTransactions.UnionWith(address.Transactions.Select(t => (t.Id, t.CreationTime)));
                        address.Transactions.Clear();
                    }
                }

                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(wallet);
            }

            return removedTransactions;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccounts();
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        var toRemove = address.Transactions.Where(t => t.CreationTime > fromDate).ToList();
                        foreach (var trx in toRemove)
                        {
                            removedTransactions.Add((trx.Id, trx.CreationTime));
                            address.Transactions.Remove(trx);
                        }
                    }
                }

                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(wallet);
            }

            return removedTransactions;
        }

        #region ISignals event handlers

        /// <summary>
        /// Called when [wallet loaded].
        /// </summary>
        /// <param name="event">The event.</param>
        protected virtual void OnWalletLoaded(WalletLoaded @event)
        {
            this.loadedWallets.Add(@event.Wallet);
        }

        /// <summary>
        /// Called when [wallet created].
        /// </summary>
        /// <param name="event">The event.</param>
        protected virtual void OnWalletCreated(WalletCreated @event)
        {
            // Created wallets are added to the loaded wallets list too.
            this.loadedWallets.Add(@event.Wallet);
        }

        /// <summary>
        /// Called when [wallet recovered].
        /// </summary>
        /// <param name="event">The @event.</param>
        protected virtual void OnWalletRecovered(WalletRecovered @event)
        {
            // Recovered wallets are added to the loaded wallets list too.
            this.loadedWallets.Add(@event.Wallet);
        }

        /// <summary>
        /// Called when [wallet recovered].
        /// </summary>
        /// <param name="event">The @event.</param>
        protected virtual void OnWalletAccountCreated(WalletAccountCreated @event)
        {
        }
        #endregion
    }
}
