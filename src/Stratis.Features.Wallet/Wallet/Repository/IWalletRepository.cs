using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// Wallet repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IWalletRepository : IRepositoryBase<long, IWallet>
    {
        /// <summary>
        /// Gets the wallet by name.
        /// </summary>
        /// <param name="walletName">Name of the wallet to fetch.</param>
        /// <returns>
        /// The found wallet, or <see langword="null" />.
        /// </returns>
        IWallet GetByName(string walletName);

        /// <summary>
        /// Gets the wallet by encryptedSeed.
        /// </summary>
        /// <param name="encryptedSeed">The encrypted seed.</param>
        /// <returns>
        /// The found wallet, or <see langword="null" />.
        /// </returns>
        IWallet GetByEncryptedSeed(string encryptedSeed);

        /// <summary>
        /// Sets the wallet tip (Hash and Height).
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="tip">The tip.</param>
        /// <returns></returns>
        IWallet SetWalletTip(string walletName, ChainedHeader tip);
    }
}
