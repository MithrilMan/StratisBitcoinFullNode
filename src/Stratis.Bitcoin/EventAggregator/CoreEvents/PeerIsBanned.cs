using System.Net;

namespace Stratis.Bitcoin.EventAggregator.CoreEvents
{
    public class PeerIsBanned : IEvent
    {
        public IPEndPoint Endpoint { get; }

        public int BanTimeSeconds { get; }

        public string Reason { get; }

        public PeerIsBanned(IPEndPoint endpoint, int banTimeSeconds, string reason)
        {
            this.Endpoint = endpoint;
            this.BanTimeSeconds = banTimeSeconds;
            this.Reason = reason;
        }
    }
}
