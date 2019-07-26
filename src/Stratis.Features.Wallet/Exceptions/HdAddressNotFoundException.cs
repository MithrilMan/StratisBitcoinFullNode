namespace Stratis.Features.Wallet
{
    public class HdAddressNotFoundException : WalletException
    {
        public HdAddressNotFoundException(string message = "HdAddress not found in the wallet.") : base(message) { }
    }
}
