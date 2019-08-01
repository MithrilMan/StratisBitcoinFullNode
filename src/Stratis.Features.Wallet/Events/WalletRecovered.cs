using System;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// The wallet that has been recovered.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class WalletRecovered : EventBase
    {
        public IWallet Wallet { get; }

        public DateTime CreationTime { get; }

        public WalletRecovered(IWallet wallet, DateTime creationTime)
        {
            this.Wallet = wallet;
            this.CreationTime = creationTime;
        }
    }
}
