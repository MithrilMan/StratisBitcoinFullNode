using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.EventAggregator.CoreEvents
{
    public class ConsensusRuleExceptionThrown : IEvent
    {
        public ConsensusRuleException ConsensusRuleException { get; }

        public ConsensusRuleExceptionThrown(ConsensusRuleException consensusRuleException)
        {
            this.ConsensusRuleException = consensusRuleException;
        }
    }
}
