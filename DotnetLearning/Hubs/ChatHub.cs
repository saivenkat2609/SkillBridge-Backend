
using DotnetLearning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DotnetLearning.Hubs
{
    [Authorize]

    public class ChatHub:Hub
    {
        private static ConcurrentDictionary<string, ConcurrentBag<string>> _connections = new();
        private readonly AppDbContext _context;
        private async Task SendToUserConnections(string userId, string eventName, object data)
        {
            if (_connections.TryGetValue(userId, out var connections))
            {
                foreach (var connectionId in connections)
                {
                    await Clients.Client(connectionId).SendAsync(eventName, data);
                }
            }
        }
        public ChatHub(AppDbContext context)
        {
            _context = context;
        }
        public async Task SendMessage(string receiverId, string message)
        {
            var senderId= Context.UserIdentifier;
            var messageObject= new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = message,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(messageObject);
            await _context.SaveChangesAsync();
            await SendToUserConnections(receiverId, "ReceiveMessage", messageObject);
            await SendToUserConnections(senderId, "ReceiveMessage", messageObject);
        }
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;
            _connections.GetOrAdd(userId, new ConcurrentBag<string>()).Add(connectionId);
            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;
            if (_connections.TryGetValue(userId, out var connections))
            {
                var newBag = new ConcurrentBag<string>(
                connections.Where(c => c != connectionId));
                if (newBag.IsEmpty)
                {
                    _connections.TryRemove(userId, out _);
                }
                else
                {
                    _connections.TryUpdate(userId,newBag,connections);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
