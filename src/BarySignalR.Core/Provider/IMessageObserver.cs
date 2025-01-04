namespace BarySignalR.Core.Provider
{
    public interface IMessageObserver
    {
        Task SendAllMessageAsync(AnonymousMessage allMessage, CancellationToken cancellationToken = default);
        Task SendAddressedMessageAsync(AddressedMessage msg, CancellationToken cancellationToken = default);
    }
}
