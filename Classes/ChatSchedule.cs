using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium.DevTools.V131.Page;

namespace TSI_Monitor.Classes
{
    public class ScheduleTimes
    {
        public DayOfWeek DayOfWeek;
        public List<TimeSpan> Times; // отсчитываем от 00:00 от начала дня.
    }


    public class ChatSchedule
    {
        public Guid Guid { get; private set; }
        public long ChatId { get; private set; }
        public string Description { get; private set; }
        public bool IsEveryWeek { get; private set; }
        public List<ScheduleTimes> ScheduleDays;

        public DateTime CreationDate { get; private set; }

        public delegate string Operation();
        public Operation ExecuteOperation = null;

        public Action ExecuteAction = null;

        //public delegate TResult UniversalDelegate<TResult>();
        //public TResult ExecuteOperation<TResult>(UniversalDelegate<TResult> operation)
        //{
        //    return operation();
        //}

        public ChatSchedule()
        {
            Guid = Guid.Empty;
            ChatId = 1;
            Description = "Created by Entity FM";
            IsEveryWeek = false;
            CreationDate = DateTime.Now;
            ExecuteOperation = null;
            ExecuteAction = null;
            ScheduleDays = new();
        }
        public ChatSchedule(long chatId, string description, bool IsEveryWeek, ScheduleTimes schedule)
        {
            this.Guid = new Guid();
            this.ChatId = chatId;
            this.Description = description;
            this.IsEveryWeek = IsEveryWeek;
            this.CreationDate = DateTime.Now;

            ScheduleDays = new List<ScheduleTimes>();
            ScheduleDays.Add(schedule);
        }

        public ChatSchedule(long chatId, string description, bool IsEveryWeek, List<ScheduleTimes> scheduleList)
        {
            this.Guid = new Guid();
            this.ChatId = chatId;
            this.Description = description;
            this.IsEveryWeek = IsEveryWeek;
            this.CreationDate = DateTime.Now;

            ScheduleDays = new List<ScheduleTimes>();
            ScheduleDays = scheduleList;
        }

        public TimeSpan GetNextExecuteTime()
        {
            // for old schedules return zero
            var difference = DateTime.Now.Subtract(CreationDate).TotalHours;
            if (IsEveryWeek && difference > 24)
            {
                return TimeSpan.Zero;
            }

            TimeSpan nearestTime = TimeSpan.MaxValue;
            var currentDaySchedules = ScheduleDays.Where(x => x.DayOfWeek == DateTime.Now.DayOfWeek);
            foreach (ScheduleTimes sched in currentDaySchedules)
            {
                foreach (TimeSpan timeExec in sched.Times)
                {
                    if (timeExec > DateTime.Now.TimeOfDay && timeExec < nearestTime)
                        nearestTime = timeExec;
                }
            }
            return nearestTime;
        }

        public static List<ScheduleTimes> TimesOnlyWeekends()
        {
            List<ScheduleTimes> template = new List<ScheduleTimes>();
            ScheduleTimes temp = new ScheduleTimes();

            temp.DayOfWeek = DayOfWeek.Friday;
            var times = new List<TimeSpan>();
            times.Add(new TimeSpan(21, 00, 00)); //hour, min, seconds

            temp.Times = times;
            template.Add(temp);

            temp = new ScheduleTimes();

            temp.DayOfWeek = DayOfWeek.Saturday;
            times = new List<TimeSpan>();
            times.Add(new TimeSpan(09, 00, 00));
            times.Add(new TimeSpan(20, 00, 00));

            temp.Times = times;
            template.Add(temp);

            temp = new ScheduleTimes();

            temp.DayOfWeek = DayOfWeek.Sunday;
            times = new List<TimeSpan>();
            times.Add(new TimeSpan(09, 00, 00));
            times.Add(new TimeSpan(20, 00, 00));

            temp.Times = times;
            template.Add(temp);

            return template;
        }

        public static List<ScheduleTimes> TimesForOneDay()
        {
            List<ScheduleTimes> template = new List<ScheduleTimes>();
            ScheduleTimes temp = new ScheduleTimes();

            temp.DayOfWeek = DateTime.Now.DayOfWeek;
            var times = new List<TimeSpan>();

            for (int i = DateTime.Now.Hour; i < 21; i++)
            {
                times.Add(new TimeSpan(i, 00, 00)); //hour, min, seconds
            }
            temp.Times = times;

            template.Add(temp);

            return template;
        }

        public static List<ScheduleTimes> Times12EveryDay()
        {
            List<ScheduleTimes> template = new();
            var workedDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            foreach (var day in workedDays)
            {
                var temp = new ScheduleTimes();
                temp.DayOfWeek = day;

                var times = new List<TimeSpan>();
                for (int i = 0; i < 24; i += 2)
                {
                    times.Add(new TimeSpan(i, 00, 00)); //hour, min, seconds
                }
                temp.Times = times;
                template.Add(temp);
            }

            return template;
        }

        public static List<ScheduleTimes> Times4Test()
        {
            List<ScheduleTimes> template = new List<ScheduleTimes>();
            ScheduleTimes temp = new ScheduleTimes();

            temp.DayOfWeek = DateTime.Now.DayOfWeek;
            var times = new List<TimeSpan>();

            for (int i = DateTime.Now.Hour; i < 21; i++)
            {
                for (int j = 0; j < 60; j += 4)
                    times.Add(new TimeSpan(i, j, 00)); //hour, min, seconds
            }
            temp.Times = times;

            template.Add(temp);

            return template;
        }
    }

    public class ScheduleRepository
    {
        private readonly DBContext _context;

        public ScheduleRepository(DBContext context)
        {
            _context = context;
        }

        public async Task<string> Add(ChatSchedule schedule)
        {
            var sched = await _context.ChatSchedules
                .Where(x => x.ChatId == schedule.ChatId && x.Description == schedule.Description)
                .FirstOrDefaultAsync();
            if (sched != null) return "уже существует";

            _context.ChatSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            Log.Logger.Info($"Создано расписание. Чат={schedule.ChatId}.Расписание={schedule.Description}");
            return "создано";
        }

        public async Task<List<ChatSchedule>> Get(long chatId)
        {
            var scheds = await _context.ChatSchedules
                .Where(x => x.ChatId == chatId )
                .ToListAsync();

            Log.Logger.Info($"Выполнен запрос текущих расписаний для чата: {chatId}. Найдно расписаний: {scheds.Count}");
            return scheds;
        }

        public async Task<List<string>> GetNames(long chatId)
        {
            var scheds = await Get(chatId);
            List<string> names = new ();
            Log.Logger.Info($"Выполнен запрос имен расписаний для чата: {chatId}. Найдно расписаний: {scheds.Count}");

            if (scheds == null)
                return new List<string> { "Расписаний не найдено"};

            foreach (var sched in scheds)
            {
                names.Add(sched.Description);
            }

            return names;
        }

        public async Task<string> DeleteAsync(long chatId)
        {
            if (chatId == 0)
            {
                Log.Logger.Error("Argument was empty in ScheduleRepository.Delete ");
                throw new ArgumentNullException();
            }

            var schedules = await _context.ChatSchedules
                .Where(p => p.ChatId == chatId)
                .ToListAsync();

            _context.ChatSchedules.RemoveRange(schedules);
            await _context.SaveChangesAsync();

            Log.Logger.Info($"Выполнен запрос удаления расписаний для чата: {chatId}. Удалено расписаний: {schedules.Count}");
            return $"Удалено {schedules.Count} расписаний";
        }
    }
}
