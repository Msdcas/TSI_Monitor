using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools.V131.Debugger;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;



CancellationTokenSource cancTokenSource = new CancellationTokenSource();

//Task schedulerWatcherTask = SheduleManager.StartCheckAndExecSchedules();
//schedulerWatcherTask.RunSynchronously();
await TBot.Main();


//переделать методы класса менеджера в асинхронные
//обработка ошибок, вывод стека вызовов
// вывод в прод как самостоятельный продукт:
    // при первом запуске только админ в белом списке
    // у админа есть меню для управления чатами + кнопка для останова бота
    // при появлении чата выходит уведомление админу на разрешение учавствовать в чате
    // выгрузка в sqlite белого списка чатов и расписания
    //брать базовые настройки из файла конфига (токен бота и ид админа)

//проследить цепочку асинхронных вызовов с линии бота и монитора очереди
//добавить кнопку, получить список немедленно
// + выгружать расписание в файл, чтобы при запуске восстановить его
static class SeleniumMonitorTSI
{
    private const string Url = "https://jira.puls.ru/secure/Dashboard.jspa?selectPageId=15003";
    private const string TableCssSelector = "#qrf-table-view-23015 > table";

    public static string GetOpenedTickets()
    {
        WebDriver driver = null;
        DataTable TicketsDTable = InitializeTable();
        try
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--headless=new"); // comment out for testing
            driver = new ChromeDriver(chromeOptions);

            driver.Navigate().GoToUrl(Url);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            var ipTextBox = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(TableCssSelector)));
            Thread.Sleep(2000);

            IWebElement ticketTable = driver.FindElement(By.CssSelector(TableCssSelector));

            if (ticketTable == null)
                return "Ошибка получения таблицы";

            var ticketRows = ticketTable.FindElements(By.TagName("tr"));

            if (ticketRows.Count == 0)
                return "Нет открытых задач TSI";

            foreach (var trow in ticketRows.Skip(1))
            {
                var text = trow.Text.Replace("Светашов Евгений Викторович", "РИТ").Replace("Зверев Михаил Дмитриевич", "Админ");
                var param = text.Split('\n');

                DataRow row = TicketsDTable.NewRow();
                row[0] = param[0]; // code
                row[1] = param[1]; // theme
                row[2] = param[2]; // status + assigner
                row[3] = param[3]; // timeout
                row[4] = param[4]; // creator

                TicketsDTable.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
        finally
        {
            driver.Close();
            driver.Quit();
        }
        return ConvertToString(TicketsDTable);
    }

    private static DataTable InitializeTable()
    {
        DataTable dt = new DataTable();

        DataColumn column1 = new DataColumn("Код", typeof(string));
        DataColumn column2 = new DataColumn("Тема", typeof(string));
        DataColumn column3 = new DataColumn("Статус + Исполнитель", typeof(string));
        DataColumn column4 = new DataColumn("Время до решения", typeof(string));
        DataColumn column5 = new DataColumn("Автор", typeof(string));

        dt.Columns.Add(column1);
        dt.Columns.Add(column2);
        dt.Columns.Add(column3);
        dt.Columns.Add(column4);
        dt.Columns.Add(column5);

        return dt;
    }

    private static DataTable MsgToDataRow(DataTable dataTable, string msg)
    {
        DataRow row = dataTable.NewRow();
        row[0] = msg;
        dataTable.Rows.Add(row);

        return dataTable;
    }

    private static string ConvertToString(DataTable dataTable)
    {
        string result = "";
        foreach (DataRow row in dataTable.Rows)
        {
            foreach (var item in row.ItemArray)
            {
                result += item.ToString() + "|"; // Используем табуляцию для разделения
            }
            result += "\n\n"; // Переход на новую строку
        }
        return result;
    }
}

class TBot
{
    private const string token = "7768609296:AAGJR_9WJv4rJQ8n6W_hHv3zzGLignWNx_A";
    private const long AdminChatId = 6750792041; //351907910
    private static List<long> WhiteChatsId = new() { 6750792041, - 969152017, 1112277578 };

    private const string showCurrentChatSchedules = "Показать расписание для этого чата";               //mbut1
    private const string cancelChatSchedules = "Отменить рассылку в этот чат";                          //mbut2
    private const string scheduleChatForPersona = "Выгружать сегодня каждый час с 08:00 по 19:00";      //mbut3
    private const string scheduleChatForWeekends = "Выгружать еженедельно ПТ_21:00, СБ_ВС_09:00,20:00"; //mbut4

    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;

