using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>Interface that every component that persist a tip should implement. This is used to initialize all components to a common tip at startup.</summary>
    public interface ITipProvider
    {
        /// <summary>
        /// Returns the tip known by the component. This is part of the initialization process.
        /// Allows a <c>null</c> return value in case the component doesn't know its initial tip (genesis tip is used instead).
        /// </summary>
        /// <returns>The initial component tip, or <see langword="null"/> if unknown. When the return value is <see langword="null"/> genesis tip is used instead.</returns>
        ChainedHeader GetTip(ChainedHeader chainTip);

        /// <summary>
        /// returns the key to be used to store the tip information.
        /// This may be useful in case there are multiple component instance that have to store their own tip or in case a component may have multiple tips to store
        /// </summary>
        /// <returns></returns>
        string GetStorageKey();
    }

    /// <summary>Component that keeps track of common tip between components that can have a tip, during initialization.</summary>
    public interface ITipsManager
    {
        /// <summary>
        /// Gets the common tip of known components that implement <see cref="ITipProvider"/>.
        /// </summary>
        /// <value>
        /// The common tip.
        /// </value>
        ChainedHeader CommonTip { get; }

        /// <summary>Initializes <see cref="ITipsManager"/>.</summary>
        /// <param name="highestHeader">Tip of chain of headers.</param>
        void Initialize(ChainedHeader highestHeader);

    }

    public class TipsManager : ITipsManager
    {
        private readonly IKeyValueRepository keyValueRepository;

        /// <summary>
        /// List of all the registered components that provide their tips.
        /// </summary>
        private readonly IEnumerable<ITipProvider> tipProviders;

        private readonly ConcurrentChain concurrentChain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private bool initialized;
        private ChainedHeader _commonTip;

        /// <inheritdoc />
        public ChainedHeader CommonTip
        {
            get
            {
                if (this.initialized)
                {
                    return this._commonTip;
                }
                else
                {
                    throw new Exception($"{nameof(this.CommonTip)} can't be accessed before {nameof(TipsManager)} initialization.");
                }
            }

            private set
            {
                this._commonTip = value;
            }
        }

        public TipsManager(IKeyValueRepository keyValueRepository, IEnumerable<ITipProvider> tipProviders, ConcurrentChain concurrentChain, ILoggerFactory loggerFactory)
        {
            this.keyValueRepository = Guard.NotNull(keyValueRepository, nameof(keyValueRepository));
            this.tipProviders = Guard.NotNull(tipProviders, nameof(tipProviders));
            this.concurrentChain = Guard.NotNull(concurrentChain, nameof(concurrentChain));
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.initialized = false;
        }

        /// <inheritdoc />
        public void Initialize(ChainedHeader chainTip)
        {
            this.CommonTip = this.FindCommonTip(chainTip);

            this.initialized = true;

            this.logger.LogDebug("Tips manager initialized at '{0}'.", this.CommonTip);
        }

        private ChainedHeader FindCommonTip(ChainedHeader chainTip)
        {
            List<ChainedHeader> componentTips = new List<ChainedHeader>();

            if (this.tipProviders.Count() == 0)
            {
                return this.concurrentChain.Genesis;
            }

            ChainedHeader commonTip = null;
            foreach (ITipProvider component in this.tipProviders)
            {
                // note: the component needs to find its way to return a known ChainedHeader
                ChainedHeader componentTip = component.GetTip(chainTip);

                if (componentTip == null)
                {
                    this.logger.LogError("Tip of component {0} not set", component.GetType().Name);
                    throw new ArgumentNullException(nameof(componentTip));
                }

                if (commonTip == null)
                {
                    // the first iteration we don't have anything to compare to
                    commonTip = componentTip;
                }
                else
                {
                    commonTip = componentTip.FindFork(commonTip);
                }
            }

            return commonTip;
        }

        public void StoreTip(string key, int height, uint256 hash)
        {
            throw new NotImplementedException();
        }
    }
}
