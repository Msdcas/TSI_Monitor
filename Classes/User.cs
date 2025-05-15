using Telegram.Bot.Types;

namespace TSI_Monitor.Classes
{
    public class User
    {
        private readonly DBContext _context;
        public long Id { get; private set; }
        public string FirstName { get; set; }
        public string UserName { get; set; }
        public bool IsBlock { get; set; }

        public string Description { get; set; }

        public readonly DateTime CreationDate;

        public DateTime LastMessageTime { get; set; }

        public User()
        {
            Id = 1;
            FirstName = string.Empty;
            UserName = string.Empty;
            IsBlock = true;
            CreationDate = DateTime.Now;
            Description = "Created by Entity FM";
            LastMessageTime = DateTime.MinValue;
        }
        public User(long Id, string firstName = null, string userName = null, bool isBlock = false)
        {
            this.Id = Id;
            this.FirstName = firstName;
            this.UserName = userName;
            this.IsBlock = isBlock;
            CreationDate = DateTime.Now;
        }

    }


    public class UserRepository
    {
        private readonly DBContext _context;

        public UserRepository(DBContext context)
        {
            _context = context;
        }

        public async Task<bool> Add(User user)
        {
            if (!await Contains(user.Id))
            {
                await _context.User.AddAsync(user);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<User> Get(long id)
        {
            Log.Logger.Info($"User.Get request id={id}");
            var result = _context.User.Find(id);
            if (result == null)
                return null;
            else
                return result;

            //if (user != null)
            //    return true;
            //else
            //    return false;
        }

        public async Task Update(User user)
        {
            //_context.Entry(user).State = EntityState.Modified;
            _context.User.Update(user);
            _context.SaveChanges();
        }

        public async Task<bool> Delete(User user)
        {
            if (await Contains(user.Id))
            {
                _context.User.Remove(user);
                _context.SaveChanges();
                return true;
            }
            return false;
        }

        public async Task<bool> Contains(long id)
        {
            User user = await Get(id);
            if (user != null)
                return true;
            else
                return false;
        }

        public async Task<bool> IsBlock(long id)
        {
            User user = await Get(id);
            if (user != null)
                if (user.IsBlock)
                    return true;
                else
                    return false;
            return false;
        }
    }


}