    static InlineKeyboardMarkup mainInlineKeyboard = new InlineKeyboardMarkup(
    new List<InlineKeyboardButton[]>()
    {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(showCurrentChatSchedules, "mbut1")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(cancelChatSchedules, "mbut2")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(scheduleChatForPersona, "mbut3")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(scheduleChatForWeekends, "mbut4")
            }
    });

    public static async Task Main()
    {
        _botClient = new TelegramBotClient(token);
        _receiverOptions = new ReceiverOptions // Также присваем значение настройкам бота
        {
            AllowedUpdates = new[] // Тут указываем типы получаемых Update`ов, о них подробнее расказано тут https://core.telegram.org/bots/api#update
            {
                UpdateType.CallbackQuery,
                UpdateType.Message, // Сообщения (текст, фото/видео, голосовые/видео сообщения и т.д.)
            }
            // Параметр, отвечающий за обработку сообщений, пришедших за то время, когда ваш бот был оффлайн
            // True - не обрабатывать, False (стоит по умолчанию) - обрабаывать

            //ThrowPendingUpdates = true,
        };
        using var cancellation = new CancellationTokenSource();
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cancellation.Token);

        var me = await _botClient.GetMe();
        Console.WriteLine($"Бот {me.FirstName} запущен!");

        await Task.Delay(-1); // Устанавливаем бесконечную задержку, чтобы наш бот работал постоянно
    }

    public static async Task SendMessage(long chatId, string message)
    {
        var botClient = new TelegramBotClient(token);

        try
        {
            var result = await botClient.SendTextMessageAsync(chatId, message);

            Console.WriteLine($"Сообщение отправлено: {result.Text}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
        }
    }

    private static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    switch (update.Message.Type)
                    {
                        case MessageType.Text:
                            await HandleTextMessage(botClient, update, update.Message, update.Message.Chat);
                            break;
                    }
                    break;

                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery != null)
                        await HandleCallbackQuery(botClient, update);
                    break;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static async Task HandleTextMessage(ITelegramBotClient botClient, Update update, Message message, Chat chat)
    {
        if (!IsChatIdInWhiteList(update.Message.Chat.Id))
        {
            await botClient.SendMessage(update.Message.Chat.Id, "You are not on the white list");
            await botClient.SendMessage(AdminChatId, $"Новый чат, id={update.Message.From}, msg={update.Message.Text}");
            //Console.WriteLine($"Drop msg from chat_id={update.Message.Id.ToString()}");
            return;
        }
        Console.WriteLine($"msg={update.Message.Text}, chat_id={update.Message.Chat.Id}");

        switch (message.Text)
        {
            case "/start":
                await botClient.SendMessage(
                    chat.Id, 
                    "Приветсвую, здесь вы можете получать список открытых задач TSI для вашего РК",
                    ParseMode.None, 
                    null,
                    replyMarkup: mainInlineKeyboard);
                break;

            default:
                await botClient.SendMessage(chat.Id, "Для этой команды нет обработчика.\nВы можете оставить предложение о функционале в меню Сервис");
                break;
        }
    }

    private static async Task HandleCallbackQuery(ITelegramBotClient botClient, Update update)
    {
        var callback = update.CallbackQuery;
        //var user = callback.From;
        var chat = callback.Message.Chat;

        Console.WriteLine($"str={callback.Data}, chat_id={chat.Id}");

        string answ = null;
        Schedule temp;
        switch (callback.Data)
        {
            //mainInlineKeyboard operations and answers

            //showCurrentChatSchedules
            case "mbut1":
                answ = SheduleManager.GetChatSchedules(chat.Id);
                await botClient.AnswerCallbackQuery(callback.Id, answ, cacheTime: 1500);
                break;

            //cancelChatSchedules
            case "mbut2":
                answ = SheduleManager.DeleteChatShedules(chat.Id);
                await botClient.AnswerCallbackQuery(callback.Id, answ, cacheTime: 1500);
                break;

            //scheduleChatForWeekends
            case "mbut3":
                temp = new Schedule(chat.Id, scheduleChatForWeekends, true, Schedule.ScheduleTemplateWeekends());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenedTickets;
                answ = SheduleManager.AddSchedules(temp);
                await botClient.AnswerCallbackQuery(callback.Id, answ, cacheTime: 1500);
                break;

            //scheduleChatForPersona
            case "mbut4":
                temp = new Schedule(chat.Id, scheduleChatForPersona, true, Schedule.ScheduleTemplateOneDay());
                temp = new Schedule(chat.Id, scheduleChatForPersona, true, Schedule.ScheduleTemplateTest());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenedTickets;
                answ = SheduleManager.AddSchedules(temp);
                await botClient.AnswerCallbackQuery(callback.Id, answ, cacheTime: 1500);
                break;

            default:
                break;
        }
        Console.WriteLine(answ);
    }

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        // Тут создадим переменную, в которую поместим код ошибки и её сообщение 
        var ErrorMessage = error switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private static bool IsChatIdInWhiteList(long chatId)
    {
        if (WhiteChatsId.Count == 0) return false;

        if (WhiteChatsId.Contains(chatId))
            return true;
        else return false;
    }

}

