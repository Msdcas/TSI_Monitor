using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Data;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Data.Sqlite;


Console.WriteLine("Enter 'stop' for stopped the bot and scheduler\n\n");

CancellationTokenSource cancTokenSource = new CancellationTokenSource();
var task = Task.Run(() => SheduleManager.StartCheckAndExecSchedules(cancTokenSource.Token));
var task2 = Task.Run(() => TBot.Start(cancTokenSource.Token));

while (!cancTokenSource.IsCancellationRequested)
{
    var input = Console.ReadLine();
    if (input?.ToLower() == "stop")
    {
        cancTokenSource.Cancel();
        TBot.Stop();
    }
}

// изменить логику работы шедулера. добавить уникальные ИД в список шедулера
//в шедулер добавить метод GetNetxtime и расчитывать минимальное время до запуска, от которого расчитывать период делея в таске

// добавить сохранение расписания при остановке бота
//добавить запрос в консоль о длине немедленной очереди, кол-ве активных расписаний
// если польз удаляет расписание, то удалять его из немедленной очереди
//добавить canceltoken в selenium
//нужно проводить тесты с планировщиком расписаний - тесты проведены, работает почти стабильно

//переделать методы класса менеджера в асинхронные
//обработка ошибок, вывод стека вызовов
// вывод в прод как самостоятельный продукт:
// при первом запуске только админ в белом списке
// у админа есть меню для управления чатами + кнопка для останова бота
// при появлении чата выходит уведомление админу на разрешение учавствовать в чате
// выгрузка в sqlite белого списка чатов и расписания
//логирование истории переписки в sqlite
//брать базовые настройки из файла конфига (токен бота и ид админа)

//проследить цепочку асинхронных вызовов с линии бота и монитора очереди
//добавить кнопку, получить список немедленно
// + выгружать расписание в файл, чтобы при запуске восстановить его
static class SeleniumMonitorTSI
{
    private const string Url = "https://jira.puls.ru/secure/Dashboard.jspa?selectPageId=15003";
    private const string TableCssSelector = "#qrf-table-view-23015 > table";

    public static string GetOpenTSITickets()
    {
        WebDriver driver = null;
        DataTable TicketsDTable = InitializeTable();
        try
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            ChromeDriverService driverService = ChromeDriverService.CreateDefaultService();

            driverService.HideCommandPromptWindow = true;
            chromeOptions.AddArguments("--headless=new"); // comment out for testing
            
            driver = new ChromeDriver(driverService, chromeOptions);


            driver.Navigate().GoToUrl(Url);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            var ipTextBox = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(TableCssSelector)));
            Thread.Sleep(2000);

            IWebElement ticketTable = driver.FindElement(By.CssSelector(TableCssSelector));

            if (ticketTable == null)
                return "Ошибка получения таблицы";

            var ticketRows = ticketTable.FindElements(By.TagName("tr"));

            foreach (var trow in ticketRows.Skip(1))
            {
                var text = trow.Text.Replace("Светашов Евгений Викторович", "РИТ").Replace("Зверев Михаил Дмитриевич", "Админ");
                var param = text.Split('\n');

                if (text == "No issues found.")
                {
                    return "Нет открытых задач TSI";
                }

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
    private const string botToken = "7768609296:AAGJR_9WJv4rJQ8n6W_hHv3zzGLignWNx_A";
    private const long AdminChatId = 6750792041; //351907910
    private static List<long> WhiteChatsId = new() { 6750792041, -969152017, 1112277578 };
    private static List<long> BlackChatsId = new();

    private const string showCurrentChatSchedules = "Показать расписание для этого чата";                //mbut1
    private const string cancelChatSchedules = "Отменить рассылку в этот чат";                           //mbut2
    private const string scheduleEveryHour = "Выгружать сегодня каждый час с 08:00 по 19:00";            //mbut3
    private const string scheduleOnlyWeekends = "Выгружать еженедельно ПТ_21:00, СБ_ВС_09:00,20:00";     //mbut4
    private const string scheduleGetImmediatly = "Получить список открытых TSI немедленно";              //mbut5

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
                InlineKeyboardButton.WithCallbackData(scheduleEveryHour, "mbut3")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(scheduleOnlyWeekends, "mbut4")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(scheduleGetImmediatly, "mbut5")
            }
    });

