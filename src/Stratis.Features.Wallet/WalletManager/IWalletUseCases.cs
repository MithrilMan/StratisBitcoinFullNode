using System;
using System.Collections.Generic;
using NBitcoin;
using TracerAttributes;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// temporary interface to store wallet use cases for WalletManager
    /// </summary>
    public interface IWalletUseCases
    {
        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        void SetLastBlockDetails(long walletId, ChainedHeader block);

        #region HdAccount

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="extPubKey">The extended public key for the wallet<see cref="EncryptedSeed"/>.</param>
        /// <param name="accountIndex">Zero-based index of the account to add.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        HdAccount AddNewAccount(long walletId, ExtPubKey extPubKey, int accountIndex, DateTimeOffset accountCreationTime);

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="accountIndex">The index at which an account will be created. If left null, a new account will be created after the last used one.</param>
        /// <param name="accountName">The name of the account to be created. If left null, an account will be created according to the <see cref="Wallet.AccountNamePattern"/>.</param>
        /// <returns>A new HD account.</returns>
        HdAccount AddNewAccount(long walletId, string password, DateTimeOffset accountCreationTime, int? accountIndex = null, string accountName = null);

        /// <summary>
        /// Create an account for a specific account index and account name pattern.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="newAccountIndex">The optional account index to use.</param>
        /// <param name="newAccountName">The optional account name to use.</param>
        /// <returns>
        /// A new HD account.
        /// </returns>
        HdAccount CreateAccount(long walletId, string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime, int newAccountIndex, string newAccountName = null);

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <returns>An unused account</returns>
        HdAccount GetFirstUnusedAccount(long walletId);

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <returns>The requested account or <c>null</c> if the account does not exist.</returns>
        HdAccount GetAccount(long walletId, string accountName);

        /// <summary>
        /// Gets the accounts in the wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>The accounts in the wallet.</returns>
        IEnumerable<HdAccount> GetAccounts(long walletId, Func<HdAccount, bool> accountFilter = null);
        #endregion

        #region HdAddress

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        bool ContainsAddress(long walletId, HdAddress address);

        /// <summary>
        /// Finds the HD addresses for the address.
        /// </summary>
        /// <remarks>
        /// Returns an HDAddress.
        /// </remarks>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="address">An address.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>HD Address</returns>
        HdAddress GetAddress(long walletId, string address, Func<HdAccount, bool> accountFilter = null);

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        IEnumerable<HdAddress> GetAllAddresses(long walletId, Func<HdAccount, bool> accountFilter = null);

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <returns>A list of all the public keys contained in the wallet.</returns>
        IEnumerable<Script> GetAllPubKeys(long walletId);

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        [NoTrace]
        ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address);
        #endregion

        #region Transactions

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A collection of spendable outputs.</returns>
        IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(long walletId, int currentChainHeight, int confirmations = 0, Func<HdAccount, bool> accountFilter = null);

        /// <summary>
        /// Gets all the transactions in the wallet.
        /// </summary>
        /// <param name="walletId">The wallet identifier.</param>
        /// <returns>A list of all the transactions in the wallet.</returns>
        IEnumerable<TransactionData> GetAllTransactions(long walletId);

        /// <summary>
        /// Calculates the fee paid by the user on a transaction sent.
        /// </summary>
        /// <param name="transactionId">The transaction id to look for.</param>
        /// <returns>The fee paid.</returns>
        Money GetSentTransactionFee(uint256 transactionId);
        #endregion
    }
}
