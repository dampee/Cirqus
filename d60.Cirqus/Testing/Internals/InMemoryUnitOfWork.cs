﻿using System.Collections;
using System.Collections.Generic;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config;
using d60.Cirqus.Events;

namespace d60.Cirqus.Testing.Internals
{
    class InMemoryUnitOfWork : RealUnitOfWork, IEnumerable<DomainEvent>
    {
        public InMemoryUnitOfWork(IAggregateRootRepository aggregateRootRepository, IDomainTypeMapper domainTypeMapper)
            : base(aggregateRootRepository, domainTypeMapper)
        {
        }

        public void Clear()
        {
            Events.Clear();
            CachedAggregateRoots.Clear();
        }

        public IEnumerator<DomainEvent> GetEnumerator()
        {
            return Events.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}