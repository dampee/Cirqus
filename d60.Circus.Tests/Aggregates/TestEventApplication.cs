﻿using System;
using System.Collections.Generic;
using System.Linq;
using d60.Circus.Aggregates;
using d60.Circus.Events;
using d60.Circus.Numbers;
using d60.Circus.TestHelpers.Internals;
using d60.Circus.Tests.Stubs;
using NUnit.Framework;

namespace d60.Circus.Tests.Aggregates
{
    [TestFixture]
    public class TestEventApplication
    {
        [Test]
        public void AppliesEmittedEvents()
        {
            var someAggregate = new SomeAggregate
            {
                UnitOfWork = new ConsoleOutUnitOfWork(),
                SequenceNumberGenerator = new TestSequenceNumberGenerator()
            };
            someAggregate.Initialize(Guid.NewGuid());
            someAggregate.AggregateRootRepository = new DefaultAggregateRootRepository(new InMemoryEventStore());

            someAggregate.DoSomething();

            Assert.That(someAggregate.StuffThatWasDone.Count, Is.EqualTo(1));
            Assert.That(someAggregate.StuffThatWasDone.First(), Is.EqualTo("emitted an event"));
        }

        [Test]
        public void ProvidesSuitableMetadataOnEvents()
        {
            var now = new DateTime(1979, 3, 19, 19, 0, 0, DateTimeKind.Utc);
            TimeMachine.FixCurrentTimeTo(now);

            var aggregateRootId = Guid.NewGuid();
            var eventCollector = new InMemoryUnitOfWork();
            var sequenceNumberGenerator = new TestSequenceNumberGenerator(startWith: 78);

            var someAggregate = new SomeAggregate
            {
                UnitOfWork = eventCollector,
                SequenceNumberGenerator = sequenceNumberGenerator
            };
            someAggregate.Initialize(aggregateRootId);
            someAggregate.AggregateRootRepository = new DefaultAggregateRootRepository(new InMemoryEventStore());

            someAggregate.DoSomething();

            var someEvent = Enumerable.Cast<SomeEvent>(eventCollector).Single();

            Assert.That(someEvent.Meta[DomainEvent.MetadataKeys.TimeUtc], Is.EqualTo(now));
            Assert.That(someEvent.Meta[DomainEvent.MetadataKeys.TimeLocal], Is.EqualTo(now.ToLocalTime()));
            Assert.That(someEvent.Meta[DomainEvent.MetadataKeys.Owner], Is.EqualTo("SomeAggregate"));
            Assert.That(someEvent.Meta[DomainEvent.MetadataKeys.SequenceNumber], Is.EqualTo(78));
            Assert.That(someEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo(aggregateRootId));
        }


        public class SomeEvent : DomainEvent<SomeAggregate>
        {
            public readonly string What;

            public SomeEvent(string what)
            {
                What = what;
            }
        }

        public class SomeAggregate : AggregateRoot, IEmit<SomeEvent>
        {
            public readonly List<string> StuffThatWasDone = new List<string>();
            public void DoSomething()
            {
                Emit(new SomeEvent("emitted an event"));
            }

            public void Apply(SomeEvent e)
            {
                StuffThatWasDone.Add(e.What);
            }
        }

    }

}