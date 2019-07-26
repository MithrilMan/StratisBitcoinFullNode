namespace Stratis.Features.Wallet.Repository
{
    public class IWalletSessionAlreadyOpen : WalletException
    {
        public IWalletSessionAlreadyOpen() : base("Cannot open a new session while an active session is still open.") { }
    }
}
