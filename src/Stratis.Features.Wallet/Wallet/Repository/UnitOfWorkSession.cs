namespace Stratis.Features.Wallet.Repository
{
    public class UnitOfWorkSession : IUnitOfWorkSession
    {
        private readonly IWalletUnitOfWork walletUnitOfWork;

        public UnitOfWorkSession(/*dbcontext, transaction or something like that to be injected, depending on RDBMS implementation*/)
        {
            this.walletUnitOfWork = walletUnitOfWork ?? throw new System.ArgumentNullException(nameof(walletUnitOfWork));
        }

        public void Commit()
        {
            this.walletUnitOfWork.Save();
        }

        public void RollBack()
        {
            this.walletUnitOfWork.Rollback();
        }

        public void Dispose()
        {
        }
    }
}