class ScheduleTimes
{
    public DayOfWeek DayOfWeek;
    public List<TimeSpan> Times; // отсчитываем от 00:00
}

class Schedule
{
    public long ChatId;
    public string Description;
    public bool IsEveryWeek;
    public DateTime CreationDate {  get; private set; }    
    public List<ScheduleTimes> ScheduleDays;

    public delegate string Operation();
    public Operation ExecuteOperation;

    public Schedule(long chatId, string description, bool IsEveryWeek, ScheduleTimes schedule)
    {
        this.ChatId = chatId;
        this.Description = description;
        this.IsEveryWeek = IsEveryWeek;
        this.CreationDate = DateTime.Now;

        ScheduleDays = new List<ScheduleTimes>();
        ScheduleDays.Add(schedule);
    }

    public Schedule(long chatId, string description, bool IsEveryWeek, List<ScheduleTimes> scheduleList)
    {
        this.ChatId = chatId;
        this.Description = description;
        this.IsEveryWeek = IsEveryWeek;
        this.CreationDate = DateTime.Now;

        ScheduleDays = new List<ScheduleTimes>();
        ScheduleDays = scheduleList;
    }

    public static List<ScheduleTimes> ScheduleTemplateWeekends()
    {
        List<ScheduleTimes> template = new List<ScheduleTimes>();
        ScheduleTimes temp = new ScheduleTimes();

        temp.DayOfWeek = DayOfWeek.Friday;
        var times = new List<TimeSpan>();
        times.Add(new TimeSpan(21, 00, 00)); //hour, min, seconds

        template.Add(temp);
        temp = new ScheduleTimes();

        temp.DayOfWeek = DayOfWeek.Saturday;
        times = new List<TimeSpan>();
        times.Add(new TimeSpan(09, 00, 00));
        times.Add(new TimeSpan(20, 00, 00));

        template.Add(temp);
        temp = new ScheduleTimes();

        temp.DayOfWeek = DayOfWeek.Sunday;
        times = new List<TimeSpan>();
        times.Add(new TimeSpan(09, 00, 00));
        times.Add(new TimeSpan(20, 00, 00));

        return template;
    }

    public static List<ScheduleTimes> ScheduleTemplateOneDay()
    {
        List<ScheduleTimes> template = new List<ScheduleTimes>();
        ScheduleTimes temp = new ScheduleTimes();

        temp.DayOfWeek = DateTime.Now.DayOfWeek;
        var times = new List<TimeSpan>();

        for (int i = DateTime.Now.Hour; i < 21; i++)
        {
            times.Add(new TimeSpan(i, 00, 00)); //hour, min, seconds
        }

        template.Add(temp);

        return template;
    }

    public static List<ScheduleTimes> ScheduleTemplateTest()
    {
        List<ScheduleTimes> template = new List<ScheduleTimes>();
        ScheduleTimes temp = new ScheduleTimes();

        temp.DayOfWeek = DateTime.Now.DayOfWeek;
        var times = new List<TimeSpan>();

        for (int i = DateTime.Now.Hour; i < 21; i++)
        {
            for (int j = 0; j < 60; j+=5)
                times.Add(new TimeSpan(i, j, 00)); //hour, min, seconds
        }

        template.Add(temp);

        return template;
    }
}

