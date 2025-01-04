using BarySignalR.Backplane.GrainInterfaces;
using BarySignalR.Core;
using BarySignalR.Core.Provider;
using Orleans.Providers;
using Orleans.Timers;

namespace BarySignalR.Backplane.GrainImplementations
{
    [StorageProvider(ProviderName = Constants.USER_STORAGE_PROVIDER)]
    public class UserActorGrain : Grain<UserActorGrainState>, IUserActorGrain
    {
        private bool dirty = false;

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var timerRegistry = (ITimerRegistry?)ServiceProvider?.GetService(typeof(ITimerRegistry));
            var grainContext = (IGrainContext?)ServiceProvider?.GetService(typeof(IGrainContext));
            if (grainContext != null)
                timerRegistry?.RegisterGrainTimer(grainContext, WriteStateIfDirty, string.Empty, new GrainTimerCreationOptions
                {
                    DueTime = TimeSpan.FromSeconds(30),
                    Period = TimeSpan.FromSeconds(30)
                });
            return base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(
            DeactivationReason reason,
            CancellationToken cancellationToken
        )
        {
            await WriteStateIfDirty(string.Empty, cancellationToken);
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        private Task WriteStateIfDirty(string arg1, CancellationToken cancellationToken)
        {
            if (!dirty)
                return Task.CompletedTask;
            return WriteStateAsync();
        }

        public Task AcceptMessageAsync(
            AnonymousMessage message,
            GrainCancellationToken cancellationToken
        )
        {
            return Task.WhenAll(
                    State.ConnectionIds
                        .Where(connId => !message.Excluding.Contains(connId))
                        .Select(connId => GrainFactory.GetGrain<IClientGrain>(connId))
                        .Select(
                            client => client.AcceptMessageAsync(message.Payload, cancellationToken)
                        )
                )
                .WithCancellation(cancellationToken.CancellationToken);
        }

        public Task AddToUserAsync(string connectionId, GrainCancellationToken cancellationToken)
        {
            dirty = State.ConnectionIds.Add(connectionId) || dirty;
            return Task.CompletedTask;
        }

        public Task RemoveFromUserAsync(
            string connectionId,
            GrainCancellationToken cancellationToken
        )
        {
            dirty = State.ConnectionIds.Remove(connectionId) || dirty;
            return Task.CompletedTask;
        }
    }

    [GenerateSerializer]
    public class UserActorGrainState
    {
        [Id(0)]
        public ISet<string> ConnectionIds { get; set; } = new HashSet<string>();
    }
}
