using System.Collections.Generic;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// HD Account repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IHdAccountRepository : IRepositoryBase<long, HdAccount>
    {
        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns>
        /// The HD account specified by the parameter or <c>null</c> if the account does not exist.
        /// </returns>
        HdAccount GetByName(string walletName, string accountName);

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>An unused account</returns>
        HdAccount GetFirstUnusedAccount(string walletName);

        /// <summary>
        /// Gets the wallet accounts.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>The list of wallet accounts</returns>
        IEnumerable<HdAccount> GetWalletAccounts(string walletName);
    }
}
