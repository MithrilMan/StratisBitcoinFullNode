using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// the wallet that has been created
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class WalletCreated : EventBase
    {
        public IWallet Wallet { get; }

        public WalletCreated(IWallet wallet)
        {
            this.Wallet = wallet;
        }
    }
}
