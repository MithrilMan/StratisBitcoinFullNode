using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Service interface that operates on wallets.
    /// </summary>
    /// <seealso cref="Stratis.Features.Wallet.IWalletUseCases" />
    public interface IWalletService //: IWalletUseCases
    {
        /// <summary>
        /// Creates a wallet.
        /// </summary>
        /// <param name="password">The password used to encrypt sensitive info.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <returns>A mnemonic defining the wallet's seed used to generate addresses and the generated wallet.</returns>
        (Mnemonic mnemonic, IWallet wallet) CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null);

        /// <summary>
        /// Recovers a wallet using mnemonic and password.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <returns>The recovered wallet.</returns>
        IWallet RecoverWallet(string password, string walletName, string mnemonic, DateTime creationTime, string passphrase = null);

        /// <summary>
        /// Recovers a wallet using extended public key and account index.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="extPubKey">The extended public key.</param>
        /// <param name="accountIndex">The account number.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns>The recovered wallet.</returns>
        IWallet RecoverWallet(string walletName, ExtPubKey extPubKey, int accountIndex, DateTime creationTime);

        /// <summary>
        /// Signs a string message.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="externalAddress">Address to use to sign.</param>
        /// <param name="message">Message to sign.</param>
        /// <returns>The generated signature.</returns>
        string SignMessage(string password, string walletName, string externalAddress, string message);

        /// <summary>
        /// Gets some general information about a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <returns>The required wallet.</returns>
        IWallet GetWallet(string walletName);

        /// <summary>
        /// Verifies the signed message.
        /// </summary>
        /// <param name="externalAddress">Address used to sign.</param>
        /// <param name="message">Message to verify.</param>
        /// <param name="signature">Message signature.</param>
        /// <returns>True if the signature is valid, false if it is invalid.</returns>
        bool VerifySignedMessage(string externalAddress, string message, string signature);

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        ISecret GetExtendedPrivateKeyForAddress(string password, string walletName, HdAddress address);

        /// <summary>
        /// Loads a specific wallet.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <returns>Returns <see langword="true"/> if the wallet has been found and loaded, <see langword="false"/> otherwise.</returns>
        bool LoadWallet(string password, string name);

        /// <summary>
        /// Loads all available wallets.
        /// </summary>
        /// <returns>Number of loaded wallets.</returns>
        int LoadWallets();

        /// <summary>
        /// Gets the extended private key of an account.
        /// </summary>
        /// <param name="accountReference">The account.</param>
        /// <param name="password">The password used to decrypt the encrypted seed.</param>
        /// <param name="cache">whether to cache the private key for future use.</param>
        /// <returns>The private key.</returns>
        ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false);

        /// <summary>
        /// Updates the wallet with the height of the last block synced.
        /// </summary>
        /// <param name="wallet">The wallet to update.</param>
        /// <param name="chainedHeader">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(IWallet wallet, ChainedHeader chainedHeader);


        /// <summary>
        /// Gets all wallet addresses.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>Both internal and external wallet addresses.</returns>
        IEnumerable<HdAddress> GetAllAddresses(string walletName);

        /// <summary>
        /// Gets all wallet transactions that are unspent or spent but with 0 confirmations.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>
        /// List of wallet related transactions.
        /// </returns>
        IEnumerable<TransactionData> GetAllUnspentTransactions(string walletName);

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>
        /// A list of all the public keys contained in the wallet.
        /// </returns>
        IEnumerable<Script> GetAllPubKeys(string walletName);

        /// <summary>
        /// Gets an account that contains no transactions.
        /// </summary>
        /// <param name="walletName">The name of the wallet from which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(string walletName, string password);

        /// <summary>
        /// Gets an account that contains no transactions.
        /// </summary>
        /// <param name="wallet">The wallet from which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(IWallet wallet, string password);

        /// <summary>
        /// Gets the extended public key of an account.
        /// </summary>
        /// <param name="accountReference">The account.</param>
        /// <returns>The extended public key.</returns>
        string GetExtPubKey(WalletAccountReference accountReference);

        /// <summary>
        /// Gets the index of a special account.
        /// Special accounts are accounts reserved for special purpose, like Cold Staking.
        /// </summary>
        /// <param name="purpose">The account purpose, used to generate the corresponding index.</param>
        /// <returns>Index of a special account</returns>
        int GetSpecialAccountIndex(string purpose);

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="confirmations">The confirmations.</param>
        /// <returns>
        /// A collection of spendable outputs
        /// </returns>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0);

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="confirmations">The confirmations.</param>
        /// <param name="accountFilter">The account filter.</param>
        /// <returns>
        /// A collection of spendable outputs
        /// </returns>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations, Func<HdAccount, bool> accountFilter);

        /// <summary>
        /// Gets the history of transactions contained in an account.
        /// If no account name is specified, history will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The account name.</param>
        /// <returns>Collection of address history and transaction pairs.</returns>
        IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null);

        /// <summary>
        /// Gets the balance of transactions contained in an account.
        /// If no account name is specified, balances will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The account name.</param>
        /// <returns>Collection of account balances.</returns>
        IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null);

        /// <summary>
        /// Gets the balance of transactions for this specific address.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="address">The address to get the balance from.</param>
        /// <returns>The address balance for an address.</returns>
        AddressBalance GetAddressBalance(string walletName, string address);

        /// <summary>
        /// TODO: this has to be renamed to GetUnusedExternalAddress once refactor is complete
        /// Gets an address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account</param>
        /// <returns>An unused address or a newly created address, in Base58 format.</returns>
        HdAddress GetUnusedAddress(WalletAccountReference accountReference);

        /// <summary>
        /// /// TODO: this has to be renamed to GetUnusedInternalAddress once refactor is complete
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <returns>An unused change address or a newly created change address, in Base58 format.</returns>
        HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference);

        /// <summary>
        /// Gets a collection of unused receiving or change addresses.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <param name="count">The number of addresses to retrieve.</param>
        /// <param name="isInternalAddress">A value indicating whether or not the addresses to get should be internal or external addresses.</param>
        /// <returns>A list of unused addresses. New addresses will be created as necessary.</returns>
        IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isInternalAddress = false);

        /// <summary>
        /// Gets a list of accounts.
        /// </summary>
        /// <param name="walletName">The name of the wallet to look into.</param>
        /// <returns>The list of wallet accounts.</returns>
        IEnumerable<HdAccount> GetAccounts(string walletName);

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
        /// Rewinds all the wallets at the specified height.
        /// </summary>
        /// <param name="height">The height.</param>
        void Rewind(int height);
    }
}
