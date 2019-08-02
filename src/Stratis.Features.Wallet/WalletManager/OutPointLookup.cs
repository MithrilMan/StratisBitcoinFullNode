using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Allow to keep tracks of wallets unspent outputs.
    /// </summary>
    public class OutPointLookup : IOutPointLookup
    {
        private Dictionary<OutPoint, TransactionData> outpointLookup;

        public OutPointLookup()
        {
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();
        }

        /// <inheritdoc />
        public void Add(OutPoint outPoint, TransactionData transaction)
        {
            this.outpointLookup[outPoint] = transaction;
        }

        /// <inheritdoc />
        public void Add(TransactionData transaction)
        {
            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
        }

        /// <inheritdoc />
        public void Remove(OutPoint outPoint)
        {
            this.outpointLookup.Remove(outPoint);
        }

        /// <inheritdoc />
        public void Remove(TransactionData transaction)
        {
            this.outpointLookup.Remove(new OutPoint(transaction.Id, transaction.Index));
        }

        /// <inheritdoc />
        public TransactionData Get(OutPoint previousOutput)
        {
            if (this.outpointLookup.TryGetValue(previousOutput, out TransactionData transactionData))
            {
                return transactionData;
            }
            else
            {
                return null;
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();
        }
    }
}