static class SheduleManager
{
    private static readonly string _BasicFolder = Environment.CurrentDirectory;
    private static readonly string _ScheduleSavedFileName = "WorkSchedulesList.json";
    private static readonly string _ScheduleSavedFullFilePath = Path.Combine(_BasicFolder, _ScheduleSavedFileName);

    private static readonly object _lockSched = new object();
    public static Queue<Schedule> SchedulesToImmediatlyOperate;
    private static List<Schedule> _Schedules = LoadSchedules();

    public static string AddSchedules(Schedule schedule)
    {
        var isExist = _Schedules.Where(x => x.ChatId == schedule.ChatId && x.Description == schedule.Description).Count() != 0;
        if (isExist) return "Такое распирасание для этого чата уже существует";

        _Schedules.Add(schedule);
        return "Распирасание создано";
    }

    public static string GetChatSchedules(long chatId)
    {
        string answ = "Расписаний не найдено";
        Console.WriteLine($"Запрос расписаний для chatId: {chatId}");

        List<Schedule> matchedSchedules;
        lock (_lockSched)
            matchedSchedules = _Schedules.Where(x => x.ChatId == chatId).ToList();

        Console.WriteLine($"Найдено расписаний: {matchedSchedules.Count}");

        if (matchedSchedules.Any())
            answ = string.Join("\n", matchedSchedules.Select(s => s.Description));

        return answ;
    }

    public static string DeleteChatShedules(long chatId)
    {
        int count = 0;
        List<Schedule> schedulesToRemove = new List<Schedule>();

        lock (_lockSched)
        {
            foreach (Schedule sched in _Schedules)
            {
                if (sched.ChatId == chatId)
                {
                    schedulesToRemove.Add(sched);
                    count++;
                }
            }

            foreach (var sched in schedulesToRemove)
            {
                _Schedules.Remove(sched);
            }
        }
        return $"Удалено {count} расписаний";
    }


    public static void CheckAndRemoveOldSchedules()
    {
        var personalSchedules = _Schedules.Where(x => x.IsEveryWeek == false).ToList();
        List<Schedule> schedulesToRemove = new List<Schedule>();

        lock (_lockSched)
        {
            foreach (Schedule sched in personalSchedules)
            {
                if (DateTime.Now - sched.CreationDate > TimeSpan.FromDays(1))
                    schedulesToRemove.Add(sched);
            }

            foreach (var sched in schedulesToRemove)
            {
                _Schedules.Remove(sched);
            }
        }
    }

    private static void PushToOperateQueue()
    {
        lock (_lockSched)
            foreach (Schedule sched in _Schedules)
            {
                var schedDays = sched.ScheduleDays;
                foreach (ScheduleTimes day in schedDays)
                {
                    if (day.DayOfWeek == DateTime.Now.DayOfWeek)
                        foreach (TimeSpan dayTimes in day.Times)
                        {
                            var timeToExec = DateTime.Today + dayTimes;
                            if (timeToExec - DateTime.Now < TimeSpan.FromSeconds(90))
                                SchedulesToImmediatlyOperate.Enqueue(sched);
                        }       
                }
            }
    }

    public static async Task StartCheckAndExecSchedules()
    {
        while (true)
        {
            while(SchedulesToImmediatlyOperate.Count > 0)
            {
                var sched = SchedulesToImmediatlyOperate.Dequeue();

                string answ = sched.ExecuteOperation.Invoke();
                await TBot.SendMessage(sched.ChatId, answ);
            }

            SaveSchedules(_Schedules);
            await Task.Delay(60 * 1000);
        }
    }

    public static void SaveSchedules(List<Schedule> schedules)
    {
        string json;

        lock (_lockSched)
            json = JsonSerializer.Serialize(schedules);
        File.WriteAllText(_BasicFolder, json);
    }

    private static List<Schedule> LoadSchedules()
    {
        if (!File.Exists(_ScheduleSavedFullFilePath))
        {
            return new List<Schedule>();
        }

        var json = File.ReadAllText(_ScheduleSavedFullFilePath);
        var scheduleList = JsonSerializer.Deserialize<List<Schedule>>(json);

        if (scheduleList != null && scheduleList.Count > 0)
            Console.WriteLine($"Восстановлено {scheduleList.Count} расписаний");

        return new List<Schedule>(scheduleList);
    }

}

static class Log
{

}




