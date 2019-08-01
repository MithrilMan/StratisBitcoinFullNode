using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.Wallet.Events
{
    /// <summary>
    /// A new account has been created.
    /// This event isn't published if an account has been created during wallet creation/recover.
    /// </summary>
    /// /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class WalletAccountCreated : EventBase
    {
        public IWallet wallet { get; }
        public HdAccount account { get; }

        public WalletAccountCreated(IWallet wallet, HdAccount account)
        {
            this.wallet = wallet;
            this.account = account;
        }
    }
}