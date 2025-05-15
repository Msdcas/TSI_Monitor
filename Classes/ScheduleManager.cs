namespace TSI_Monitor.Classes
{
    static class SheduleManager
    {
        private static readonly object _lockSched = new object();
        private static Queue<ChatSchedule> _SchedulesToImmediatlyOperate = new Queue<ChatSchedule>();
        private static List<ChatSchedule> _Schedules = new(); // LoadSchedules();

        public delegate Task MessageSender(long chatId,  string message);
        public static MessageSender SendMessage;

        private static void CheckAndRemoveOldSchedules()
        {
            var personalSchedules = _Schedules.Where(x => x.IsEveryWeek == false).ToList();
            List<ChatSchedule> schedulesToRemove = new List<ChatSchedule>();

            lock (_lockSched)
            {
                foreach (ChatSchedule sched in personalSchedules)
                {
                    if (DateTime.Now - sched.CreationDate > TimeSpan.FromDays(1))
                        schedulesToRemove.Add(sched);
                }

                foreach (var sched in schedulesToRemove)
                {
                    _Schedules.Remove(sched);
                }
            }
            Log.Logger.Debug($"Выполнение системного события. Удалено старых расписаний: {schedulesToRemove.Count}");
        }

        private static void PushToQueueOperate(int secondsDiap)
        {
            //List<ChatSchedule> scheds = new ();       //error: StartCheckAndExecSchedulesSystem.ArgumentOutOfRangeException: The added or subtracted value results in an un-representable DateTime. (Parameter 't')
            //lock (_lockSched)
            //    scheds = _Schedules
            //        .Where(schedule => schedule.ScheduleDays
            //            .Any(day => day.DayOfWeek == DateTime.Now.DayOfWeek))
            //        .ToList();

            //foreach (ChatSchedule sched in scheds)
            //{
            //    var timeToExec = DateTime.Today + sched.GetNextExecuteTime();
            //    var difference = DateTime.Now.Subtract(timeToExec).TotalSeconds;
            //    if (difference < secondDiap && difference > 0)
            //    {
            //        _SchedulesToImmediatlyOperate.Enqueue(sched);
            //        Log.Logger.Debug($"Задача помещена в очередь исполнения. Чат={sched.ChatId}, время={timeToExec}");
            //    }
            //}

            lock (_lockSched)
                foreach (ChatSchedule sched in _Schedules)
                {
                    var schedDays = sched.ScheduleDays;
                    foreach (ScheduleTimes day in schedDays)
                    {
                        if (day.DayOfWeek == DateTime.Now.DayOfWeek)
                            foreach (TimeSpan dayTimes in day.Times)
                            {
                                var timeToExec = DateTime.Today + dayTimes;
                                var difference = DateTime.Now.Subtract(timeToExec).TotalSeconds;
                                if (difference < secondsDiap && difference > 0)
                                {
                                    _SchedulesToImmediatlyOperate.Enqueue(sched);
                                    Log.Logger.Debug($"Задача помещена в очередь исполнения. Чат={sched.ChatId}, время={dayTimes}");
                                }
                            }
                    }
                }
        }

        public static async Task StartCheckAndExecSchedules(CancellationToken cancellation)
        {
            Log.Logger.Info("Schedule monitor is stARted", new { Color = "green" });

            //var temp = new ChatSchedule(0, "system_save_schedules", true, ChatSchedule.Schedule12TimesEveryDay());
            //temp.ExecuteAction = SaveSchedules;
            //AddSchedules(temp);

            //temp = new ChatSchedule(0, "system_delete_old_schedules", true, ChatSchedule.Schedule12TimesEveryDay());
            //temp.ExecuteAction = CheckAndRemoveOldSchedules;
            //AddSchedules(temp);

            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    string answ = null;
                    while (_SchedulesToImmediatlyOperate.Any())
                    {
                        var sched = _SchedulesToImmediatlyOperate.Dequeue();
                        Log.Logger.Debug($"Выполнение задачи расписания для чата={sched.ChatId}");

                        if (answ == null)
                            answ = sched.ExecuteOperation?.Invoke();        //нужно вызывать как begininvoke с методом обработки результата как параметром
                        //await TBot.SendMessage(sched.ChatId, answ);
                        await SendMessage(sched.ChatId, answ);

                        Log.Logger.Debug($"Выполнена задача расписания для чата={sched.ChatId}");
                    }
                    PushToQueueOperate(80); //проверяем в минус 80 сек к текущему времени


                    //CheckAndRemoveOldSchedules();
                    //SaveSchedules();
                    await Task.Delay(60 * 1000); // спим 60 сек
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Ошибка в StartCheckAndExecSchedules" + ex.ToString());

            }

            //await TBot.SendMessage(0, "Schedule monitor is stOPped");
            Log.Logger.Info("Schedule monitor is stOPped", new { Color = "red" });

        }

    }
}
