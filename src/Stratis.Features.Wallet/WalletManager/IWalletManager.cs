﻿using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IWalletManager: IWalletUseCases
    {
        /// <summary>
        /// Starts this wallet manager.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the wallet manager.
        /// <para>Internally it waits for async loops to complete before saving the wallets to disk.</para>
        /// </summary>
        void Stop();

        /// <summary>
        /// The last processed block.
        /// </summary>
        uint256 WalletTipHash { get; set; }

        /// <summary>
        /// The last processed block height.
        /// </summary>
        int WalletTipHeight { get; set; }

        /// <summary>
        /// Helps identify UTXO's that can participate in staking.
        /// </summary>
        /// <returns>A dictionary containing string and template pairs - e.g. { "P2PK", PayToPubkeyTemplate.Instance }</returns>
        Dictionary<string, ScriptTemplate> GetValidStakingTemplates();

        /// <summary>
        /// Returns additional transaction builder extensions to use for building staking transactions.
        /// </summary>
        /// <returns>Transaction builder extensions to use for building staking transactions.</returns>
        IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking();

        /// <summary>
        /// Lists all spendable transactions from the account specified in <see cref="WalletAccountReference" />.
        /// </summary>
        /// <param name="walletAccountReference">The wallet account reference.</param>
        /// <param name="confirmations">The confirmations.</param>
        /// <returns>
        /// A collection of spendable outputs that belong to the given account.
        /// </returns>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0);

        /// <summary>
        /// Deletes a wallet.
        /// </summary>
        void DeleteWallet();

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <returns>An unused change address or a newly created change address, in Base58 format.</returns>
        HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference);

        /// <summary>
        /// Gets a list of accounts.
        /// </summary>
        /// <param name="walletName">The name of the wallet to look into.</param>
        /// <returns>The list of wallet accounts.</returns>
        IEnumerable<HdAccount> GetAccounts(string walletName);

        /// <summary>
        /// Gets the last block height.
        /// </summary>
        /// <returns>The height of tip of the wallet.</returns>
        int LastBlockHeight();

        /// <summary>
        /// Remove all the transactions in the wallet that are above this block height
        /// </summary>
        /// <param name="fork">The fork.</param>
        void RemoveBlocks(ChainedHeader fork);

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="chainedHeader">The blocks chain of headers.</param>
        void ProcessBlock(Block block, ChainedHeader chainedHeader);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="blockHeight">The height of the block this transaction came from. Null if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included.</param>
        /// <param name="isPropagated">Transaction propagation state.</param>
        /// <returns>A value indicating whether this transaction affects the wallet.</returns>
        bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true);

        /// <summary>
        /// Gets the extension of the wallet files.
        /// </summary>
        /// <returns>The wallet file extension, if any.</returns>
        string GetWalletFileExtension();

        /// <summary>
        /// Gets all the wallets' names.
        /// </summary>
        /// <returns>A collection of the wallets' names.</returns>
        IEnumerable<string> GetWalletsNames();

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        HashHeightPair LastReceivedBlockInfo();

        /// <summary>
        /// Gets a wallet given its name.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get.</param>
        /// <returns>A wallet or null if it doesn't exist</returns>
        IWallet GetWalletByName(string walletName);

        /// <summary>
        /// Gets the block locator of the first loaded wallet.
        /// </summary>
        /// <returns></returns>
        ICollection<uint256> GetFirstWalletBlockLocator();

        /// <summary>
        /// Gets the list of the wallet filenames, along with the folder in which they're contained.
        /// </summary>
        /// <returns>The wallet filenames, along with the folder in which they're contained.</returns>
        (string folderPath, IEnumerable<string>) GetWalletsFiles();

        /// <summary>
        /// Gets whether there are any wallet files loaded or not.
        /// </summary>
        /// <returns>Whether any wallet files are loaded.</returns>
        bool ContainsWallets { get; }

        /// <summary>
        /// Gets the lowest LastBlockSyncedHeight of all loaded wallet account roots.
        /// </summary>
        /// <returns>The lowest LastBlockSyncedHeight or null if there are no account roots yet.</returns>
        int? GetEarliestWalletHeight();

        /// <summary>
        /// Gets the oldest wallet creation time.
        /// </summary>
        /// <returns>The oldest wallet creation date.</returns>
        DateTimeOffset GetOldestWalletCreationTime();

        /// <summary>
        /// Removes the specified transactions from the wallet and persist it.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <param name="transactionsIds">The IDs of transactions to remove.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds);

        /// <summary>
        /// Removes all the transactions from the wallet and persist it.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName);

        /// <summary>
        /// Removes all the transactions that occurred after a certain date.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <param name="fromDate">The date after which the transactions should be removed.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate);
    }
}
