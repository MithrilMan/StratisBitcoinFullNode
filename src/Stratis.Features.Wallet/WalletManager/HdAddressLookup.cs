using System.Collections.Generic;
using System.Linq;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Keeps track of wallet addresses in order to allow faster look-ups of transactions affecting the wallets' addresses.
    /// </summary>
    /// <seealso cref="Stratis.Features.Wallet.IHdAddressLookup" />
    public class HdAddressLookup : IHdAddressLookup
    {
        private object lockObject;

        /// <summary>
        /// The list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        /// </summary>
        internal ScriptToAddressLookup scriptToAddressLookup;

        public HdAddressLookup()
        {
            this.lockObject = new object();
            this.scriptToAddressLookup = this.CreateAddressFromScriptLookup();
        }

        /// <summary>
        /// Adds the addresses to keep track of.
        /// </summary>
        /// <param name="addresses">The addresses to track.</param>
        public void TrackAddresses(IEnumerable<HdAddress> addresses)
        {
            if (addresses == null)
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (HdAddress address in addresses)
                {
                    this.scriptToAddressLookup[address.ScriptPubKey] = address;
                    if (address.Pubkey != null)
                        this.scriptToAddressLookup[address.Pubkey] = address;
                }
            }
        }

        /// <summary>
        /// Creates the <see cref="ScriptToAddressLookup"/> object to use.
        /// </summary>
        /// <remarks>
        /// Override this method and the <see cref="ScriptToAddressLookup"/> object to provide a custom keys lookup.
        /// </remarks>
        /// <returns>A new <see cref="ScriptToAddressLookup"/> object for use by this class.</returns>
        protected virtual ScriptToAddressLookup CreateAddressFromScriptLookup()
        {
            return new ScriptToAddressLookup();
        }
    }
}
