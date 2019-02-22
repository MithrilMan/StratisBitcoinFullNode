using System;

namespace Stratis.Bitcoin.EventAggregator
{
    public interface IEventAggregator : IDisposable
    {
        IDisposable Subscribe<T>(Action<T> action) where T : IEvent;

        void Publish<T>(T @event) where T : IEvent;
    }
}
