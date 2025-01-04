using BarySignalR.Backplane.GrainInterfaces;
using BarySignalR.Core;
using BarySignalR.Core.Provider;
using BarySignalR.Core.State;
namespace BarySignalR.Backplane.GrainAdaptors
{
    public class GrainGroupActor : IGroupActor
    {
        private readonly string hubName;
        private readonly IGroupActorGrain groupActorGrain;

        public GrainGroupActor(string hubName, IGroupActorGrain groupActorGrain)
        {
            this.hubName = hubName;
            this.groupActorGrain = groupActorGrain;
        }

        public Task AcceptMessageAsync(AnonymousMessage message, CancellationToken cancellationToken = default)
        {
            message = new AnonymousMessage(message.Excluding.Select(x => $"{hubName}::{x}").ToSet(), message.Payload);
            var token = new GrainCancellationTokenSource();
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => token.Cancel());
            }

            return groupActorGrain.AcceptMessageAsync(message, token.Token);
        }

        public Task AddToGroupAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            connectionId = $"{hubName}::{connectionId}";
            var token = new GrainCancellationTokenSource();
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => token.Cancel());
            }

            return groupActorGrain.AddToGroupAsync(connectionId, token.Token);
        }

        public Task RemoveFromGroupAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            connectionId = $"{hubName}::{connectionId}";
            var token = new GrainCancellationTokenSource();
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => token.Cancel());
            }

            return groupActorGrain.RemoveFromGroupAsync(connectionId, token.Token);
        }
    }
}
