using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// The wallet that has been loaded.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class WalletLoaded: EventBase
    {
        public IWallet Wallet { get; }

        public WalletLoaded(IWallet wallet)
        {
            this.Wallet = wallet;
        }
    }
}
