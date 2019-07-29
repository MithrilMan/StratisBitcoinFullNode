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
        /// Creates a wallet and persist it as a file on the local system.
        /// </summary>
        /// <param name="password">The password used to encrypt sensitive info.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <returns>A mnemonic defining the wallet's seed used to generate addresses.</returns>
        Mnemonic CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null);

        /// <summary>
        /// Signs a string message.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="externalAddress">Address to use to sign.</param>
        /// <param name="message">Message to sign.</param>
        /// <returns>The generated signature.</returns>
        string SignMessage(string password, string walletName, string externalAddress, string message);

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
    }
}
