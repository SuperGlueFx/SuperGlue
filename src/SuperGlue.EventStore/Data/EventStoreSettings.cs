using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EventStore.ClientAPI;
using SuperGlue.Configuration;

namespace SuperGlue.EventStore.Data
{
    public class EventStoreSettings
    {
        private readonly ICollection<Action<ConnectionSettingsBuilder>> _settingsModifiers = new Collection<Action<ConnectionSettingsBuilder>>();
        private readonly ICollection<Action<IDictionary<string, object>>> _commitHeadersModifiers = new Collection<Action<IDictionary<string, object>>>();

        public EventStoreSettings()
        {
            ConnectionStringName = "EventStore";
        }

        public string ConnectionStringName { get; private set; }
        public Func<IDictionary<string, object>, object, Guid, string, string> FindCommandStreamFor { get; private set; }

        public EventStoreSettings UseConnectionStringName(string connectionString)
        {
            ConnectionStringName = connectionString;
            return this;
        }

        public EventStoreSettings ModifySettings(Action<ConnectionSettingsBuilder> modifier)
        {
            _settingsModifiers.Add(modifier);
            return this;
        }

        public EventStoreSettings StoreCommands(Func<IDictionary<string, object>, object, Guid, string, string> findStream)
        {
            FindCommandStreamFor = findStream;

            return this;
        }

        public EventStoreSettings DontStoreCommands()
        {
            FindCommandStreamFor = null;

            return this;
        }

        public EventStoreSettings ModifyHeadersUsing(Action<IDictionary<string, object>> modifier)
        {
            _commitHeadersModifiers.Add(modifier);

            return this;
        }

        internal Tuple<EventStoreConnectionString, IEventStoreConnection> CreateConnection(IApplicationConfiguration configuration)
        {
            var connectionString = new EventStoreConnectionString(ConnectionStringName, configuration);

            var connection = connectionString.CreateConnection(x =>
            {
                foreach (var modifier in _settingsModifiers)
                    modifier(x);
            });

            return new Tuple<EventStoreConnectionString, IEventStoreConnection>(connectionString, connection);
        }

        internal void ModifyHeaders(IDictionary<string, object> headers)
        {
            foreach (var commitHeadersModifier in _commitHeadersModifiers)
                commitHeadersModifier(headers);
        }
    }
}