namespace Stratis.Features.Wallet.Repository
{
    public class UnitOfWorkSessionNotOpenException : WalletException
    {
        public UnitOfWorkSessionNotOpenException() : base("No open available.") { }
    }
}
