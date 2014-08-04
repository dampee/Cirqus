﻿using System;
using System.Collections.Generic;
using System.Linq;
using d60.EventSorcerer.Aggregates;
using d60.EventSorcerer.Config;
using d60.EventSorcerer.Events;
using d60.EventSorcerer.Extensions;
using d60.EventSorcerer.Numbers;
using d60.EventSorcerer.Views.Basic;

namespace d60.EventSorcerer.TestHelpers
{
    /// <summary>
    /// Use this bad boy to test your CQRS+ES things
    /// </summary>
    public class TestContext
    {
        readonly InMemoryEventCollector _eventCollector = new InMemoryEventCollector();
        readonly InMemoryEventStore _eventStore = new InMemoryEventStore();
        readonly BasicAggregateRootRepository _aggregateRootRepository;
        readonly TestEventDispatcher _eventDispatcher;
        DateTime _currentTime = DateTime.MinValue;
        bool _initialized;

        public TestContext()
        {
            _aggregateRootRepository = new BasicAggregateRootRepository(_eventStore);
            _eventDispatcher = new TestEventDispatcher(_aggregateRootRepository);
        }

        public TestContext AddViewManager(IViewManager viewManager)
        {
            _eventDispatcher.AddViewManager(viewManager);
            return this;
        }

        public void SetCurrentTime(DateTime fixedCurrentTime)
        {
            _currentTime = fixedCurrentTime;
        }

        public TAggregateRoot Get<TAggregateRoot>(Guid aggregateRootId) where TAggregateRoot : AggregateRoot, new()
        {
            var aggregateRoot = _aggregateRootRepository.Get<TAggregateRoot>(aggregateRootId);

            aggregateRoot.EventCollector = _eventCollector;
            aggregateRoot.SequenceNumberGenerator = new CachingSequenceNumberGenerator(_eventStore);

            return aggregateRoot;
        }

        public void Initialize()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Commits the current unit of work (i.e. emitted events from <see cref="UnitOfWork"/> are saved
        /// to the history and will be used to hydrate aggregate roots from now on.
        /// </summary>
        public void Commit()
        {
            EnsureInitialized();

            var domainEvents = UnitOfWork.ToList();

            if (!domainEvents.Any()) return;

            _eventStore.Save(Guid.NewGuid(), domainEvents);

            _eventCollector.Clear();

            _eventDispatcher.Dispatch(_eventStore, domainEvents);
        }

        void EnsureInitialized()
        {
            if (!_initialized)
            {
                _eventDispatcher.Initialize(_eventStore, purgeExistingViews: true);
                _initialized = true;
            }
        }

        /// <summary>
        /// Saves the given domain event to the history - requires that the aggregate root ID has been added
        /// </summary>
        public void Save<TAggregateRoot>(DomainEvent<TAggregateRoot> domainEvent) where TAggregateRoot : AggregateRoot
        {
            if (!domainEvent.Meta.ContainsKey(DomainEvent.MetadataKeys.AggregateRootId))
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Canno save domain event {0} because it does not have an aggregate root ID! Use the Save(id, event) overload or make sure that the '{1}' metadata key has been set",
                        domainEvent, DomainEvent.MetadataKeys.AggregateRootId));
            }

            Save(domainEvent.GetAggregateRootId(), domainEvent);
        }

        /// <summary>
        /// Saves the given domain event to the history as if it was emitted by the specified aggregate root
        /// </summary>
        public void Save<TAggregateRoot>(Guid aggregateRootId, DomainEvent<TAggregateRoot> domainEvent) where TAggregateRoot : AggregateRoot
        {
            var now = GetNow();

            domainEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId] = aggregateRootId;
            domainEvent.Meta[DomainEvent.MetadataKeys.SequenceNumber] = _eventStore.GetNextSeqNo(aggregateRootId);
            domainEvent.Meta[DomainEvent.MetadataKeys.Owner] = AggregateRoot.GetOwnerFromType(typeof(TAggregateRoot));
            domainEvent.Meta[DomainEvent.MetadataKeys.Version] = domainEvent.GetType().GetFromAttributeOrDefault<VersionAttribute, int>(a => a.Number, 1);
            domainEvent.Meta[DomainEvent.MetadataKeys.TimeLocal] = now.ToLocalTime();
            domainEvent.Meta[DomainEvent.MetadataKeys.TimeUtc] = now;

            _eventStore.Save(Guid.NewGuid(), new[] { domainEvent });
        }

        DateTime GetNow()
        {
            if (_currentTime == DateTime.MinValue)
            {
                return DateTime.UtcNow;
            }

            var timeToReturn = _currentTime.ToUniversalTime();
            
            // simulate time progressing
            _currentTime = _currentTime.AddTicks(1);

            return timeToReturn;
        }

        /// <summary>
        /// Gets the events collected in the current unit of work
        /// </summary>
        public IEnumerable<DomainEvent> UnitOfWork
        {
            get { return _eventCollector.ToList(); }
        }

        /// <summary>
        /// Gets the entire history of commited events from the event store
        /// </summary>
        public IEnumerable<DomainEvent> History
        {
            get { return _eventStore.Stream().ToList(); }
        }
    }
}