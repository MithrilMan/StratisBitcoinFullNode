namespace Stratis.Features.Wallet
{
    public class WalletDuplicateNameException : WalletException
    {
        public WalletDuplicateNameException(string message = "Wallet already exists.") : base(message) { }
    }
}
