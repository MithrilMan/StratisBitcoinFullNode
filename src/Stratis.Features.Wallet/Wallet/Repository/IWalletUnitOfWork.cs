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
        /// Begins a session, that must be followed either by a <see cref="Commit" /> or a <see cref="Rollback" />.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> session. If no explicit Save is called before it being disposed, a rollback will be performed.</returns>
        /// <exception cref="UnitOfWorkSessionAlreadyOpenException">Thrown if a session is already open.</exception>
        IUnitOfWorkSession Begin();

        ///// <summary>
        ///// Commits current session.
        ///// </summary>
        ///// <exception cref="UnitOfWorkSessionNotOpenException">Thrown if no session is open.</exception>
        //void Commit();

        ///// <summary>
        ///// Rollbacks current session.
        ///// </summary>
        ///// <exception cref="UnitOfWorkSessionNotOpenException">Thrown if no session is open.</exception>
        //void Rollback();

        // TODO: investigating best approach. Currently trying an IDisposable pattern to handle sessions
    }
}
