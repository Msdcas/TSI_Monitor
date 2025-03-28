using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.Modules.Input;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Data;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;



CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


await TBot.Main();




//проследить цепочку асинхронных вызовов с линии бота и монитора очереди
//добавить кнопку, получить список немедленно
//выгружать расписание в файл, чтобы при запуске восстановить его
static class SeleniumMonitorTSI
{
    private const string Url = "https://jira.puls.ru/secure/Dashboard.jspa?selectPageId=15003";
    private const string TableCssSelector = "#qrf-table-view-23015 > table";

    public static string GetOpenedTickets()
    {
        ChromeOptions chromeOptions = new ChromeOptions();
        chromeOptions.AddArguments("--headless=new"); // comment out for testing
        WebDriver driver = new ChromeDriver(chromeOptions);

        driver.Navigate().GoToUrl(Url);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
        var ipTextBox = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(TableCssSelector)));
        Thread.Sleep(2000);

        IWebElement ticketTable = driver.FindElement(By.CssSelector(TableCssSelector));
        DataTable TicketsDTable = InitializeTable();

        if (ticketTable == null) //return MsgToDataRow(TicketsDTable, "Ошибка получения таблицы");
            return "Ошибка получения таблицы";

        var ticketRows = ticketTable.FindElements(By.TagName("tr"));

        if (ticketRows.Count == 0) //return MsgToDataRow(TicketsDTable, "Таблица получена, но была пуста");
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

        driver.Close();
        driver.Quit();

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
    private const string AdminChatId = "6750792041";
    private static List<string> WhiteChatsId = new List<string> { "6750792041", "-969152017", "1112277578" };

    private const string showCurrentChatSchedules = "Показать текущее расписание";
    private const string cancelChatSchedules = "Отменить рассылку в этот чат";
    private const string scheduleChatForPersona = "Сегодня каждый час с 08:00 по 19:00";
    private const string scheduleChatForWeekends = "Еженедельно ПТ_21:00, СБ_ВС_09:00,20:00";

    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;

    static InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(
    new List<InlineKeyboardButton[]>()
    {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(showCurrentChatSchedules),
                InlineKeyboardButton.WithCallbackData(cancelChatSchedules),
                InlineKeyboardButton.WithCallbackData(scheduleChatForPersona),
                InlineKeyboardButton.WithCallbackData(scheduleChatForWeekends),
            }
    });

    public static async Task Main()
    {
        _botClient = new TelegramBotClient(token);
        _receiverOptions = new ReceiverOptions // Также присваем значение настройкам бота
        {
            AllowedUpdates = new[] // Тут указываем типы получаемых Update`ов, о них подробнее расказано тут https://core.telegram.org/bots/api#update
            {
                UpdateType.Message, // Сообщения (текст, фото/видео, голосовые/видео сообщения и т.д.)
            }
            // Параметр, отвечающий за обработку сообщений, пришедших за то время, когда ваш бот был оффлайн
            // True - не обрабатывать, False (стоит по умолчанию) - обрабаывать

            //ThrowPendingUpdates = true,
        };
        using var cancellation = new CancellationTokenSource();
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cancellation.Token);

        var me = await _botClient.GetMe();
        Console.WriteLine($"{me.FirstName} запущен!");

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
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;
                var user = message.From;

                //PutMsgToUserLogFile(user.Id, message.Text);
                //Console.WriteLine(user.Id + "\t" + message.Text);

                if (message.Type == MessageType.Text)
                {
                    await HandleTextMessage(botClient, message, message.Chat);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static async Task HandleTextMessage(ITelegramBotClient botClient, Message message, Chat chat)
    {
        if (!IsChatIdInWhiteList(chat.Id.ToString()))
        {
            await botClient.SendMessage(chat.Id, "You are not on the white list");
            await botClient.SendMessage(AdminChatId, $"Новый чат, id={message.From}, msg={message.Text}");
            return;
        }

        string answ = null;
        Schedule temp;
        switch (message.Text)
        {
            case "/start":
                await botClient.SendMessage(
                    chat.Id, 
                    "Приветсвую, здесь вы можете получать список открытых задач TSI для вашего РК",
                    ParseMode.None, 
                    null,
                    inlineKeyboard);
                break;

            case showCurrentChatSchedules:
                answ = SheduleManager.GetChatSchedules(chat.Id);
                await botClient.SendMessage(chat.Id, answ);
                break;

            case cancelChatSchedules:
                answ = SheduleManager.DeleteChatShedules(chat.Id);
                await botClient.SendMessage(chat.Id, answ);
                break;

            case scheduleChatForWeekends: // здесь идет добавление расписания для чата
                temp = new Schedule(chat.Id, scheduleChatForWeekends, true, Schedule.ScheduleTemplateWeekends());
                temp.Operation = SeleniumMonitorTSI.GetOpenedTickets;
                answ = SheduleManager.AddSchedules(temp);
                await botClient.SendMessage(chat.Id, answ);
                break;

            case scheduleChatForPersona: // здесь идет добавление расписания для чата
                temp = new Schedule(chat.Id, scheduleChatForPersona, true, Schedule.ScheduleTemplateOneDay());
                temp.Operation = SeleniumMonitorTSI.GetOpenedTickets;
                answ = SheduleManager.AddSchedules(temp);
                await botClient.SendMessage(chat.Id, answ);
                break;

            default:
                //await botClient.SendMessage(chat.Id, "Для этой команды нет обработчика.\nВы можете оставить предложение о функционале в меню Сервис");
                break;
        }
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

    private static bool IsChatIdInWhiteList(string chatId)
    {
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
    public Operation ScheduleOperation;

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

}

static class SheduleManager
{
    public static Queue<Schedule> SchedulesToImmediatlyOperate;
    private static List<Schedule> Schedules = new();

    public static string AddSchedules(Schedule schedule)
    {
        var isExist = Schedules.Where(x => x.ChatId == schedule.ChatId && x.Description == schedule.Description).Count() != 0;
        if (isExist) return "Такое распирасание для этого чата уже существует";

        Schedules.Add(schedule);
        return "Распирасание создано";
    }

    public static string GetChatSchedules(long chatId)
    {
        string answ = "Расписаний не найдено";

        var matchedSchedules = Schedules.Where(x => x.ChatId == chatId).ToList();
        if (matchedSchedules.Any())
        {
            answ = "";
            answ = string.Join("\n", matchedSchedules.Select(s => s.Description));
        }

        return answ;
    }

    public static string DeleteChatShedules(long chatId)
    {
        int count = 0;
        foreach (Schedule sched in Schedules)
        {
            if (sched.ChatId == chatId)
            {
                Schedules.Remove(sched);
                count++;
            }
        }
        return $"Удалено {count} расписаний";
    }

    public static void CheckAndRemoveOldSchedules()
    {
        var personalSchedules = Schedules.Where(x => x.IsEveryWeek == false).ToList();

        foreach (Schedule sched in personalSchedules)
        {
            if (DateTime.Now - sched.CreationDate > TimeSpan.FromDays(1))
                Schedules.Remove(sched);
        }
    }

    private static void PushToOperateQueue()
    {
        foreach (Schedule sched in Schedules)
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

    public static async void StartCheckAndExecSchedules(CancellationToken cancellation)
    {
        while (cancellation.IsCancellationRequested)
        {
            while(SchedulesToImmediatlyOperate.Count > 0)
            {
                var sched = SchedulesToImmediatlyOperate.Dequeue();

                string answ = sched.ScheduleOperation.Invoke();
                await TBot.SendMessage(sched.ChatId, answ);
            }
            Thread.Sleep(60 * 1000);
        }
    }

}

static class Log
{

}




