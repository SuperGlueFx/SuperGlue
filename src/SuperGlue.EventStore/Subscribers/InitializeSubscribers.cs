﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using SuperGlue.Configuration;
using SuperGlue.EventStore.Data;
using SuperGlue.UnitOfWork;

namespace SuperGlue.EventStore.Subscribers
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class InitializeSubscribers : IStartApplication
    {
        private readonly IHandleEventSerialization _eventSerialization;
        private readonly IEventStoreConnection _eventStoreConnection;
        private static bool running;
        private readonly IDictionary<string, IServiceSubscription> _serviceSubscriptions = new Dictionary<string, IServiceSubscription>();

        public InitializeSubscribers(IHandleEventSerialization eventSerialization, IEventStoreConnection eventStoreConnection)
        {
            _eventSerialization = eventSerialization;
            _eventStoreConnection = eventStoreConnection;
        }

        public string Name => "subscribers";
        public string Chain => "chains.Subscribers";

        public async Task Start(AppFunc chain, IDictionary<string, object> settings, string environment, string[] arguments)
        {
            settings.Log("Starting subscribers for environment: {0}", LogLevel.Debug, environment);

            running = true;

            var subscriptionSettings = settings.GetSettings<SubscribersSettings>();

            var streams = subscriptionSettings.GetSubscribedStreams();

            foreach (var stream in streams)
                await SubscribeService(chain, stream.Item1, stream.Item2, subscriptionSettings.GetPersistentSubscriptionGroupNameFor(stream.Item1, settings), settings).ConfigureAwait(false);
        }

        public Task ShutDown(IDictionary<string, object> settings)
        {
            settings.Log("Shutting down subscribers", LogLevel.Debug);

            running = false;

            foreach (var subscription in _serviceSubscriptions)
                subscription.Value.Close();

            _serviceSubscriptions.Clear();

            return Task.CompletedTask;
        }

        public AppFunc GetDefaultChain(IBuildAppFunction buildApp, IDictionary<string, object> settings, string environment)
        {
            settings.Log("Getting default chain for subscribers for environment: {0}", LogLevel.Debug, environment);
            return buildApp.Use<HandleUnitOfWork>(new HandleUnitOfWorkOptions(true)).Use<ExecuteSubscribers>().Build();
        }

        private async Task SubscribeService(AppFunc chain, string stream, bool liveOnlySubscriptions, string subscriptionKey, IDictionary<string, object> environment)
        {
            environment.Log("Subscribing to stream: {0}", LogLevel.Debug, stream);

            while (true)
            {
                if (!running)
                    return;

                try
                {
                    if (_serviceSubscriptions.ContainsKey(subscriptionKey))
                    {
                        _serviceSubscriptions[subscriptionKey].Close();
                        _serviceSubscriptions.Remove(subscriptionKey);
                    }

                    if (liveOnlySubscriptions)
                    {
                        //TODO:Handle retries on error
                        var eventstoreSubscription = await _eventStoreConnection.SubscribeToStreamAsync(stream, true,
                            async (subscription, evnt) => await PushEventToService(chain, stream, _eventSerialization.DeSerialize(evnt), false, environment,
                                x => environment.Log("Successfully handled event: {0} on stream: {1}", LogLevel.Debug, x.EventId, stream),
                                (x, exception) => environment.Log(exception, "Failed handling event: {0} on stream: {1}", LogLevel.Error, x.EventId, stream)).ConfigureAwait(false),
                            async (subscription, reason, exception) => await SubscriptionDropped(chain, stream, true, subscriptionKey, reason, exception, environment).ConfigureAwait(false)).ConfigureAwait(false);

                        _serviceSubscriptions[subscriptionKey] = new LiveOnlyServiceSubscription(eventstoreSubscription);
                    }
                    else
                    {
                        var eventstoreSubscription = _eventStoreConnection.ConnectToPersistentSubscription(stream, subscriptionKey,
                            async (subscription, evnt) => await PushEventToService(chain, stream, _eventSerialization.DeSerialize(evnt), true, environment,
                                x =>
                                {
                                    environment.Log("Successfully handled event: {0} on stream: {1}", LogLevel.Debug, x.EventId, stream);

                                    subscription.Acknowledge(x.OriginalEvent);
                                }, (x, exception) =>
                                {
                                    environment.Log(exception, "Failed handling event: {0} on stream: {1}", LogLevel.Error, x.EventId, stream);

                                    subscription.Fail(x.OriginalEvent, PersistentSubscriptionNakEventAction.Unknown, exception.Message);
                                }).ConfigureAwait(false), async (subscription, reason, exception) => await SubscriptionDropped(chain, stream, false, subscriptionKey, reason, exception, environment).ConfigureAwait(false), autoAck:false);

                        _serviceSubscriptions[subscriptionKey] = new PersistentServiceSubscription(eventstoreSubscription);
                    }

                    return;
                }
                catch (Exception ex)
                {
                    if (!running)
                        return;

                    environment.Log(ex, "Couldn't subscribe to stream: {0}. Retrying in 5 seconds.", LogLevel.Warn, stream);

                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
        }

        private async Task SubscriptionDropped(AppFunc chain, string stream, bool liveOnlySubscriptions, string subscriptionKey, SubscriptionDropReason reason, Exception exception, IDictionary<string, object> environment)
        {
            if (!running)
                return;

            environment.Log(exception, "Subscription dropped for stream: {0}. Reason: {1}. Retrying...", LogLevel.Warn, stream, reason);

            if (reason != SubscriptionDropReason.UserInitiated)
                await SubscribeService(chain, stream, liveOnlySubscriptions, subscriptionKey, environment).ConfigureAwait(false);
        }

        private static async Task PushEventToService(AppFunc chain, string stream, DeSerializationResult evnt, bool catchup, IDictionary<string, object> environment, Action<DeSerializationResult> done, Action<DeSerializationResult, Exception> error)
        {
            if (!running)
                return;

            if (!evnt.Successful)
            {
                error(evnt, evnt.Error);
                return;
            }

            try
            {
                var requestEnvironment = new Dictionary<string, object>();
                foreach (var item in environment)
                    requestEnvironment[item.Key] = item.Value;

                var request = requestEnvironment.GetEventStoreRequest();

                request.Stream = stream;
                request.Event = evnt;
                request.IsCatchUp = catchup;

                var correlationId = evnt.Metadata.Get(DefaultRepository.CorrelationIdKey, Guid.NewGuid().ToString());

                using (requestEnvironment.OpenCorrelationContext(correlationId))
                using (requestEnvironment.OpenCausationContext(evnt.EventId.ToString()))
                {
                    await chain(requestEnvironment).ConfigureAwait(false);
                }

                done(evnt);
            }
            catch (Exception ex)
            {
                environment.Log(ex, "Couldn't push events from stream: {0}", LogLevel.Error, stream);
                error(evnt, ex);
            }
        }
    }
}