﻿namespace SuperGlue.EventStore.ProcessManagers
{
    public interface IProcessManagerState
    {
        string Id { get; set; }
        int Version { get; }

        void BuildFromHistory(IEventStream eventStream);
        IEventStream GetUncommittedChanges();
        void ClearUncommittedChanges();

        void TransferState(object evnt);
    }
}