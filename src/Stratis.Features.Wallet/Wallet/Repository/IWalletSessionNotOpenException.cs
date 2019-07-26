namespace Stratis.Features.Wallet.Repository
{
    public class IWalletSessionNotOpenException : WalletException
    {
        public IWalletSessionNotOpenException() : base("No session available.") { }
    }
}
