using System.Collections.Generic;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// New addresses have been created.
    /// </summary>
    /// /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class WalletAddressesCreated : EventBase
    {
        public IWallet wallet { get; }
        public HdAccount account { get; }
        public IEnumerable<HdAddress> NewAddresses { get; }

        public WalletAddressesCreated(IWallet wallet, HdAccount account, IEnumerable<HdAddress> newAddresses)
        {
            this.wallet = wallet;
            this.account = account;
            this.NewAddresses = newAddresses;
        }
    }
}