private static TaskCompletionSource tcs;

    public static async Task Start(CancellationToken cancellation)
    {
        if (tcs is not null)
            throw new InvalidOperationException("Bot is already running.");
        tcs = new();

        _botClient = new TelegramBotClient(botToken);
        _receiverOptions = new ReceiverOptions // Также присваем значение настройкам бота
        {
            AllowedUpdates = new[] // Тут указываем типы получаемых Update`ов, о них подробнее расказано тут https://core.telegram.org/bots/api#update
            {
                UpdateType.CallbackQuery,
                UpdateType.Message, // Сообщения (текст, фото/видео, голосовые/видео сообщения и т.д.)
            },
            // Параметр, отвечающий за обработку сообщений, пришедших за то время, когда ваш бот был оффлайн
            // True - не обрабатывать, False (стоит по умолчанию) - обрабаывать

            //ThrowPendingUpdates = true,
        };


        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cancellation);

        var me = await _botClient.GetMe();
        Console.WriteLine($"Bot {me.FirstName} is stARted", Console.ForegroundColor = ConsoleColor.Green);

        await tcs.Task;
        tcs = null;

        Console.WriteLine($"Bot {me.FirstName} is stOPped", Console.ForegroundColor = ConsoleColor.Red);
    }

    public static void Stop()
    {
        try
        {
            tcs?.SetResult();
        }
        catch
        {
            throw new InvalidOperationException("Bot is not running.");
        }
    }

    public static async Task SendMessage(long chatId, string message)
    {
        var botClient = new TelegramBotClient(botToken);

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

                    if (Contains(BlackChatsId, update.Message.Chat.Id))
                    {
                        return;
                    }

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
        if (!Contains(WhiteChatsId, update.Message.Chat.Id))
        {
            await botClient.SendMessage(update.Message.Chat.Id, "You are not on the white list.\nRequest sent to admin");

            await botClient.SendMessage(AdminChatId,
                $"Новый чат, id={update.Message.From},msg={update.Message.Text}",
                replyMarkup: KeyboardHelper.GetInlineKbr_IsAddNewChat(update.Message.Chat.Id.ToString()));
            
            Console.WriteLine($"Drop msg from chat_id={update.Message.Id.ToString()}");
            return;
        }

        Console.WriteLine($"msg={update.Message.Text}, chat_id={update.Message.Chat.Id}");


        string answ = null;
        ChatSchedule temp;
        switch (message.Text)
        {
            case "/start":
                await botClient.SendMessage(
                    chat.Id, 
                    "Приветсвую, здесь вы можете получать список открытых задач TSI для вашего РК \n Функционал доступен в меню чата",
                    ParseMode.None, 
                    null,
                    replyMarkup: mainInlineKeyboard);
                break;

            case "/mbut1":
                answ = SheduleManager.GetChatSchedules(chat.Id);
                await botClient.SendMessage(chat.Id, answ);
                break;

            //cancelChatSchedules
            case "/mbut2":
                answ = SheduleManager.DeleteChatShedules(chat.Id);
                await botClient.SendMessage(chat.Id, answ);
                break;

            //scheduleChatForPersona
            case "/mbut3":
                temp = new ChatSchedule(chat.Id, scheduleEveryHour, true, ChatSchedule.ScheduleTemplateOneDay());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenTSITickets;
                answ = "Ежечасное расписание " + SheduleManager.AddSchedules(temp);
                await botClient.SendMessage(chat.Id, answ);
                break;

            //scheduleChatForWeekends
            case "/mbut4":
                temp = new ChatSchedule(chat.Id, scheduleOnlyWeekends, true, ChatSchedule.ScheduleTemplateWeekends());
                //temp = new Schedule(chat.Id, "test_schedule", true, Schedule.ScheduleTemplateTest());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenTSITickets;
                answ = "Еженедельное расписание " + SheduleManager.AddSchedules(temp);
                await botClient.SendMessage(chat.Id, answ);
                break;

            //scheduleGetImmediatly
            case "/mbut5":
                await botClient.SendMessage(chat.Id, SeleniumMonitorTSI.GetOpenTSITickets());
                //await botClient.SendMessage(chat.Id, answ);
                break;

            default:
                await botClient.SendMessage(chat.Id, "Для этой команды нет обработчика.");
                break;
        }
    }

    private static async Task HandleCallbackQuery(ITelegramBotClient botClient, Update update)
    {
        var callback = update.CallbackQuery;
        var user = callback.From;
        var chat = callback.Message.Chat;

        //Console.WriteLine($"str={callback.Data}, chat_id={chat.Id}");

        if (user.Id == AdminChatId)
        {
            long newChatId;
            if (long.TryParse(update.CallbackQuery.Data.Replace("addChat_", ""), out newChatId))
            {
                if (callback.Data.StartsWith("addChat_"))
                {
                    WhiteChatsId.Add(newChatId);

                    await botClient.SendMessage(newChatId, $"You have been added to the WHITE list");
                    //Console.WriteLine($"add new chat {newChatId}");
                    //await botClient.AnswerCallbackQuery(callback.Id, $"Чат {newChatId} добавлен в белый список");
                    await botClient.SendMessage(AdminChatId, $"Чат {newChatId} добавлен в белый список");
                    await botClient.DeleteMessage(AdminChatId, update.CallbackQuery.Message.Id);
                }

                if (callback.Data.StartsWith("ignChat_"))
                {
                    BlackChatsId.Add(newChatId);
                    await botClient.DeleteMessage(AdminChatId, update.CallbackQuery.Message.Id);
                    await botClient.SendMessage(newChatId, $"You have been added to the BLACK list");
                }

                return;
            }
        }

        string answ = null;
        ChatSchedule temp;
        switch (callback.Data)
        {
            //mainInlineKeyboard operations and answers

            //showCurrentChatSchedules
            case "mbut1":
                answ = SheduleManager.GetChatSchedules(chat.Id);
                await botClient.AnswerCallbackQuery(callback.Id, answ);
                break;

            //cancelChatSchedules
            case "mbut2":
                answ = SheduleManager.DeleteChatShedules(chat.Id);
                await botClient.AnswerCallbackQuery(callback.Id, answ);
                break;

            //scheduleChatForPersona
            case "mbut3":
                temp = new ChatSchedule(chat.Id, scheduleEveryHour, true, ChatSchedule.ScheduleTemplateOneDay());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenTSITickets;
                answ = "Ежечасное расписание " + SheduleManager.AddSchedules(temp);
                await botClient.AnswerCallbackQuery(callback.Id, answ);
                break;

            //scheduleChatForWeekends
            case "mbut4":
                temp = new ChatSchedule(chat.Id, scheduleOnlyWeekends, true, ChatSchedule.ScheduleTemplateWeekends());
                //temp = new Schedule(chat.Id, "test_schedule", true, Schedule.ScheduleTemplateTest());
                temp.ExecuteOperation = SeleniumMonitorTSI.GetOpenTSITickets;
                answ = "Еженедельное расписание " + SheduleManager.AddSchedules(temp);
                await botClient.AnswerCallbackQuery(callback.Id, answ);
                break;

            //scheduleGetImmediatly
            case "mbut5":
                await botClient.AnswerCallbackQuery(callback.Id, SeleniumMonitorTSI.GetOpenTSITickets());  //Telegram Bot API error 400: Bad Request: MESSAGE_TOO_LONG
                //await botClient.SendMessage(chat.Id, answ);
                break;

            default:
                break;
        }
        //Console.WriteLine(answ);
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

    private static bool Contains<T>(List<T> list,T obj)
    {
        if (list.Count == 0) return false;

        if (list.Contains(obj))
            return true;
        else return false;
    }

}

