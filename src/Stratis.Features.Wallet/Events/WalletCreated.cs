using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// The wallet that has been created.
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
