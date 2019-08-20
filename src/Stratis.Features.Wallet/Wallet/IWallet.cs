using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// A wallet.
    /// </summary>
    public interface IWallet
    {
        /// <summary>
        /// The name of this wallet.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Flag indicating if it is a watch only wallet.
        /// </summary>
        bool IsExtPubKeyWallet { get; set; }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        byte[] ChainCode { get; set; }

        /// <summary>
        /// Gets or sets the Merkle path.
        /// </summary>
        ICollection<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet is for.
        /// </summary>
        Network Network { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The root of the accounts tree.
        /// </summary>
        ICollection<AccountRoot> AccountsRoot { get; set; }

        /// <summary>
        /// Gets the accounts in the wallet.
        /// </summary>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>The accounts in the wallet.</returns>
        IEnumerable<HdAccount> GetAccounts(Func<HdAccount, bool> accountFilter = null);

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <returns>The requested account or <c>null</c> if the account does not exist.</returns>
        HdAccount GetAccount(string accountName);

        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        void SetLastBlockDetails(ChainedHeader block);

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <returns>A list of all the public keys contained in the wallet.</returns>
        IEnumerable<Script> GetAllPubKeys();

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="accountIndex">The index at which an account will be created. If left null, a new account will be created after the last used one.</param>
        /// <param name="accountName">The name of the account to be created. If left null, an account will be created according to the <see cref="Wallet.AccountNamePattern"/>.</param>
        /// <returns>A new HD account.</returns>
        HdAccount AddNewAccount(string password, DateTimeOffset accountCreationTime, int? accountIndex = null, string accountName = null);

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="extPubKey">The extended public key for the wallet<see cref="EncryptedSeed"/>.</param>
        /// <param name="accountIndex">Zero-based index of the account to add.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        HdAccount AddNewAccount(ExtPubKey extPubKey, int accountIndex, DateTimeOffset accountCreationTime);

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        HdAccount GetFirstUnusedAccount();

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        bool ContainsAddress(HdAddress address);

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address);

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A collection of spendable outputs.</returns>
        IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(int currentChainHeight, int confirmations = 0, Func<HdAccount, bool> accountFilter = null);

        /// <summary>
        /// Calculates the fee paid by the user on a transaction sent.
        /// </summary>
        /// <param name="transactionId">The transaction id to look for.</param>
        /// <returns>The fee paid.</returns>
        Money GetSentTransactionFee(uint256 transactionId);

        /// <summary>
        /// Finds the HD addresses for the address.
        /// </summary>
        /// <remarks>
        /// Returns an HDAddress.
        /// </remarks>
        /// <param name="address">An address.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>HD Address</returns>
        HdAddress GetAddress(string address, Func<HdAccount, bool> accountFilter = null);
    }
}
