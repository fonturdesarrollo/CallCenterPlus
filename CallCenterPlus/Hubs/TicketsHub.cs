using Microsoft.AspNetCore.SignalR;

namespace CallCenterPlus.Hubs;

// Push-only hub: no client-invokable methods. RequestController raises
// "TicketQueued" whenever a ticket enters status 1 (new request or reopen);
// Views/Agent/Requests.cshtml listens for it to refresh the "En cola" tab.
public class TicketsHub : Hub
{
}