class ScheduleTimes
{
    public DayOfWeek DayOfWeek;
    public List<TimeSpan> Times; // отсчитываем от 00:00 от начала дня.
}

class ChatSchedule
{
    public Guid Guid {  get; private set; }
    public long ChatId { get; private set; }
    public string Description { get; private set; }
    public bool IsEveryWeek { get; private set; }
    public List<ScheduleTimes> ScheduleDays;

    public DateTime CreationDate {  get; private set; }    
    
    public delegate string Operation();
    public Operation ExecuteOperation;

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

        // далее возникает конфликт т.к. расписаний может быть несколько, но это можно опустить
        // т.к. пока мы имеем только 1 делегат и не важно по какому расписанию он будет вызван
        // проблема появится когда будет список делегатов, но в данном случае проще создать новый экземпляр этого класса

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

    public static List<ScheduleTimes> ScheduleTemplateWeekends()
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
        temp.Times = times;

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
            for (int j = 0; j < 60; j += 4)
                times.Add(new TimeSpan(i, j, 00)); //hour, min, seconds
        }
        temp.Times = times;

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
    public static Queue<ChatSchedule> SchedulesToImmediatlyOperate = new Queue<ChatSchedule>();
    private static List<ChatSchedule> _Schedules = LoadSchedules();

    public static string AddSchedules(ChatSchedule schedule)
    {
        var isExist = _Schedules.Where(x => x.ChatId == schedule.ChatId && x.Description == schedule.Description).Count() != 0;
        if (isExist) return "уже существует";

        _Schedules.Add(schedule);
        return "создано";
    }

    public static string GetChatSchedules(long chatId)
    {
        string answ = "Расписаний не найдено";
        //Console.WriteLine($"Запрос расписаний для chatId: {chatId}");

        List<ChatSchedule> matchedSchedules;
        lock (_lockSched)
            matchedSchedules = _Schedules.Where(x => x.ChatId == chatId).ToList();

        //Console.WriteLine($"Найдено расписаний: {matchedSchedules.Count}");

        if (matchedSchedules.Any())
            answ = string.Join("\n", matchedSchedules.Select(s => s.Description));

        return answ;
    }

    public static string DeleteChatShedules(long chatId)
    {
        int count = 0;
        List<ChatSchedule> schedulesToRemove = new List<ChatSchedule>();

        lock (_lockSched)
        {
            foreach (ChatSchedule sched in _Schedules)
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
    }

    private static void PushToOperateQueue()
    {
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
                            if (difference < 80 && difference > 0)
                            {
                                SchedulesToImmediatlyOperate.Enqueue(sched);
                                Console.WriteLine(
                                    $"\t{DateTime.Now.ToString("hh:mm")}> Задача помещена в очередь исполнения. Чат={sched.ChatId}, время={dayTimes}",
                                    Console.ForegroundColor = ConsoleColor.Yellow);
                            }
                        }       
                }
            }
    }

    public static async Task StartCheckAndExecSchedules(CancellationToken cancellation)
    {
        Console.WriteLine("Schedule monitor is stARted", Console.ForegroundColor = ConsoleColor.Green);
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                while (SchedulesToImmediatlyOperate.Any())
                {
                    var sched = SchedulesToImmediatlyOperate.Dequeue();
                    Console.WriteLine(
                                    $"\t{DateTime.Now.ToString("hh:mm")}> Исполнение задачи из очереди. Чат={sched.ChatId}",
                                    Console.ForegroundColor = ConsoleColor.DarkYellow);

                    string answ = sched.ExecuteOperation.Invoke();
                    await TBot.SendMessage(sched.ChatId, answ);
                }

                //if (_Schedules.Count != 0) SaveSchedules(_Schedules); //Access to the path 'D:\Projects\VS\TSI_Monitor\TSI_Monitor\bin\Release\net8.0' is denied.
                PushToOperateQueue();   //косяк, тут ли его вызывать? или опустить этот метод и работать только с PushToOperateQueue
                CheckAndRemoveOldSchedules();

                await Task.Delay(60 * 1000);
            }
            Console.WriteLine("Schedule monitor is stOPped", Console.ForegroundColor = ConsoleColor.Red);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public static void SaveSchedules(List<ChatSchedule> schedules)
    {
        string json;

        lock (_lockSched)
            json = JsonSerializer.Serialize(schedules);
        File.WriteAllText(_BasicFolder, json);
    }

    private static List<ChatSchedule> LoadSchedules()
    {
        if (!File.Exists(_ScheduleSavedFullFilePath))
        {
            return new List<ChatSchedule>();
        }

        var json = File.ReadAllText(_ScheduleSavedFullFilePath);
        var scheduleList = JsonSerializer.Deserialize<List<ChatSchedule>>(json);

        if (scheduleList != null && scheduleList.Count > 0)
            Console.WriteLine($"Восстановлено {scheduleList.Count} расписаний");

        return new List<ChatSchedule>(scheduleList);
    }

}

