namespace Stratis.Features.Wallet
{
    public class WalletDuplicateEncryptedSeedException : WalletException
    {
        public WalletDuplicateEncryptedSeedException(string message = "Wallet already exists.") : base(message) { }
    }
}
