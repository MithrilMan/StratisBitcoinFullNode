using System;
using System.Security;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Keeps track of wallet locking status.
    /// </summary>
    public interface IWalletLockTracker
    {
        /// <summary>
        /// Locks the wallet.
        /// </summary>
        /// <param name="wallet">The wallet to lock.</param>
        void LockWallet(IWallet wallet);

        /// <summary>
        /// Unlocks a wallet for the specified time.
        /// </summary>
        /// <param name="wallet">The wallet to unlock.</param>
        /// <param name="password">The wallet password.</param>
        /// <param name="duration">Length of expiry of the unlocking.</param>
        void UnlockWallet(IWallet wallet, string password, TimeSpan duration);

        /// <summary>
        /// Gets the secret of the specified wallet or <see langword="null"/> if not found.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <returns>The wallet secret if found, or <see langword="null"/>.</returns>
        SecureString GetSecret(IWallet wallet);
    }
}
