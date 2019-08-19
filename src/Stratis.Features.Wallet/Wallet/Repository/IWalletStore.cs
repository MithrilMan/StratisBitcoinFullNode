using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// Wallet repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IWalletStore
    {
        #region Wallet

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
        #endregion

        #region Address

        /// <summary>
        /// Finds the HD address for the address.
        /// </summary>
        /// <remarks>
        /// Returns an HDAddress.
        /// </remarks>
        /// <param name="address">An address.</param>
        /// <returns>HD Address</returns>
        HdAddress GetAddress(string address);

        IEnumerable<Script> GetAllPubKeys(long hdAccountId);

        void Add(IEnumerable<HdAddress> newReceivingAddresses);
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
        /// <param name="count">The number of addresses to create.</param>
        /// <param name="isChange">A value indicating whether or not the addresses to get should be receiving or change addresses.</param>
        /// <returns>A list of unused addresses. New addresses will be created as necessary.</returns>
        IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange);
    }
}
