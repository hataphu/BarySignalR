namespace BarySignalR.Core.State
{
    public interface IUserActor : IMessageAcceptor
    {
        Task AddToUserAsync(string connectionId, CancellationToken cancellationToken = default);
        Task RemoveFromUserAsync(
            string connectionId,
            CancellationToken cancellationToken = default
        );
    }
}
