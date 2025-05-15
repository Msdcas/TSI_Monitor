using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium.Support.UI;
using System.Data.Entity;

namespace TSI_Monitor.Classes
{
    public class Chat
    {
        public long Id { get; private set; }
        public string Name { get; private set; }
        public bool IsBlock { get; set; }
        public string Description { get; set; } = string.Empty;

        public readonly DateTime CreationTime;

        public Chat()
        {
            Id = -1;
            Name = null;
            IsBlock = true;
            Description = "Created by Entity FM";
            CreationTime = DateTime.Now;
        }
        public Chat(long id, string name = null, bool isBlock = false, string description = null) 
        { 
            this.Id = id;
            this.Name = name;
            this.IsBlock = isBlock;
            this.Description = description;
            this.CreationTime = DateTime.Now;
        }

    }

    public class ChatRepository
    {
        private readonly DBContext _context;

        public ChatRepository(DBContext context)
        {
            _context = context;
        }

        public async Task<bool> Update(long id)
        {
            if (await Contains(id))
            {
                var ent = await Get(id);
                _context.Chat.Update(ent);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> Delete(long id)
        {
            if (await Contains(id))
            {
                var ent = await Get(id);
                _context.Chat.Remove(ent);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> Add(Chat chat)
        {
            if (!await Contains(chat.Id))
            {
                await _context.Chat.AddAsync(chat);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<Chat> Get(long id)
        {
            var chat = await _context.Chat
                .Where(c => c.Id == id)
                .FirstAsync();
            return chat;
        }
        public async Task<bool> Contains(long id)
        {
            var chat = await _context.Chat
                .Where(c => c.Id == id)
                .FirstAsync();

            if (chat == null)
                return false;
            else
                return true;
        }

        public async Task<bool> IsBlock(long id)
        {
            Chat chat = await Get(id);
            if (chat != null)
                if (chat.IsBlock)
                    return true;
                else
                    return false;
            return false;
        }
    }
}
