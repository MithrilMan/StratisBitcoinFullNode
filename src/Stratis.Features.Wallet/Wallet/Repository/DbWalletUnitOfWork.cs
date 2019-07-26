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
        private readonly ILoggerFactory loggerFactory;

        public IWalletRepository WalletRepository { get; }

        public IHdAccountRepository HdAccountRepository { get; }

        public IHdAddressRepository HdAddressRepository { get; }

        public ITransactionDataRepository TransactionDataRepository { get; }

        public DbWalletUnitOfWork(ILoggerFactory loggerFactory, IWalletRepository walletRepository, IHdAccountRepository hdAccountRepository, IHdAddressRepository hdAddressRepository, ITransactionDataRepository transactionDataRepository)
        {
            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.WalletRepository = Guard.NotNull(walletRepository, nameof(walletRepository));
            this.HdAccountRepository = Guard.NotNull(hdAccountRepository, nameof(hdAccountRepository));
            this.HdAddressRepository = Guard.NotNull(hdAddressRepository, nameof(hdAddressRepository));
            this.TransactionDataRepository = Guard.NotNull(transactionDataRepository, nameof(transactionDataRepository));
        }

        public void Begin()
        {

        }

        public void Save()
        {

        }
    }
}
