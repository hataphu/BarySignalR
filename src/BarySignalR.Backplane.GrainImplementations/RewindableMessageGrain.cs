﻿using BarySignalR.Backplane.GrainInterfaces;
using BarySignalR.Core;
using Orleans.Providers;
using BarySignalR.Core.Provider;
using BarySignalR.Core.Data;
using Orleans.Timers;

namespace BarySignalR.Backplane.GrainImplementations
{
    [StorageProvider(ProviderName = Constants.MESSAGE_STORAGE_PROVIDER)]
    public class RewindableMessageGrain<T>
        : Grain<RewindableMessageGrainState<T>>,
            IRewindableMessageGrain<T>
    {
        private int maxMessages;
        private bool persistenceEnabled;
        private TimeSpan persistenceInterval;
        private CircularBuffer<RewindableMessageWrapper<T>> messageBuffer = null!;
        private long OldestMessageId => messageBuffer.Front().MessageId;
        private long LatestMessageId => State.LatestMessageId;
        private bool dirty = false;

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var config = (BarySignalRSiloConfig?)ServiceProvider?.GetService(typeof(BarySignalRSiloConfig));
            var timerRegistry = (ITimerRegistry?)ServiceProvider?.GetService(typeof(ITimerRegistry));
            var grainContext = (IGrainContext?)ServiceProvider?.GetService(typeof(IGrainContext));
            maxMessages = config?.MaxMessageRewind ?? 0;
            persistenceEnabled = config?.PersistenceEnabled ?? false;
            persistenceInterval = config?.PerstenceInterval ?? TimeSpan.Zero;

            if (State == null || State.Messages == null)
            {
                State = new RewindableMessageGrainState<T>
                {
                    MessageGroup = Guid.NewGuid(),
                    Messages = Array.Empty<RewindableMessageWrapper<T>>()
                };
            }
            messageBuffer = new CircularBuffer<RewindableMessageWrapper<T>>(
                maxMessages,
                State.Messages
            );

            if (persistenceInterval > TimeSpan.Zero)
            {
                if (grainContext != null)
                    timerRegistry?.RegisterGrainTimer(grainContext, WriteStateIfDirtyAsync, string.Empty, new GrainTimerCreationOptions
                    {
                        DueTime = persistenceInterval,
                        Period = persistenceInterval
                    });
            }
            return base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(
            DeactivationReason reason,
            CancellationToken cancellationToken
        )
        {
            await WriteStateIfDirtyAsync(string.Empty,cancellationToken);
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        public Task<List<(T message, MessageHandle handle)>> GetMessagesSinceAsync(
            MessageHandle handle
        )
        {
            var messageIdExclusive = handle.MessageId;
            if (messageIdExclusive >= LatestMessageId || handle.MessageGroup != State.MessageGroup)
            {
                // It's possible we have been recreated on a new silo (and thus restarted our counter), so we simply return an empty value
                // This could happen if we use the in memory grain storage.  And if we are, then we do not need to be reliable
                return Task.FromResult(new List<(T, MessageHandle)>());
            }
            // If the oldest message is for example 2, and we want all messages since 1, we can still service that, so we add 1
            if (OldestMessageId > messageIdExclusive + 1)
            {
                throw new ArgumentOutOfRangeException($"Oldest message is: {OldestMessageId}");
            }
            var messages = messageBuffer.SkipWhile(x => x.MessageId <= messageIdExclusive).ToList();
            return Task.FromResult(
                messages
                    .Select(
                        msg => (msg.Message, new MessageHandle(msg.MessageId, State.MessageGroup))
                    )
                    .ToList()
            );
        }

        public Task<MessageHandle> PushMessageAsync(T message)
        {
            State.LatestMessageId++;
            messageBuffer.PushBack(
                new RewindableMessageWrapper<T>
                {
                    Message = message,
                    SentAt = DateTimeOffset.UtcNow,
                    MessageId = State.LatestMessageId
                }
            );
            dirty = true;
            return Task.FromResult(new MessageHandle(LatestMessageId, State.MessageGroup));
        }

        private Task WriteStateIfDirtyAsync(string arg1, CancellationToken cancellationToken)
        {
            if (!dirty || !persistenceEnabled)
            {
                return Task.CompletedTask;
            }
            State.Messages = messageBuffer.ToArray();
            return WriteStateAsync();
        }
    }

    [GenerateSerializer]
    public class RewindableMessageGrainState<T>
    {
        [Id(0)]
        public Guid MessageGroup { get; set; }

        [Id(1)]
        public long LatestMessageId { get; set; }

        [Id(2)]
        public RewindableMessageWrapper<T>[] Messages { get; set; } = null!;
    }

    [GenerateSerializer]
    public class RewindableMessageWrapper<T>
    {
        [Id(0)]
        public long MessageId { get; set; }

        [Id(1)]
        public DateTimeOffset SentAt { get; set; }

        [Id(2)]
        public T Message { get; set; } = default!;
    }
}
