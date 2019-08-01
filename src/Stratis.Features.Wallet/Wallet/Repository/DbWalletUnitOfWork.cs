using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// Generic unit of work interface, to support transaction operations on different repositories
    /// </summary>
    public class DbWalletUnitOfWork : IWalletUnitOfWork
    {
        private ILogger logger;

        public IWalletRepository WalletRepository { get; }

        public IHdAccountRepository HdAccountRepository { get; }

        public IHdAddressRepository HdAddressRepository { get; }

        public ITransactionDataRepository TransactionDataRepository { get; }

        private IUnitOfWorkSession currentSession;

        public DbWalletUnitOfWork(ILoggerFactory loggerFactory, IWalletRepository walletRepository, IHdAccountRepository hdAccountRepository, IHdAddressRepository hdAddressRepository, ITransactionDataRepository transactionDataRepository)
        {
            this.logger = Guard.NotNull(loggerFactory, nameof(loggerFactory)).CreateLogger(this.GetType().Name);

            this.WalletRepository = Guard.NotNull(walletRepository, nameof(walletRepository));
            this.HdAccountRepository = Guard.NotNull(hdAccountRepository, nameof(hdAccountRepository));
            this.HdAddressRepository = Guard.NotNull(hdAddressRepository, nameof(hdAddressRepository));
            this.TransactionDataRepository = Guard.NotNull(transactionDataRepository, nameof(transactionDataRepository));
        }


        /// <inheritdoc />
        public IUnitOfWorkSession Begin()
        {
            if (this.currentSession != null)
            {
                throw new UnitOfWorkSessionAlreadyOpenException();
            }

            this.currentSession = new UnitOfWorkSession(/*a dbcontext/transaction/whatever*/);
            return this.currentSession;
        }

        ///// <inheritdoc />
        //public void Commit()
        //{
        //    if (this.currentSession != null)
        //    {
        //        throw new UnitOfWorkSessionNotOpenException();
        //    }

        //    this.currentSession.Commit();
        //    this.currentSession = null;
        //}

        ///// <inheritdoc />
        //public void Rollback()
        //{
        //    if (this.currentSession != null)
        //    {
        //        throw new UnitOfWorkSessionNotOpenException();
        //    }

        //    this.currentSession.RollBack();
        //    this.currentSession = null;
        //}

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.currentSession != null)
            {
                this.currentSession.Dispose();
                this.currentSession = null;
            }
        }
    }
}
