using BarySignalR.Core.Provider;

namespace BarySignalR.Backplane.GrainInterfaces
{
    public interface IGroupActorGrain : IGrainWithStringKey
    {
        Task AddToGroupAsync(string connectionId, GrainCancellationToken cancellationToken);
        Task RemoveFromGroupAsync(string connectionId, GrainCancellationToken cancellationToken);
        Task AcceptMessageAsync(AnonymousMessage message, GrainCancellationToken cancellationToken);

    }

}
