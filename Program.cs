using TSI_Monitor.Classes;

string _BasicFolder = Environment.CurrentDirectory;

Log.Initialize();
Console.WriteLine($"Current folder = {_BasicFolder}");
Log.Logger.Info("Starting... Enter 'stop' for stopped the bot and the scheduler\n", new { Color = "green" });

DBContext dBContext = new DBContext();
dBContext.DatabaseManager(Path.Combine(_BasicFolder, "TsiMonitor.db"));

UserRepository userRepos = new UserRepository(dBContext);
ChatRepository chatRepos = new ChatRepository(dBContext);
ScheduleRepository scheduleRepos = new ScheduleRepository(dBContext);

TBot bot = new TBot("7768609296:AAGJR_9WJv4rJQ8n6W_hHv3zzGLignWNx_A",
    userRepos, chatRepos, scheduleRepos);

CancellationTokenSource cancTokenSource = new CancellationTokenSource();

var schedTask = Task.Run(() => SheduleManager.StartCheckAndExecSchedules(cancTokenSource.Token));
var botTask = Task.Run(() => bot.Start(cancTokenSource.Token));

while (!cancTokenSource.IsCancellationRequested)
{
    var input = Console.ReadLine();
    if (input?.ToLower() == "stop")
    {
        cancTokenSource.Cancel();
        bot.Stop();
    }
    else
    {
        Console.WriteLine("Command not found. Available commands: 'stop'\n");
    }
}


// добавить таблицу бд для общих настроек, где хранить прочие данные классов

// ПИЗДЕЦ!!! для приватных чатов выводить запрос на разрешения ID пользователя. Для групповых - ID чата.
//также добавить ID учетки админа в белыый список из конфига, а не чата админа.

//раз в день проверять наличие файла error.log и выдавать кол-во содержимого админу
//проверять состояние тасков и выдавать результат если кто то умер
// изменить привязку к админу не через его чат ид, а через ид собеседника

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

