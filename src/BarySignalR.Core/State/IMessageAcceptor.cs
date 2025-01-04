using BarySignalR.Core.Provider;

namespace BarySignalR.Core.State;

public interface IMessageAcceptor
{
    Task AcceptMessageAsync(
        AnonymousMessage targetedMessage,
        CancellationToken cancellationToken = default
    );
}
