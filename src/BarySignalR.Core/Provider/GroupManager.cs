using Microsoft.AspNetCore.SignalR;

namespace BarySignalR.Core.Provider;

/// <summary>
/// Implements the SignalR group manager through BarySignalR.
/// <see cref="HubContext"/>
/// </summary>
internal class GroupManager : IGroupManager
{
    private readonly string hubName;
    private readonly IActorProviderFactory providerFactory;

    public GroupManager(string hubName, IActorProviderFactory providerFactory)
    {
        this.hubName = hubName;
        this.providerFactory = providerFactory;
    }

    public Task AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    )
    {
        return providerFactory
            .GetGroupActor(hubName, groupName)
            .AddToGroupAsync(connectionId, cancellationToken);
    }

    public Task RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    )
    {
        return providerFactory
            .GetGroupActor(hubName, groupName)
            .RemoveFromGroupAsync(connectionId, cancellationToken);
    }
}
