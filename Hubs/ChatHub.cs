using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shioko.Models;
using Shioko.Services;

namespace Shioko.Tinder.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext ctx;
        public ChatHub(AppDbContext context)
        {
            ctx = context;
        }

        // called by the client to join a specific chat room
        public async Task JoinChat(int chatThreadId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatThreadId.ToString());
        }

        // called by the client to send a message to a chat room
        public async Task SendMessage(int chatThreadId, string messsageContent)
        {
            var userIdClaim = Context.User?.FindFirst(CustomClaimTypes.UserId);
            if (userIdClaim == null || !Int32.TryParse(userIdClaim.Value, out int senderUserId))
            {
                // cannot send message if not authenticated properly
                return;
            }

            // TODO validate chatThreadId (CRUD in database)
            // TODO validate/moderate messsageContent

            var message = new ChatMessage
            {
                ChatThreadId = chatThreadId,
                SenderUserId = senderUserId,
                Content = messsageContent,
                CreatedAt = DateTime.UtcNow,
            };

            // TODO
            ctx.ChatMessages.Add(message);
            await ctx.SaveChangesAsync();

            // broadcast the message to all clients in the chat room
            await Clients.Group(chatThreadId.ToString()).SendAsync("ReceiveMessage", new
            {
                //chatThreadId = chatThreadId,
                //senderUserId = senderUserId,
                //content = messsageContent,
                //createdAt = message.CreatedAt,
                id = chatThreadId,
                senderUserId = senderUserId,
                content = messsageContent,
                timestamp = ((DateTimeOffset)message.CreatedAt).ToUnixTimeSeconds(),
            });
        }

    }
}
