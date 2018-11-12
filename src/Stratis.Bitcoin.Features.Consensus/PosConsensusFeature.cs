﻿using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosConsensusFeature : ConsensusFeature
    {
        private readonly Network network;
        private readonly IChainState chainState;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly NodeDeployments nodeDeployments;
        private readonly ConcurrentChain chain;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;
        private readonly ICheckpoints checkpoints;

        public PosConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments,
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            Signals.Signals signals,
            ILoggerFactory loggerFactory,
            ICheckpoints checkpoints) : base(network, chainState, connectionManager, signals, consensusManager, nodeDeployments)
        {
            this.network = network;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.checkpoints = checkpoints;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }
}