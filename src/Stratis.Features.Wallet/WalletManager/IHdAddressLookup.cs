using System;
using System.Collections.Generic;
using System.Security;
using NBitcoin;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Allow to keep tracks of wallets addresses.
    /// </summary>
    public interface IHdAddressLookup
    {
        /// <summary>
        /// Adds the addresses to keep track of.
        /// </summary>
        /// <param name="addresses">The addresses to track.</param>
        void TrackAddresses(IEnumerable<HdAddress> addresses);
    }
}
