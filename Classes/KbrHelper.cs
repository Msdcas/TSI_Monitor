using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace TSI_Monitor.Classes
{
    class KbrHelper
    {
        public static InlineKeyboardMarkup GetInlineKbr_OnAddNewChat(string chatId)
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

        public const string showCurrentChatSchedules = "Показать расписание для этого чата";                //mbut1
        public const string cancelChatSchedules = "Отменить рассылку в этот чат";                           //mbut2
        public const string scheduleEveryHour = "Выгружать сегодня каждый час с 08:00 по 19:00";            //mbut3
        public const string scheduleOnlyWeekends = "Выгружать еженедельно ПТ_21:00, СБ_ВС_09:00,20:00";     //mbut4
        public const string scheduleGetImmediatly = "Получить список открытых TSI немедленно";              //mbut5

        public static InlineKeyboardMarkup mainInlineKeyboard = new InlineKeyboardMarkup(
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

    }
}
