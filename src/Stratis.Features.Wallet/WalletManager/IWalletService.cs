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
        /// Signs a string message.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="externalAddress">Address to use to sign.</param>
        /// <param name="message">Message to sign.</param>
        /// <returns>The generated signature.</returns>
        string SignMessage(string password, string walletName, string externalAddress, string message);

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        ISecret GetExtendedPrivateKeyForAddress(string password, string walletName, HdAddress address);
    }
}
