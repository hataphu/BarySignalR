using BarySignalR.Core.Provider;

namespace BarySignalR.Backplane.GrainInterfaces
{
    public interface IUserActorGrain : IGrainWithStringKey
    {
        Task AddToUserAsync(string connectionId, GrainCancellationToken cancellationToken);
        Task RemoveFromUserAsync(string connectionId, GrainCancellationToken cancellationToken);
        Task AcceptMessageAsync(AnonymousMessage message, GrainCancellationToken cancellationToken);

    }
}
