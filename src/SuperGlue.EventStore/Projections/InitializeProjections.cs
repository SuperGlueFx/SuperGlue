 using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using SuperGlue.Configuration;
using SuperGlue.ExceptionManagement;
using SuperGlue.UnitOfWork;

namespace SuperGlue.EventStore.Projections
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class InitializeProjections : IStartApplication
    {
        private readonly IHandleEventSerialization _eventSerialization;
        private readonly IEnumerable<IEventStoreProjection> _projections;
        private readonly IEventStoreConnection _eventStoreConnection;
        private static bool running;
        private readonly IDictionary<string, ProjectionSubscription> _projectionSubscriptions = new Dictionary<string, ProjectionSubscription>();

        public InitializeProjections(IHandleEventSerialization eventSerialization, IEnumerable<IEventStoreProjection> projections, IEventStoreConnection eventStoreConnection)
        {
            _eventSerialization = eventSerialization;
            _projections = projections;
            _eventStoreConnection = eventStoreConnection;
        }

        public string Name => "projections";
        public string Chain => "chains.Projections";

        public async Task Start(AppFunc chain, IDictionary<string, object> settings, string environment, string[] arguments)
        {
            settings.Log("Starting projections for environment: {0}", LogLevel.Debug, environment);

            running = true;

            foreach (var projection in _projections)
                await SubscribeProjection(projection, chain, settings).ConfigureAwait(false);
        }

        public Task ShutDown(IDictionary<string, object> settings)
        {
            settings.Log("Shutting down projections", LogLevel.Debug);

            running = false;

            foreach (var subscription in _projectionSubscriptions)
                subscription.Value.Close();

            _projectionSubscriptions.Clear();

            return Task.CompletedTask;
        }

        public AppFunc GetDefaultChain(IBuildAppFunction buildApp, IDictionary<string, object> settings, string environment)
        {
            settings.Log("Getting default chain for projections for environment: {0}", LogLevel.Debug, environment);

            return buildApp
                .Use<Retry>(5, TimeSpan.FromSeconds(1), "projection")
                .Use<HandleUnitOfWork>(new HandleUnitOfWorkOptions(true))
                .Use<ExecuteProjection>()
                .Use<SetLastEvent>()
                .Build();
        }

        private async Task SubscribeProjection(IEventStoreProjection currentEventStoreProjection, AppFunc chain, IDictionary<string, object> environment)
        {
            environment.Log("Subscribing projection: {0}", LogLevel.Debug, currentEventStoreProjection.ProjectionName);

            var bufferSettings = environment.GetSettings<ProjectionSettings>().GetBufferSettings();

            while (true)
            {
                if (!running)
                    return;

                if (_projectionSubscriptions.ContainsKey(currentEventStoreProjection.ProjectionName))
                {
                    _projectionSubscriptions[currentEventStoreProjection.ProjectionName].Close();
                    _projectionSubscriptions.Remove(currentEventStoreProjection.ProjectionName);
                }

                try
                {
                    var eventNumberManager = environment.Resolve<IManageEventNumbersForProjections>();

                    var messageProcessor = new MessageProcessor();
                    var messageSubscription = Observable
                        .FromEvent<DeSerializationResult>(x => messageProcessor.MessageArrived += x, x => messageProcessor.MessageArrived -= x)
                        .Buffer(TimeSpan.FromSeconds(bufferSettings.Seconds), bufferSettings.NumberOfEvents)
                        .Subscribe(async x => await PushEventsToProjections(chain, currentEventStoreProjection, x, environment).ConfigureAwait(false));

                    var eventStoreSubscription = _eventStoreConnection.SubscribeToStreamFrom(currentEventStoreProjection.ProjectionName, 
                        await eventNumberManager.GetLastEvent(currentEventStoreProjection.ProjectionName, environment).ConfigureAwait(false), CatchUpSubscriptionSettings.Default,
                        (subscription, evnt) => messageProcessor.OnMessageArrived(_eventSerialization.DeSerialize(evnt)),
                        subscriptionDropped: async (subscription, reason, exception) => await SubscriptionDropped(chain, currentEventStoreProjection, reason, exception, environment).ConfigureAwait(false));

                    _projectionSubscriptions[currentEventStoreProjection.ProjectionName] = new ProjectionSubscription(messageSubscription, eventStoreSubscription);

                    return;
                }
                catch (Exception ex)
                {
                    if (!running)
                        return;

                    environment.Log(ex, "Couldn't subscribe projection: {0}. Retrying in 5 seconds.", LogLevel.Warn, currentEventStoreProjection.ProjectionName);

                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
        }

        private void StopProjection(IEventStoreProjection projection)
        {
            if (!_projectionSubscriptions.ContainsKey(projection.ProjectionName))
                return;

            _projectionSubscriptions[projection.ProjectionName].Close();
            _projectionSubscriptions.Remove(projection.ProjectionName);
        }

        private async Task SubscriptionDropped(AppFunc chain, IEventStoreProjection projection, SubscriptionDropReason reason, Exception exception, IDictionary<string, object> environment)
        {
            if (!running)
                return;

            environment.Log(exception, "Subscription dropped for projection: {0}. Reason: {1}. Retrying...", LogLevel.Warn, projection.ProjectionName, reason);

            if (reason != SubscriptionDropReason.UserInitiated)
                await SubscribeProjection(projection, chain, environment).ConfigureAwait(false);
        }

        private async Task PushEventsToProjections(AppFunc chain, IEventStoreProjection projection, IEnumerable<DeSerializationResult> events, IDictionary<string, object> environment)
        {
            if (!running)
                return;

            var eventsList = events.ToList();

            var successfullEvents = eventsList
                .Where(x => x.Successful)
                .ToList();

            if (!successfullEvents.Any())
                return;

            try
            {
                var requestEnvironment = new Dictionary<string, object>();
                foreach (var item in environment)
                    requestEnvironment[item.Key] = item.Value;

                var request = requestEnvironment.GetEventStoreRequest();

                request.Projection = projection;
                request.Events = eventsList;

                using (requestEnvironment.OpenCorrelationContext(Guid.NewGuid().ToString()))
                {
                    await chain(requestEnvironment).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                //TODO:Mark service as failing to we can report that via consul
                environment.Log(ex, "Couldn't push events to projection: {0}", LogLevel.Error, projection.ProjectionName);

                StopProjection(projection);

                await environment.Notifications().Error("projections", $"Projection: {projection.ProjectionName} failed!", ex).ConfigureAwait(false);
            }
        }
    }
}