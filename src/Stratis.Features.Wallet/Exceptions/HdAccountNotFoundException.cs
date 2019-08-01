namespace Stratis.Features.Wallet
{
    public class HdAccountNotFoundException : WalletException
    {
        public HdAccountNotFoundException(string message = "HdAccount not found in the wallet.") : base(message) { }
    }
}
