﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using BarySignalR.Core;
using BarySignalR.Core.Data;
using BarySignalR.Core.Provider;

namespace BarySignalR.SignalR
{
    /// <summary>
    /// Implements a SignalR hub backplane through a pub sub mechanism
    /// </summary>
    /// <typeparam name="THub">The hub type this is applicable to</typeparam>
    public class BarySignalRHubLifetimeManager<THub> : HubLifetimeManager<THub>, IDisposable
        where THub : Hub
    {
        private const string CONNECTION_LATEST_MESSAGE_KEY = "BarySignalR_LatestClientMessageHandle";
        private bool disposed;
        private readonly HubConnectionStore hubConnectionStore = new();
        private readonly IActorProviderFactory actorProviderFactory;
        private readonly IMessageObservable messageObservable;
        private readonly IMessageObserver messageObserver;
        private readonly IMessageArgsSerializer messageArgsSerializer;
        private readonly ILogger<BarySignalRHubLifetimeManager<THub>> logger;
        private readonly string hubName = typeof(THub).Name;
        private SubscriptionHandle? allSubscriptionHandle;

        private MessageHandle latestAllMessageHandle;

        private BarySignalRHubLifetimeManager(
            IActorProviderFactory actorProviderFactory,
            IMessageObservable messageObservable,
            IMessageObserver messageObserver,
            IMessageArgsSerializer messageArgsSerializer,
            ILogger<BarySignalRHubLifetimeManager<THub>> logger
        )
        {
            this.actorProviderFactory =
                actorProviderFactory
                ?? throw new ArgumentNullException(nameof(actorProviderFactory));
            this.messageObservable =
                messageObservable ?? throw new ArgumentNullException(nameof(messageObservable));
            this.messageObserver =
                messageObserver ?? throw new ArgumentNullException(nameof(messageObserver));
            this.messageArgsSerializer =
                messageArgsSerializer
                ?? throw new ArgumentNullException(nameof(messageArgsSerializer));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create an instance of this class and subscribes to messages that are broadcasted to the "all" stream for the hub
        /// </summary>
        /// <param name="groupActorProvider"></param>
        /// <param name="userActorProvider"></param>
        /// <param name="messageObservable"></param>
        /// <param name="messageObserver"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A new instance of <see cref="BarySignalRHubLifetimeManager<THub>" /> that is subscribed to the anonymous message broadcasts</returns>
        public static async Task<BarySignalRHubLifetimeManager<THub>> CreateAsync(
            IActorProviderFactory actorProviderFactory,
            IMessageObservable messageObservable,
            IMessageObserver messageObserver,
            IMessageArgsSerializer messageArgsSerializer,
            ILogger<BarySignalRHubLifetimeManager<THub>> logger,
            CancellationToken cancellationToken = default
        )
        {
            var manager = new BarySignalRHubLifetimeManager<THub>(
                actorProviderFactory,
                messageObservable,
                messageObserver,
                messageArgsSerializer,
                logger
            );
            manager.allSubscriptionHandle = await messageObservable.SubscribeToAllAsync(
                manager.OnAnonymousMessageReceived,
                manager.OnAnonymousSubscriptionEnd,
                default,
                cancellationToken
            );
            return manager;
        }

        public override async Task OnConnectedAsync(HubConnectionContext connection)
        {
            hubConnectionStore.Add(connection);

            if (connection.UserIdentifier != null)
            {
                await actorProviderFactory
                    .GetUserActor(hubName, connection.UserIdentifier)
                    .AddToUserAsync(connection.ConnectionId);
            }
            try
            {
                await messageObservable.SubscribeToConnectionAsync(
                    connection.ConnectionId,
                    OnAddressedMessageReceived,
                    OnClientSubscriptionEnd,
                    GetClientMessageHandle(connection)
                );
            }
            catch (ArgumentOutOfRangeException e)
            {
                logger.LogWarning(
                    e,
                    "Unable to replay client messages since last connect for client {connectionId}",
                    connection.ConnectionId
                );
                await messageObservable.SubscribeToConnectionAsync(
                    connection.ConnectionId,
                    OnAddressedMessageReceived,
                    OnClientSubscriptionEnd,
                    default
                );
            }
        }

        public override async Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            hubConnectionStore.Remove(connection);
            if (connection.UserIdentifier != null)
            {
                await actorProviderFactory
                    .GetUserActor(hubName, connection.UserIdentifier)
                    .RemoveFromUserAsync(connection.ConnectionId);
            }
            await messageObservable.UnsubscribeFromConnectionAsync(connection.ConnectionId);
        }

        public override Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default
        )
        {
            var group = actorProviderFactory.GetGroupActor(hubName, groupName);
            return group.AddToGroupAsync(connectionId, cancellationToken);
        }

