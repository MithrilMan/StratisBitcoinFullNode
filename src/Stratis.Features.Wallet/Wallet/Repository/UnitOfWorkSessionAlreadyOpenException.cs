namespace Stratis.Features.Wallet.Repository
{
    public class UnitOfWorkSessionAlreadyOpenException : WalletException
    {
        public UnitOfWorkSessionAlreadyOpenException() : base("Cannot open a new session while an active session is already open.") { }
    }
}
