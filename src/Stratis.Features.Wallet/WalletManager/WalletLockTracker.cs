using System;
using System.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Allow to lock and unlock wallets and keep track of their statuses.
    /// It's designed to be singleton.
    /// </summary>
    public class WalletLockTracker : IWalletLockTracker
    {
        /// <summary>The private key cache for unlocked wallets.</summary>
        private readonly MemoryCache privateKeyCache;

        /// <summary>Logger instance.</summary>
        private readonly ILogger logger;

        public WalletLockTracker(LoggerFactory loggerFactory)
        {
            this.logger = Guard.NotNull(loggerFactory, nameof(loggerFactory)).CreateLogger(this.GetType().FullName);

            this.privateKeyCache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromMinutes(1) });
        }

        /// <inheritdoc />
        public void LockWallet(IWallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            string cacheKey = wallet.EncryptedSeed;
            this.privateKeyCache.Remove(cacheKey);
        }

        /// <inheritdoc />
        public void UnlockWallet(IWallet wallet, string password, TimeSpan duration)
        {
            Guard.NotNull(wallet, nameof(wallet));

            this.CacheSecret(wallet, password, duration);
        }

        /// <inheritdoc />
        public SecureString GetSecret(IWallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            string cacheKey = wallet.EncryptedSeed;
            if(!this.privateKeyCache.TryGetValue(cacheKey, out SecureString secretValue))
            {
                this.logger.LogTrace("(-)[Wallet secret not found, wallet is locked.]");
                return null;
            }

            return secretValue;
        }

        [NoTrace]
        private SecureString CacheSecret(IWallet wallet, string walletPassword, TimeSpan duration)
        {
            string cacheKey = wallet.EncryptedSeed;

            if (!this.privateKeyCache.TryGetValue(cacheKey, out SecureString secretValue))
            {
                Key privateKey = Key.Parse(wallet.EncryptedSeed, walletPassword, wallet.Network);
                secretValue = privateKey.ToString(wallet.Network).ToSecureString();
            }

            this.privateKeyCache.Set(cacheKey, secretValue, duration);

            return secretValue;
        }
    }
}
