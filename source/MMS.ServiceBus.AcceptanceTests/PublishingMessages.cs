﻿//-------------------------------------------------------------------------------
// <copyright file="PublishingMessages.cs" company="Multimedia Solutions AG">
//   Copyright (c) MMS AG 2011-2015
// </copyright>
//-------------------------------------------------------------------------------

namespace MMS.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using FluentAssertions;
    using NUnit.Framework;
    using ServiceBus.Pipeline;
    using ServiceBus.Testing;

    [TestFixture]
    public class PublishingMessages
    {
        private Context context;

        private HandlerRegistrySimulator registry;

        private Broker broker;

        private MessageUnit publisher;

        private MessageUnit subscriber;

        [SetUp]
        public void SetUp()
        {
            this.context = new Context();
            this.registry = new HandlerRegistrySimulator(this.context);

            this.broker = new Broker();
            Topic destination = Topic.Create("Subscriber");

            this.publisher = new MessageUnit(new EndpointConfiguration().Endpoint("Publisher").Concurrency(1))
                .Use(new AlwaysRouteToDestination(destination));
            this.subscriber = new MessageUnit(new EndpointConfiguration().Endpoint("Subscriber")
                .Concurrency(1)).Use(this.registry);

            this.broker.Register(this.publisher)
                       .Register(this.subscriber, destination);

            this.broker.Start();
        }

        [TearDown]
        public void TearDown()
        {
            this.broker.Stop();
        }

        [Test]
        public async Task WhenOneMessagePublished_InvokesSynchronousAndAsynchronousHandlers()
        {
            await this.publisher.Publish(new Event { Bar = 42 });

            this.context.AsyncHandlerCalled.Should().BeInvokedOnce();
            this.context.HandlerCalled.Should().BeInvokedOnce();
        }

        [Test]
        public async Task WhenMultipeMessagePublished_InvokesSynchronousAndAsynchronousHandlers()
        {
            await this.publisher.Publish(new Event { Bar = 42 });
            await this.publisher.Publish(new Event { Bar = 43 });

            this.context.AsyncHandlerCalled.Should().BeInvokedTwice();
            this.context.HandlerCalled.Should().BeInvokedTwice();
        }

        [Test]
        public async Task WhenPublishingMessagesWithCustomHeaders_HeadersCanBeReadOnSubscriberSide()
        {
            const string HeaderKey = "MyHeader";
            const string HeaderValue = "MyValue";

            var publishOptions = new PublishOptions();
            publishOptions.Headers.Add(HeaderKey, HeaderValue);

            await this.publisher.Publish(new Event { Bar = 42 }, publishOptions);

            this.context.HandlerCaughtHeaders.Should()
                .Contain(HeaderKey, HeaderValue);
            this.context.AsyncHandlerCaughtHeaders.Should()
                .Contain(HeaderKey, HeaderValue);
        }

        public class HandlerRegistrySimulator : HandlerRegistry
        {
            private readonly Context context;

            public HandlerRegistrySimulator(Context context)
            {
                this.context = context;
            }

            public override IReadOnlyCollection<object> GetHandlers(Type messageType)
            {
                if (messageType == typeof(Event))
                {
                    return this.ConsumeWith(
                        new AsyncMessageHandler(this.context),
                        new MessageHandler(this.context).AsAsync());
                }

                return this.ConsumeAll();
            }
        }

        public class AsyncMessageHandler : IHandleMessageAsync<Event>
        {
            private readonly Context context;

            public AsyncMessageHandler(Context context)
            {
                this.context = context;
            }

            public Task Handle(Event message, IBusForHandler bus)
            {
                this.context.AsyncHandlerCalled += 1;
                this.context.AsyncHandlerCaughtHeaders = bus.Headers(message);
                return Task.FromResult(0);
            }
        }

        public class MessageHandler : IHandleMessage<Event>
        {
            private readonly Context context;

            public MessageHandler(Context context)
            {
                this.context = context;
            }

            public void Handle(Event message, IBusForHandler bus)
            {
                this.context.HandlerCalled += 1;
                this.context.HandlerCaughtHeaders = bus.Headers(message);
            }
        }

        public class Event
        {
            public int Bar { get; set; }
        }

        public class Context
        {
            public int AsyncHandlerCalled { get; set; }

            public int HandlerCalled { get; set; }

            public IDictionary<string, string> AsyncHandlerCaughtHeaders { get; set; }

            public IDictionary<string, string> HandlerCaughtHeaders { get; set; }
        }
    }
}