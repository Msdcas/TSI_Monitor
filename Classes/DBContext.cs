using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

namespace TSI_Monitor.Classes
{
    public class DBContext : DbContext
    {
        private string _ConnectionString;

        public DbSet<User> User { get; set; }
        public DbSet<Chat> Chat { get; set; }
        public DbSet<ChatSchedule> ChatSchedules { get; set; }

        public void DatabaseManager(string dbFilePath)
        {
            SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = dbFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Password = ""
            };

            _ConnectionString = connectionStringBuilder.ConnectionString;
            if (! File.Exists(dbFilePath))
                CreateSqliteDBIfNotExists();
            else
            {
                SqliteConnection sqliteConnection = new SqliteConnection(_ConnectionString);
                sqliteConnection.Open(); // mode by default = ReadWriteCreate
            }

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .OwnsOne(x => x.User);

            modelBuilder.Entity<Users>()
                .Property(c => c.UserID)
                .ValueGeneratedNever();

            modelBuilder.Entity<Currency>()
                .HasKey(k => k.UserID);

        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_ConnectionString);
        }

        private void CreateSqliteDBIfNotExists()
        {
            try
            {
                string dbPath = _ConnectionString.Split('=')[1].Split(';')[0];

                //SqliteConnection.CreateFile(dbPath);
                SqliteConnection sqliteConnection = new SqliteConnection(_ConnectionString);
                sqliteConnection.Open(); // mode by default = ReadWriteCreate
                    
                // Использование блока using для автоматического закрытия соединения
                using (var connection = new SqliteConnection(_ConnectionString))
                {
                    connection.Open();
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS Classes (Id INTEGER PRIMARY KEY, Name TEXT)";
                    using (var command = new SqliteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.ToString());
            }
        }


    }

}
