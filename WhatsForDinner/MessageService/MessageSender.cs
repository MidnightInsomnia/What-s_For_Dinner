using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using WhatsForDinner.DataService;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;
using WhatsForDinner.RandomService;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace WhatsForDinner.MessageService
{
    public class MessageSender
    {
        ITelegramBotClient BotClient { get; set; }
        CancellationToken CancellationToken { get; set; }
        long ChatId { get; set; }
        long CustomerId { get; set; }
        public MessageSender(ITelegramBotClient botClient, MessageInfo messageInfo,  CancellationToken cancellationToken)
        {
            BotClient = botClient;
            ChatId = messageInfo.ChatId;
            CustomerId = messageInfo.CustomerId;
            CancellationToken = cancellationToken;
        }

        public async Task SendMessageAsync(string messageText)
        {
            await BotClient.SendTextMessageAsync(ChatId,
                messageText,
                cancellationToken: CancellationToken);
        }

        public async Task SendMessageWithButtonsAsync(string messageText, params string[] replyButtonText)
        {
            await BotClient.SendTextMessageAsync(ChatId,
                messageText,
                cancellationToken: CancellationToken,
                replyMarkup: GetReplyButtons(replyButtonText));
        }

        public async Task SendError(string messageText)
        {
            await BotClient.SendTextMessageAsync(ChatId, messageText, cancellationToken: CancellationToken);
        }

        public async Task SendMenu()
        {
            //Узнать количество
            var count = await DataService.DataService.CountAllCustomerDishes(customerID: CustomerId);

            Message sendMessage = await BotClient.SendTextMessageAsync(
                    chatId: ChatId,
                    text: $"Количество блюд в вашем меню: {count} \n\n" +
                          $"1. Добавить блюдо\n" +
                          $"2. Посмотреть меню\n" +
                          $"3. Что приготовить? 🍽",
                    cancellationToken: CancellationToken,
                    replyMarkup: GetReplyButtons("1", "2", "3🍽"));
        }

        public async Task SendDish(Dish dish)
        {
            var dishTxt = "";

            dishTxt += $"<b>Название:</b> {dish.DishName}\n";

            if (!dish.DishDescription.IsNullOrEmpty())
                dishTxt += $"<b>Описание:</b> {dish.DishDescription}\n";

            dishTxt += $"<b>Приём пищи:</b> {Tools.GetDishDayTimeStr(dish.DishDayTimeID)}\n";

            if (dish.DishPhotoBase64.IsNullOrEmpty())
            {
                await BotClient.SendTextMessageAsync(ChatId,
                            text: dishTxt,
                            cancellationToken: CancellationToken,
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                            replyMarkup: GetInlineDishButtons(dish.DishID.ToString(), dish.DishName));
            }

            if (!dish.DishPhotoBase64.IsNullOrEmpty())
            {
                using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(dish.DishPhotoBase64)))
                {
                    await BotClient.SendPhotoAsync(ChatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(ms),
                        caption: dishTxt,
                        cancellationToken: CancellationToken,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: GetInlineDishButtons(dish.DishID.ToString(), dish.DishName));
                }
            }
        }

        public async Task SendRandomDish()
        {
            var lastPos = await DataService.DataService.GetLastRandomDishPos(CustomerId);

            var dishList = await DataService.DataService.GetAllDishes(CustomerId);

            var randomDishNumber = RandomService.Tools.GetRandomNumber(LastIndex: dishList.Count, LastGeneratedNumber: lastPos).Result;

            await DataService.DataService.SetLastRandomDishPos(CustomerId, randomDishNumber);

            await BotClient.SendTextMessageAsync(ChatId,
                            $"Сегодняшнее блюдо: ",
                            cancellationToken: CancellationToken,
                            replyMarkup: GetReplyButtons("Назад в меню", "Не подходит 🎲"));

            await SendDish(dishList[randomDishNumber]);
        }

        public async Task SendNextStage(CustomerState nextStage)
        {
            switch (nextStage)
            {
                case CustomerState.Menu:
                    break;

                //DISH ADD
                case CustomerState.AddingDishName:
                    break;
                case CustomerState.AddingDishDescription:
                    break;
                case CustomerState.AddingDishDayTime:
                    break;
                case CustomerState.AddingDishPhoto:
                    break;
                //DISH EDIT
                case CustomerState.EditingDishName:
                    break;
                case CustomerState.EditingDishDescription:
                    break;
                case CustomerState.EditingDishDayTime:
                    break;
                case CustomerState.EditingDishPhoto:
                    break;

                case CustomerState.DeleteDishConfirmation:
                    break;

                //DISH LIST & RANDOM
                case CustomerState.WatchingDishList:
                    break;
                case CustomerState.RandomDishGenerating:
                    break;
            }
        }

        IReplyMarkup GetReplyButtons(params string[] buttonText)
        {
            KeyboardButton[] keyboardButtons = new KeyboardButton[buttonText.Length];

            for (int i = 0; i < buttonText.Length; i++)
            {
                keyboardButtons[i] = new KeyboardButton(buttonText[i]);
            }

            ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
            {
                keyboardButtons
            })
            {
                //Изменяет размер кнопки относительно размера элемента
                ResizeKeyboard = true
            };

            return replyKeyboardMarkup;
        }

        IReplyMarkup GetInlineDishButtons(string callBackText, string dishName)
        {
            Console.WriteLine($"CALLBACK DATA + {callBackText}");
            var searchStr = dishName.Replace(" ", "+");
            InlineKeyboardMarkup inlineKeyboard = new(new[]
            {
                // first row
                new []
                {
                    InlineKeyboardButton.WithUrl(text: "Искать рецепт в интернете", url: $"https://yandex.ru/search/?text={searchStr}+рецепт"),
                },
                // second row
                new []
                {
                    InlineKeyboardButton.WithCallbackData(text: "Изменить", callbackData: $"Edit {callBackText}"),
                    InlineKeyboardButton.WithCallbackData(text: "Удалить", callbackData: $"Delete {callBackText}"),
                },
                });

            return inlineKeyboard;
        }
    }
}
