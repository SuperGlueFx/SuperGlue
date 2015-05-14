﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SuperGlue.EventStore.Data;

namespace SuperGlue.EventStore.ProcessManagers
{
    public abstract class BaseProcessManager<TState> : IManageProcess where TState : IProcessManagerState
    {
        private readonly IRepository _repository;
        private readonly IDictionary<string, object> _environment;

        private readonly IDictionary<string, TState> _instances = new Dictionary<string, TState>();
        private IReadOnlyDictionary<Type, Tuple<Action<object, TState, IDictionary<string, object>>, Func<object, string>>> _eventMappings;

        protected BaseProcessManager(IRepository repository, IDictionary<string, object> environment)
        {
            _repository = repository;
            _environment = environment;
        }

        public virtual string ProcessName { get { return GetType().FullName.Replace(".", "-").ToLower(); } }

        public IEnumerable<string> GetStreamsToProcess()
        {
            yield return GetTimeOutsStreamName();

            foreach (var stream in GetInterestingStreams())
                yield return stream;
        }

        public void Start()
        {
            var eventMappings = new Dictionary<Type, Tuple<Action<object, TState, IDictionary<string, object>>, Func<object, string>>>();
            var mappingContext = new EventMappingContext<TState>(eventMappings);

            MapEvents(mappingContext);

            _eventMappings = new ReadOnlyDictionary<Type, Tuple<Action<object, TState, IDictionary<string, object>>, Func<object, string>>>(eventMappings);

            OnStarted();
        }

        public void Apply(object evnt, int version, IDictionary<string, object> metaData)
        {
            foreach (var type in GetTypesFrom(evnt))
            {
                if (!_eventMappings.ContainsKey(type))
                    continue;

                var handlerMapping = _eventMappings[type];

                var id = handlerMapping.Item2(evnt);

                if (!_instances.ContainsKey(id))
                {
                    var processManagerInstance = Load(id);

                    _instances[id] = processManagerInstance;
                }

                handlerMapping.Item1(evnt, _instances[id], metaData);
            }
        }

        public void Commit()
        {
            foreach (var instance in _instances)
                Save(instance.Value);

            _instances.Clear();
        }

        protected abstract IEnumerable<string> GetInterestingStreams();

        private TState Load(string id)
        {
            var state = CreateDefaultState(id);

            var events = _repository.LoadStream(GetStreamName(id));

            state.BuildFromHistory(new EventStream(events));

            return state;
        }

        private void Save(TState state)
        {
            var streamName = GetStreamName(state.Id);
            var changes = state.GetUncommittedChanges();

            _repository.SaveToStream(streamName, changes.Events, Guid.NewGuid(), new ActionMetaData(_environment));

            state.ClearUncommittedChanges();
        }

        protected abstract void MapEvents(EventMappingContext<TState> mappingContext);
        protected abstract TState CreateDefaultState(string id);

        protected virtual void OnStarted() { }
        protected virtual void OnCommitted() { }

        protected void RequestTimeOut(object evnt, IDictionary<string, object> metaData, DateTime at)
        {
            _repository.RequestTimeOut(GetTimeOutsStreamName(), Guid.NewGuid(), evnt, new ReadOnlyDictionary<string, object>(metaData), at);
        }

        protected virtual string GetStreamName(string id)
        {
            return string.Format("ProcessManagers-{0}-{1}", ProcessName, id);
        }

        protected virtual string GetTimeOutsStreamName()
        {
            return string.Format("ProcessManagers-{0}-TimeOuts", ProcessName);
        }

        private static IEnumerable<Type> GetTypesFrom(object evnt)
        {
            if (evnt == null)
                yield break;

            yield return evnt.GetType();

            var baseType = evnt.GetType().BaseType;

            while (baseType != null)
            {
                yield return baseType;
                baseType = baseType.BaseType;
            }

            foreach (var @interface in evnt.GetType().GetInterfaces())
                yield return @interface;
        }
    }
}