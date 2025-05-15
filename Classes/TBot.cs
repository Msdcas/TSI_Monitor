using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TSI_Monitor.Classes
{
    class TBot
    {
        private readonly string botToken;
        private readonly long? AdminChatId = 6750792041; //351907910

        private readonly ITelegramBotClient _botClient = null;
        private readonly ReceiverOptions _receiverOptions = null;
        private TaskCompletionSource TaskComplSource = null;

        private readonly TSI_Monitor.Classes.UserRepository UserRepos;
        private readonly TSI_Monitor.Classes.ChatRepository ChatRepos;
        private readonly ScheduleRepository SchedRepos;


        public TBot(string token, UserRepository userRepos,
                ChatRepository chatRepos, ScheduleRepository schedRepos, long adminID = 0)
        {
            botToken = token;
            AdminChatId = adminID;
            UserRepos = userRepos;
            ChatRepos = chatRepos;
            SchedRepos = schedRepos;

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
        }

        public async Task Start(CancellationToken cancellation)
        {
            if (TaskComplSource is not null)
            {
                Log.Logger.Warn("Попытка повтороного запуска уже запущенного экземпляра бота");
                throw new InvalidOperationException("Bot is already running.");
            }
            TaskComplSource = new();

            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cancellation);

            var me = await _botClient.GetMe();
            Log.Logger.Info($"Bot {me.FirstName} is started", new { Color = "green" });

            await TaskComplSource.Task;

            TaskComplSource = null;
            Log.Logger.Info($"Bot {me.FirstName} is stopped", new { Color = "red" });
        }

        public void Stop()
        {
            try
            {
                TaskComplSource?.SetResult();
            }
            catch
            {
                Log.Logger.Error("Bot is not running.");
            }
        }

        public async Task SendMessage(long chatId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                Log.Logger.Warn("SendMessage:: message were empty");
                return;
            }

            //var botClient = new TelegramBotClient(botToken);
            try
            {
                var result = await _botClient.SendMessage(chatId, message);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Ошибка при отправке сообщения: {ex.Message}");
            }
        }

        public async Task SendAdminMessage(string message)
        {
            if (AdminChatId == null)
            {
                Log.Logger.Warn($"AdminChatId was not set:: msg={message}");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Log.Logger.Warn("SendMessage:: message were empty");
                return;
            }
            
            try
            {
                var result = await _botClient.SendMessage(AdminChatId, message);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"AdminSendMessage. Ошибка при отправке сообщения: {ex.Message}");
            }
        }

        private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var chatId = update.Message.Chat.Id;
                var userId = update.Message.From.Id;
                if (chatId < 0) // group
                {
                    if (await ChatRepos.IsBlock(chatId))
                    {
                        return;
                    }
                    if (!await ChatRepos.Contains(chatId))
                    {
                        ChatRepos.Add(new Chat(chatId, isBlock: true));

                        await botClient.SendMessage(update.Message.Chat.Id, "This chat is not on the white list.\nRequest sent to admin");
                        await botClient.SendMessage(AdminChatId,
                            $"New group chat, id={chatId},msg={update.Message.Text}",
                            replyMarkup: KbrHelper.GetInlineKbr_OnAddNewChat(chatId.ToString()));
                        return;
                    }
                }
                else // private chat
                {
                    if (await UserRepos.IsBlock(chatId))
                    {
                        return;
                    }
                    if (!await UserRepos.Contains(chatId))
                    {
                        UserRepos.Add(new User(userId, isBlock: true));

                        await botClient.SendMessage(update.Message.Chat.Id, "You are not on the white list.\nRequest sent to admin");
                        await botClient.SendMessage(AdminChatId,
                            $"New private chat, id={userId}," +
                            $"user={update.Message.From.FirstName}\\{update.Message.From.LastName}",
                            replyMarkup: KbrHelper.GetInlineKbr_OnAddNewChat(userId.ToString()));
                        return;
                    }
                }

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
                Log.Logger.Error(ex.ToString());
            }
        }

        private async Task HandleTextMessage(ITelegramBotClient botClient, Update update, Message message, Telegram.Bot.Types.Chat chat)
        {
            Log.Logger.Debug($"Handle new text msg={update.Message.Text}, chat_id={update.Message.Chat.Id}");

            string answ = null;
            ChatSchedule temp;

            var command = message.Text.Replace($"@{_botClient.GetMe().Result.Username}", "");
            switch (command)
            {
                case "/start":
                    await botClient.SendMessage(
                        chat.Id,
                        "Здесь вы можете получать список открытых задач TSI для вашего РК \n Функционал доступен в меню чата",
                        ParseMode.None,
                        null,
                        replyMarkup: KbrHelper.mainInlineKeyboard);
                    break;

                //Показать расписание для этого чата
                case "/mbut1":
                    answ = (await SchedRepos.GetNames(chat.Id)).ToString();
                    await botClient.SendMessage(chat.Id, answ);
                    break;

                //cancelChatSchedules
                case "/mbut2":
                    answ = await SchedRepos.DeleteAsync(chat.Id);
                    await botClient.SendMessage(chat.Id, answ);
                    break;

                //scheduleChatForPersona
                case "/mbut3":
                    temp = new ChatSchedule(chat.Id, KbrHelper.scheduleEveryHour, true, ChatSchedule.TimesForOneDay());
                    temp.ExecuteOperation = SeleniumTsiMonitor.GetOpenedTickets;
                    answ = "Ежечасное расписание " + await SchedRepos.Add(temp);
                    await botClient.SendMessage(chat.Id, answ);
                    break;

                //scheduleChatForWeekends
                case "/mbut4":
                    temp = new ChatSchedule(chat.Id, KbrHelper.scheduleOnlyWeekends, true, ChatSchedule.TimesOnlyWeekends());
                    //temp = new Schedule(chat.Id, "test_schedule", true, Schedule.ScheduleTemplateTest());
                    temp.ExecuteOperation = SeleniumTsiMonitor.GetOpenedTickets;
                    answ = "Еженедельное расписание " + await SchedRepos.Add(temp);
                    await botClient.SendMessage(chat.Id, answ);
                    break;

                //scheduleGetImmediatly
                case "/mbut5":
                    await botClient.SendMessage(chat.Id, SeleniumTsiMonitor.GetOpenedTickets());
                    //await botClient.SendMessage(chat.Id, answ);
                    break;

                default:
                    await botClient.SendMessage(chat.Id, "Для этой команды нет обработчика.");
                    break;
            }
        }

        private async Task HandleCallbackQuery(ITelegramBotClient botClient, Update update)
        {
            var callback = update.CallbackQuery;
            var user = callback.From;
            var chat = callback.Message.Chat;

            //Log.Logger.Debug($"str={callback.Data}, chat_id={chat.Id}");

            if (user.Id == AdminChatId)
            {
                long newChatId;
                if (long.TryParse(update.CallbackQuery.Data.Replace("addChat_", ""), out newChatId))
                {
                    if (callback.Data.StartsWith("addChat_"))
                    {
                        UserRepos.Add(new User(newChatId));

                        await botClient.SendMessage(newChatId, $"You have been added to the WHITE list");
                        //Log.Logger.Debug($"add new chat {newChatId}");
                        //await botClient.AnswerCallbackQuery(callback.Id, $"Чат {newChatId} добавлен в белый список");
                        await botClient.SendMessage(AdminChatId, $"Чат {newChatId} добавлен в белый список");
                        await botClient.DeleteMessage(AdminChatId, update.CallbackQuery.Message.Id);
                    }

                    if (callback.Data.StartsWith("ignChat_"))
                    {
                        UserRepos.Add(new User(newChatId, isBlock: true));
                        await botClient.DeleteMessage(AdminChatId, update.CallbackQuery.Message.Id);
                        await botClient.SendMessage(newChatId, $"You have been added to the BLACK list");
                    }

                    return;
                }
            }

            // через AnswerCallbackQuery нельзя передавать слоишком длинные строки. => передаем статус и короткие строки

            string answ = null;
            ChatSchedule temp;
            switch (callback.Data)
            {
                //mainInlineKeyboard operations and answers
                //showCurrentChatSchedules
                case "mbut1":
                    answ = (await SchedRepos.GetNames(chat.Id)).ToString();
                    await botClient.AnswerCallbackQuery(callback.Id, answ);
                    break;

                //cancelChatSchedules
                case "mbut2":
                    answ = await SchedRepos.DeleteAsync(chat.Id);
                    await botClient.AnswerCallbackQuery(callback.Id, answ);
                    break;

                //scheduleChatForPersona
                case "mbut3":
                    temp = new ChatSchedule(chat.Id, KbrHelper.scheduleEveryHour, true, ChatSchedule.TimesForOneDay());
                    temp.ExecuteOperation = SeleniumTsiMonitor.GetOpenedTickets;
                    answ = "Ежечасное расписание " + await SchedRepos.Add(temp);
                    await botClient.AnswerCallbackQuery(callback.Id, answ);
                    break;

                //scheduleChatForWeekends
                case "mbut4":
                    temp = new ChatSchedule(chat.Id, KbrHelper.scheduleOnlyWeekends, true, ChatSchedule.TimesOnlyWeekends());
                    //temp = new Schedule(chat.Id, "test_schedule", true, Schedule.ScheduleTemplateTest());
                    temp.ExecuteOperation = SeleniumTsiMonitor.GetOpenedTickets;
                    answ = "Еженедельное расписание " + await SchedRepos.Add(temp);
                    await botClient.AnswerCallbackQuery(callback.Id, answ);
                    break;

                //scheduleGetImmediatly
                case "mbut5":
                    //await botClient.SendMessage(chat.Id, SeleniumMonitorTSI.GetOpenTSITickets());
                    //await botClient.AnswerCallbackQuery(callback.Id, "Запрос выполнен");  //Telegram Bot API error 400: Bad Request: MESSAGE_TOO_LONG
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await botClient.AnswerCallbackQuery(callback.Id, "Собираем информацию...");
                            var tickets = SeleniumTsiMonitor.GetOpenedTickets();
                            await botClient.SendMessage(chat.Id, tickets);
                        }
                        catch (Exception ex)
                        {
                            await botClient.SendMessage(chat.Id, "Возникла ошибка во время работы модуля SeleniumMonitorTSI");
                            Log.Logger.Error(ex.ToString());
                        }
                    });
                    break;

                default:
                    break;
            }
            //Log.Logger.Debug(answ);
        }

        private Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
        {
            // Тут создадим переменную, в которую поместим код ошибки и её сообщение 
            var ErrorMessage = error switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => error.ToString()
            };

            Log.Logger.Error("TBot_ErorrHandler:: " + ErrorMessage);
            return Task.CompletedTask;
        }

    }
}
