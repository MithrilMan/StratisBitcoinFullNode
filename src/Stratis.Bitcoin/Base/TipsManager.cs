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
        /// returns the key to be used to store the tip information.
        /// This may be useful in case there are multiple component instance that have to store their own tip or in case a component may have multiple tips to store
        /// </summary>
        /// <returns></returns>
        string GetStorageKey();

        ChainedHeader FindFork(ChainedHeader chainTip);
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

        void StoreTip(ITipProvider tipProvider, uint256 tipHash);
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
            // If there aren't component that are providing tips, then return the chainTip
            if (this.tipProviders.Count() == 0)
            {
                return chainTip;
            }

            // get the tip of every known tipProvider
            List<HashHeightPair> componentsTips = new List<HashHeightPair>();

            // Get all component tips information
            // If a component doesn't have tip information, we rewind back to genesis
            foreach (ITipProvider component in this.tipProviders)
            {
                var componentTip = this.keyValueRepository.LoadValue<HashHeightPair>(this.ComputeStorageKey(component));
                if (componentTip == null)
                {
                    this.logger.LogTrace("A component doesn't have a stored tip, rewind to genesis");
                    return this.concurrentChain.Genesis;
                }

                componentsTips.Add(componentTip);
            }

            // Try to find a common tip.
            ChainedHeader commonTip = null;
            foreach (HashHeightPair tipHashHeight in componentsTips)
            {
                // note: the component needs to find its way to return a known ChainedHeader
                ChainedHeader componentTip = this.concurrentChain.GetBlock(tipHashHeight.Hash);

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

        public void StoreTip(ITipProvider tipProvider, uint256 tipHash)
        {
            ChainedHeader tipHeader = this.concurrentChain.GetBlock(tipHash);

            if (tipHeader == null)
            {
                this.logger.LogTrace("Cannot find an header with hash {0} passed by the component {1}", tipHash, tipProvider.GetType().Name);
                throw new ArgumentException(string.Format("Cannot find an header with hash {0}", tipHash));
            }

            var tip = new HashHeightPair(tipHeader);
            this.keyValueRepository.SaveValue(this.ComputeStorageKey(tipProvider), tip);
        }

        /// <summary>
        /// Compute the storage key to use to store the component tip.
        /// If GetStorageKey returns null, component type name is used instead.
        /// </summary>
        /// <param name="tipProvider">The tip provider.</param>
        /// <returns></returns>
        /// <remarks>
        /// All component tip keys are prefixed with <c>TIP_</c> string.
        /// </remarks>
        private string ComputeStorageKey(ITipProvider tipProvider)
        {
            return $"TIP_{tipProvider.GetStorageKey() ?? tipProvider.GetType().Name}";
        }
    }
}
