using System;
using System.Threading.Tasks;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// Generic unit of work interface, to support transaction operations on different repositories
    /// </summary>
    public interface IWalletUnitOfWork : IDisposable
    {
        IWalletRepository WalletRepository { get; }

        IHdAccountRepository HdAccountRepository { get; }

        IHdAddressRepository HdAddressRepository { get; }

        ITransactionDataRepository TransactionDataRepository { get; }

        void Save();
    }
}
