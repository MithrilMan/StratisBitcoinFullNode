using System;

namespace Stratis.Features.Wallet.Repository
{
    public interface IUnitOfWorkSession : IDisposable
    {
        void Commit();
        void RollBack();
    }
}
