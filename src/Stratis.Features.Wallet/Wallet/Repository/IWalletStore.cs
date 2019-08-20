using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    public interface ITransactionScope : IDisposable
    {
        ITransactionScope Begin();
        void Commit();
        void Rollback();
    }

    /// <summary>
    /// Wallet repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IWalletStore : ITransactionScope
    {
        #region Wallet

        /// <summary>
        /// Adds the wallet into the store.
        /// </summary>
        /// <param name="wallet">The wallet instance to add to the store.</param>
        /// <returns>The added wallet instance</returns>
        IWallet AddWallet(IWallet wallet);

        /// <summary>
        /// Gets all wallets.
        /// </summary>
        /// <returns>Collection of wallets found in the store.</returns>
        IEnumerable<IWallet> GetAllWallets();

        /// <summary>
        /// Gets the wallet by name.
        /// </summary>
        /// <param name="walletName">Name of the wallet to fetch.</param>
        /// <returns>
        /// The found wallet, or <see langword="null" />.
        /// </returns>
        IWallet GetWalletByName(string walletName);

        /// <summary>
        /// Gets the wallet by encryptedSeed.
        /// </summary>
        /// <param name="encryptedSeed">The encrypted seed.</param>
        /// <returns>
        /// The found wallet, or <see langword="null" />.
        /// </returns>
        IWallet GetWalletByEncryptedSeed(string encryptedSeed);

        /// <summary>
        /// Sets the wallet tip (Hash and Height).
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="tip">The tip.</param>
        /// <returns>
        /// The wallet.
        /// </returns>
        IWallet SetWalletTip(string walletName, ChainedHeader tip);
        #endregion

        #region Account

        /// <summary>
        /// Adds the account into the store.
        /// </summary>
        /// <param name="walletName">Name of the wallet that owns the account.</param>
        /// <param name="account">The account to add.</param>
        /// <returns>
        /// The instance of the added account.
        /// </returns>
        HdAccount AddAccount(string walletName, HdAccount account);

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns>
        /// The HD account specified by the parameter or <c>null</c> if the account does not exist.
        /// </returns>
        HdAccount GetAccountByName(string walletName, string accountName);

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>An unused account</returns>
        HdAccount GetFirstUnusedAccount(string walletName);

        /// <summary>
        /// Gets the wallet accounts.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>The list of wallet accounts</returns>
        IEnumerable<HdAccount> GetWalletAccounts(string walletName);

        /// <summary>
        /// Gets the wallet accounts using the specified filter.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="accountFilter">The account filter to apply.</param>
        /// <returns>The list of wallet accounts</returns>
        IEnumerable<HdAccount> GetWalletAccounts(string walletName, Func<HdAccount, bool> accountFilter);

        #endregion

        #region Address

        /// <summary>
        /// Adds the addresses to the store.
        /// </summary>
        /// <param name="account">The account that own the addresses.</param>
        /// <param name="addressesToAdd">The addresses to add.</param>
        void AddAddress(HdAccount account, IEnumerable<HdAddress> addressesToAdd);

        /// <summary>
        /// Finds the HD address for the address.
        /// </summary>
        /// <remarks>
        /// Returns an HDAddress.
        /// </remarks>
        /// <param name="address">An address.</param>
        /// <returns>HD Address</returns>
        HdAddress GetAddress(string address);

        /// <summary>
        /// Gets all wallet addresses.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>Both internal and external wallet addresses.</returns>
        IEnumerable<HdAddress> GetAllAddresses(string walletName);

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>
        /// A list of all the public keys contained in the wallet.
        /// </returns>
        IEnumerable<Script> GetAllPubKeys(string walletName);

        void Add(IEnumerable<HdAddress> newReceivingAddresses);
        #endregion

        #region Transaction

        /// <summary>
        /// Gets all wallet transactions that are unspent or spent but with 0 confirmations.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>
        /// List of wallet related transactions.
        /// </returns>
        IEnumerable<TransactionData> GetAllUnspentTransactions(string walletName);

        /// <summary>
        /// Lists all spendable transactions in the current account.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
        /// <param name="coinbaseMaturity">The coinbase maturity after which a coinstake transaction is spendable.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        /// <remarks>Note that coinbase and coinstake transaction outputs also have to mature with a sufficient number of confirmations before
        /// they are considered spendable. This is independent of the confirmations parameter.</remarks>
        IEnumerable<UnspentOutputReference> GetSpendableTransactions(WalletAccountReference accountReference, int currentChainHeight, long coinbaseMaturity, int confirmations);
        #endregion

        /// <summary>
        /// Gets the history of transactions contained in an account.
        /// If no account is specified, history will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accounts">A collection of accounts to get history from.</param>
        /// <returns>Collection of address history and transaction pairs.</returns>
        IEnumerable<AccountHistory> GetHistory(string walletName, IEnumerable<HdAccount> accounts);

        /// <summary>
        /// Gets the balance of transactions contained in one or more account of a specified wallet.
        /// If no account is specified, balances will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accounts">A collection of accounts to get balance from.</param>
        /// <returns>Collection of account balances.</returns>
        IEnumerable<AccountBalance> GetBalances(string walletName, IEnumerable<HdAccount> accounts);

        /// <summary>
        /// Gets the balance of transactions for this specific address.
        /// </summary>
        /// <param name="address">The address to get the balance from.</param>
        /// <returns>The address balance for an address.</returns>
        AddressBalance GetAddressBalance(string address);

        /// <summary>
        /// Gets a collection of unused receiving or change addresses.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <param name="count">The number of addresses to retrieve.</param>
        /// <param name="isInternalAddress">A value indicating whether or not the addresses to get should internal or external addresses.</param>
        /// <returns>A list of unused addresses. New addresses will be created as necessary.</returns>
        IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isInternalAddress);
    }
}
