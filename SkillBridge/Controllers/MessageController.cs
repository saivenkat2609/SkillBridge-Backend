using SkillBridge.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SkillBridge.Controllers
{
    [ApiController]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MessageController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        [Route("api/messages/conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var lastMessage = g.First();
                    var otherUser = lastMessage.SenderId == currentUserId
                        ? lastMessage.Receiver
                        : lastMessage.Sender;
                    return new
                    {
                        userId = g.Key,
                        userName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                        lastMessage = lastMessage.Content,
                        lastMessageAt = lastMessage.SentAt,
                        unreadCount = g.Count(m => m.ReceiverId == currentUserId && !m.IsRead)
                    };
                })
                .ToList();

            return Ok(conversations);
        }

        [HttpGet]
        [Route("api/messages/{otherUser}")]
        public async Task<IActionResult> GetMessageHistory(string otherUser)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Where(m => (m.SenderId == currentUser && m.ReceiverId == otherUser) || (m.ReceiverId == currentUser && m.SenderId == otherUser))
                .ToListAsync();
            var sortedMessages = messages.OrderBy(m => m.SentAt).ToList();
            return Ok(sortedMessages);
        }
        [HttpGet]
        [Route("api/messages/unread-count")]
        public async Task<IActionResult> GetUnreadMessages()
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Where(m => m.ReceiverId == currentUser && !m.IsRead)
                .CountAsync();
            return Ok(messages);
        }
        [HttpPut]
        [Route("api/messages/mark-as-read/{userId}")]
        public async Task<IActionResult> MarkAsRead(string userId)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _context.Messages.Where(m => m.ReceiverId == currentUser && m.SenderId == userId && !m.IsRead)
                .ToList().ForEach(m =>
                {
                    m.IsRead = true;
                    m.ReadAt = DateTime.UtcNow;
                });
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
