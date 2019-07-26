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

        /// <summary>
        /// Begins a session, that must be followed either by a <see cref="Commit"/> or a <see cref="Rollback"/>.
        /// </summary>
        /// <exception cref="IWalletSessionAlreadyOpen">Thrown if a session is already open.</exception>
        void Begin();

        /// <summary>
        /// Commits current session.
        /// </summary>
        /// <exception cref="IWalletSessionNotOpenException">Thrown if no session is open.</exception>
        void Save();

        /// <summary>
        /// Rollbacks current session.
        /// </summary>
        /// <exception cref="IWalletSessionNotOpenException">Thrown if no session is open.</exception>
        void Rollback();
    }
}
