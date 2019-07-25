namespace Stratis.Features.Wallet
{
    public class WalletNotFoundException : WalletException
    {
        public WalletNotFoundException(string message = "Wallet not found") : base(message)
        {
        }
    }
}
