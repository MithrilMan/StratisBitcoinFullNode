using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Wallet.Broadcasting;
using Stratis.Features.Wallet.Events;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class JsonWalletManager : WalletManager
    {
        /// <summary>File extension for wallet files.</summary>
        private const string WalletFileExtension = "wallet.json";

        /// <summary>Timer for saving wallet files to the file system.</summary>
        private const int WalletSavetimeIntervalInMinutes = 5;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        public JsonWalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ChainIndexer chainIndexer,
            WalletSettings walletSettings,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IScriptAddressReader scriptAddressReader,
            ISignals signals,
            IWalletService walletService,
            IHdAddressLookup hdAddressLookup,
            IBroadcasterManager broadcasterManager = null)
            : base(loggerFactory, network, chainIndexer, walletSettings, dataFolder, asyncProvider, nodeLifetime, dateTimeProvider, scriptAddressReader, signals, walletService, hdAddressLookup, broadcasterManager)
        {
        }

        public override void Start()
        {
            base.Start();

            // Save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop("Wallet persist job", token =>
            {
                this.SaveWallets();
                this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
            startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));
        }

        /// <inheritdoc />
        public override void Stop()
        {
            base.Stop();
            this.asyncLoop?.Dispose();

            this.SaveWallets();
        }

        public void SaveWallet(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            lock (this.lockObject)
            {
                this.fileStorage.SaveToFile(wallet, $"{wallet.Name}.{WalletFileExtension}", new FileStorageOption { SerializeNullValues = false });
            }
        }

        /// <inheritdoc />
        public void SaveWallets()
        {
            foreach (Wallet wallet in this.loadedWallets)
            {
                this.SaveWallet(wallet);
            }
        }

        /// <inheritdoc />
        public string GetWalletFileExtension()
        {
            return WalletFileExtension;
        }

        protected override void OnWalletCreated(WalletCreated @event)
        {
            base.OnWalletCreated(@event);

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(@event.wallet);
        }

        protected override void OnWalletAccountCreated(WalletAccountCreated @event)
        {
            base.OnWalletAccountCreated(@event);

            this.SaveWallet(@event.wallet);
        }
    }
}
