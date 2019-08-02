using System;
using System.Collections.Generic;
using System.Security;
using NBitcoin;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Allow to keep tracks of wallets unspent outputs.
    /// </summary>
    public interface IOutPointLookup
    {
        /// <summary>
        /// Tries the get transaction data for a specific previous output. Returns null if not found.
        /// </summary>
        /// <param name="outPoint">The previous out.</param>
        /// <returns><see cref="TransactionData"/> information relative to the <paramref name="outPoint"/>, or <see langword="null"/>.</returns>
        TransactionData Get(OutPoint outPoint);

        /// <summary>
        /// Adds the transaction data relative to the specified OutPoint.
        /// </summary>
        /// <param name="outPoint">The outPoint.</param>
        /// <param name="transaction">The transaction data to add.</param>
        void Add(OutPoint outPoint, TransactionData transaction);

        /// <summary>
        /// Removes the specified OutPoint reference transaction data.
        /// </summary>
        /// <param name="outPoint">The outPoint.</param>
        void Remove(OutPoint outPoint);

        /// <summary>
        /// Adds the specified transaction data using its data as OutPoint.
        /// </summary>
        /// <param name="transaction">The transaction data to add.</param>
        void Add(TransactionData transaction);

        /// <summary>
        /// Removes the specified transaction data using its data as OutPoint.
        /// </summary>
        /// <param name="transaction">The transaction data to remove.</param>
        void Remove(TransactionData transaction);

        /// <summary>
        /// Clears this instance.
        /// </summary>
        void Clear();
    }
}