        public override Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default
        )
        {
            var group = actorProviderFactory.GetGroupActor(hubName, groupName);
            return group.RemoveFromGroupAsync(connectionId, cancellationToken);
        }

        public override Task SendAllAsync(
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            return messageObserver.SendAllMessageAsync(
                new AnonymousMessage(
                    EmptySet<string>.Instance,
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                ),
                cancellationToken
            );
        }

        public override Task SendAllExceptAsync(
            string methodName,
            object?[] args,
            IReadOnlyList<string> excludedConnectionIds,
            CancellationToken cancellationToken = default
        )
        {
            return messageObserver.SendAllMessageAsync(
                new AnonymousMessage(
                    excludedConnectionIds.ToSet(),
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                ),
                cancellationToken
            );
        }

        public override Task SendConnectionAsync(
            string connectionId,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            return SendConnectionsAsync(
                new SingletonList<string>(connectionId),
                methodName,
                args,
                cancellationToken
            );
        }

        public override Task SendConnectionsAsync(
            IReadOnlyList<string> connectionIds,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            var toAwait = new List<Task>();
            foreach (var connectionId in connectionIds)
            {
                var local = hubConnectionStore[connectionId];
                var msg = new AddressedMessage(
                    connectionId,
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                );
                if (local != null)
                {
                    toAwait.Add(OnAddressedMessageReceived(msg, default));
                }
                else
                {
                    toAwait.Add(messageObserver.SendAddressedMessageAsync(msg, cancellationToken));
                }
            }
            return Task.WhenAll(toAwait);
        }

        public override Task SendGroupAsync(
            string groupName,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            var group = actorProviderFactory.GetGroupActor(hubName, groupName);
            return group.AcceptMessageAsync(
                new AnonymousMessage(
                    EmptySet<string>.Instance,
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                ),
                cancellationToken
            );
        }

        public override Task SendGroupExceptAsync(
            string groupName,
            string methodName,
            object?[] args,
            IReadOnlyList<string> excludedConnectionIds,
            CancellationToken cancellationToken = default
        )
        {
            var group = actorProviderFactory.GetGroupActor(hubName, groupName);
            return group.AcceptMessageAsync(
                new AnonymousMessage(
                    excludedConnectionIds.ToSet(),
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                ),
                cancellationToken
            );
        }

        public override Task SendGroupsAsync(
            IReadOnlyList<string> groupNames,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            return Task.WhenAll(
                groupNames.Select(
                    group => SendGroupAsync(group, methodName, args, cancellationToken)
                )
            );
        }

        public override Task SendUserAsync(
            string userId,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            var user = actorProviderFactory.GetUserActor(hubName, userId);
            return user.AcceptMessageAsync(
                new AnonymousMessage(
                    EmptySet<string>.Instance,
                    new MethodMessage(methodName, messageArgsSerializer.Serialize(args))
                ),
                cancellationToken
            );
        }

        public override Task SendUsersAsync(
            IReadOnlyList<string> userIds,
            string methodName,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            return Task.WhenAll(
                userIds.Select(userId => SendUserAsync(userId, methodName, args, cancellationToken))
            );
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
                return;
            if (allSubscriptionHandle != null)
            {
                await messageObservable.UnsubscribeFromAllAsync(allSubscriptionHandle);
                allSubscriptionHandle = null;
            }
            disposed = true;
        }

        [Obsolete("Use DisposeAsync instead")]
        public void Dispose()
        {
            if (disposed)
                return;
            DisposeAsync().AsTask().Wait();
        }

        private async Task OnAddressedMessageReceived(AddressedMessage arg, MessageHandle handle)
        {
            var connection = hubConnectionStore[arg.ConnectionId];
            if (connection == null)
                return;
            if (connection.ConnectionAborted.IsCancellationRequested)
                return;
            await connection.WriteAsync(
                new InvocationMessage(
                    arg.Payload.MethodName,
                    messageArgsSerializer.Deserialize(arg.Payload.SerializedArgs)
                )
            );

            var latestClientMessageHandle = GetClientMessageHandle(connection);
            if (
                handle != default
                // We only want to store the latest message, unless the group has changed, in which case we cannot rely on always increasing ids
                && (
                    handle.MessageId > latestClientMessageHandle.MessageId
                    || handle.MessageGroup != latestClientMessageHandle.MessageGroup
                )
            )
            {
                connection.Items[CONNECTION_LATEST_MESSAGE_KEY] = handle;
            }
        }

        private Task OnClientSubscriptionEnd(string connectionId)
        {
            var conn = hubConnectionStore[connectionId];
            if (conn == null)
                return Task.CompletedTask;
            if (conn.ConnectionAborted.IsCancellationRequested)
                return Task.CompletedTask;
            return OnConnectedAsync(conn);
        }

        private async Task OnAnonymousSubscriptionEnd(SubscriptionHandle _)
        {
            try
            {
                allSubscriptionHandle = await messageObservable.SubscribeToAllAsync(
                    OnAnonymousMessageReceived,
                    OnAnonymousSubscriptionEnd,
                    latestAllMessageHandle,
                    default
                );
            }
            catch (ArgumentOutOfRangeException e)
            {
                logger.LogWarning(e, "Unable to replay anonymous messages since last connect");
                allSubscriptionHandle = await messageObservable.SubscribeToAllAsync(
                    OnAnonymousMessageReceived,
                    OnAnonymousSubscriptionEnd,
                    default,
                    default
                );
            }
        }

        private async Task OnAnonymousMessageReceived(AnonymousMessage msg, MessageHandle handle)
        {
            var toAwait = new List<ValueTask>();
            foreach (var conn in hubConnectionStore)
            {
                if (msg.Excluding.Contains(conn.ConnectionId))
                    continue;
                if (conn.ConnectionAborted.IsCancellationRequested)
                    continue;
                toAwait.Add(
                    conn.WriteAsync(
                        new InvocationMessage(
                            msg.Payload.MethodName,
                            messageArgsSerializer.Deserialize(msg.Payload.SerializedArgs)
                        )
                    )
                );
            }
            await Task.WhenAll(toAwait.Where(vt => !vt.IsCompleted).Select(vt => vt.AsTask()));
            if (
                handle != default
                // We only want to store the latest message, unless the group has changed, in which case we cannot rely on always increasing ids
                && (
                    handle.MessageId > latestAllMessageHandle.MessageId
                    || handle.MessageGroup != latestAllMessageHandle.MessageGroup
                )
            )
            {
                latestAllMessageHandle = handle;
            }
        }

        private MessageHandle GetClientMessageHandle(HubConnectionContext connection)
        {
            if (
                connection.Items.TryGetValue(
                    CONNECTION_LATEST_MESSAGE_KEY,
                    out var latestClientMessageHandle
                )
            )
            {
                if (latestClientMessageHandle != null)
                {
                    return (MessageHandle)latestClientMessageHandle;
                }
            }

            return default;
        }
    }
}
