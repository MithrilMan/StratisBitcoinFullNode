using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Stratis.Bitcoin.EventAggregator
{
    public class EventAggregator : IEventAggregator
    {
        /// <summary>
        /// Proof of concept instance.
        /// this instance is here just to not having to change lot of files to inject event aggregator into services.
        /// TODO: remove this property and inject EventAggregator in every service that need it.
        /// </summary>
        public static EventAggregator PoC { get; } = new EventAggregator();

        private readonly Subject<IEvent> subject = new Subject<IEvent>();

        public IDisposable Subscribe<T>(Action<T> action) where T : IEvent
        {
            return this.subject.OfType<T>()
                .AsObservable()
                .Subscribe(action);
        }

        public void Publish<T>(T @event) where T : IEvent
        {
            this.subject.OnNext(@event);
        }

        public void Dispose()
        {
            this.subject.Dispose();
        }

    }
}