//public class DatabaseManager
//{
//    private string connectionString;

//    public DatabaseManager(string dbFilePath)
//    {
//        connectionString = $"Data Source={dbFilePath};Version=3;";
//        CreateDatabaseIfNotExists();
//    }

//    private void CreateDatabaseIfNotExists()
//    {
//        if (!System.IO.File.Exists(connectionString.Split('=')[1].Split(';')[0]))
//        {
//            SQLiteConnection.CreateFile(connectionString.Split('=')[1].Split(';')[0]);
//            using (var connection = new SQLiteConnection(connectionString))
//            {
//                connection.Open();
//                string createTableQuery = "CREATE TABLE IF NOT EXISTS Classes (Id INTEGER PRIMARY KEY, Name TEXT)";
//                using (var command = new SQLiteCommand(createTableQuery, connection))
//                {
//                    command.ExecuteNonQuery();
//                }
//            }
//        }
//    }
//}

class KeyboardHelper
{
    public static InlineKeyboardMarkup GetInlineKbr_IsAddNewChat(string chatId)
    {
        InlineKeyboardMarkup mainInlineKeyboard = new InlineKeyboardMarkup(
        new List<InlineKeyboardButton[]>()
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("В белый список", $"addChat_{chatId}"),
                InlineKeyboardButton.WithCallbackData("В игнор", $"ignChat_{chatId}")
            }
        });

        return mainInlineKeyboard;
    }


}

static class Log
{

}




