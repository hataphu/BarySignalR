namespace BarySignalR.Core.State
{
    public interface IGroupActor : IMessageAcceptor
    {
        Task AddToGroupAsync(string connectionId, CancellationToken cancellationToken = default);
        Task RemoveFromGroupAsync(
            string connectionId,
            CancellationToken cancellationToken = default
        );
    }
}
