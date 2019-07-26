using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet.Repository
{
    /// <summary>
    /// HD Address repository interface to fetch and store data based on use cases.
    /// </summary>
    public interface IHdAddressRepository : IRepositoryBase<long, HdAddress>
    {
        /// <summary>
        /// Finds the HD address for the address.
        /// </summary>
        /// <remarks>
        /// Returns an HDAddress.
        /// </remarks>
        /// <param name="address">An address.</param>
        /// <returns>HD Address</returns>
        HdAddress GetAddress(string address);

        IEnumerable<Script> GetAllPubKeys(long hdAccountId);

        void Add(IEnumerable<HdAddress> newReceivingAddresses);
    }
}
