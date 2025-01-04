using System;
using Microsoft.AspNetCore.SignalR;

namespace BarySignalR.Core.Provider;

/// <summary>
/// Implements the SignalR IHubContext using the BarySignalR services - allows sending messages to connected clients from within grains
/// </summary>
internal sealed class HubContext<THubClient> : IHubContext<Hub<THubClient>, THubClient>
    where THubClient : class
{
    private readonly HubContext hubContext;

    public HubContext(HubContext hubContext)
    {
        this.hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public IHubClients<THubClient> Clients => new HubClients<THubClient>(hubContext.Clients);

    public IGroupManager Groups => hubContext.Groups;
}
