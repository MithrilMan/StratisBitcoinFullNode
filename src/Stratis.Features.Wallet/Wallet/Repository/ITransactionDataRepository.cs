using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// TransactionData repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface ITransactionDataRepository : IRepositoryBase<long, TransactionData>
    {
        IEnumerable<Script> GetAllPubKeys(long hdAccountId);
    }
}
