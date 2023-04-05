using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using System.Threading;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using WhatsForDinner;
using WhatsForDinner.Config;
using WhatsForDinner.DataService;
using static System.Net.Mime.MediaTypeNames;
using static WhatsForDinner.DataService.DataService;

//Dictionary<long, CustomerState> _customerStates = new Dictionary<long, CustomerState>();
Dictionary<long, Dish> _customerTempDishes = new Dictionary<long, Dish>();

DinnerConfig.Initiation();
DataService.ConnectionString = DinnerConfig.AppConfiguration["WhatsForDinner:ConnectionStrings:DishDBConnectionString"];

await DataService.InitAllUserStates();

var botClient = new TelegramBotClient(DinnerConfig.AppConfiguration["WhatsForDinner:TelegramToken"]);

using CancellationTokenSource cts = new();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if(update.CallbackQuery != null)
    {
        if(update.CallbackQuery.Data.Contains("Change"))
            await Console.Out.WriteLineAsync("CALLBACK ЩА КАК ИЗМЕНИМ");

        if (update.CallbackQuery.Data.Contains("Delete"))
            await Console.Out.WriteLineAsync("CALLBACK ЩА КАК УДАЛИМ");

        return;
    }

    try
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;

        var customerId = update.Message.From.Id;

        var customerState = DataService.GetCustomerState(customerId).Result;

        //ДЛЯ DISH ADDING RETURN НА ВСЕ ВИДЫ СООБЩЕНИЙ КРОМЕ PHOTO

        if (message.Text is not { } messageText && customerState != CustomerState.DishAddingPhotoUpload)
            return;

        //НЕ СЕЙЧАС
        // Only process text messages
        /*if (message.Text is not { } messageText)
            return;*/

        //var messageIsText = !(message.Text is not { } messageText);

        var chatId = message.Chat.Id;

        if (!_customerTempDishes.ContainsKey(customerId))
            _customerTempDishes.Add(customerId, null);

        if (!message.Text.IsNullOrEmpty() && message.Text.Equals("/start"))
        {
            if (await DataService.IsCustomerExists(customerId))
            {
                await Console.Out.WriteLineAsync("CUSTOMER СУЩЕСТВУЕТ");
                await DataService.UpdateCustomerLastDate(customerId);

                Message sendMessage = await botClient.SendTextMessageAsync(chatId,
                    $"Пользователь {update.Message.From.Username} уже зарегистрирован.", cancellationToken: cancellationToken);
            }
            else
            {
                await Console.Out.WriteLineAsync("CUSTOMER НЕ СУЩЕСТВУЕТ");
                await DataService.AddCustomer(customerId, update.Message.From.Username, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                Message sendMessage = await botClient.SendTextMessageAsync(chatId,
                    $"Пользователь {update.Message.From.Username} добавлен.", cancellationToken: cancellationToken);
            }

            await DataService.SetCustomerState(customerId, CustomerState.Menu);

            await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
        }

        switch (customerState)
        {
            case CustomerState.Menu:
                if (message.Text.Equals("1"))
                {
                    await DataService.SetCustomerState(customerId, CustomerState.DishAdding);

                    await botClient.SendTextMessageAsync(chatId,
                    $"Введите название блюда",
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню"));
                }

                if (message.Text.Equals("2"))
                {
                    await DataService.SetCustomerState(customerId, CustomerState.DishListWatching);

                    var dishList = await DataService.GetAllDishes(customerId);

                    await botClient.SendTextMessageAsync(chatId,
                    $"Ваши блюда:",
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню"));

                    foreach (var dish in dishList)
                    {
                        await SendDish(chatId, dish, cancellationToken: cancellationToken);
                    }
                }

                if (message.Text.Equals("3⚡️"))
                {
                    await DataService.SetCustomerState(customerId, CustomerState.RandomDishGenerating);

                    await SendRandomDish(chatId, customerId, cancellationToken);
                }

                break;

            case CustomerState.DishAdding:
                if (message.Text.Equals("Назад в меню"))
                {
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }

                _customerTempDishes[customerId] = new Dish(message.Text);
                await DataService.SetCustomerState(customerId, CustomerState.DishAddingDescription);

                await botClient.SendTextMessageAsync(chatId,
                    $"Введите описание блюда",
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым"));

                break;

            case CustomerState.DishAddingDescription:
                if (message.Text.Equals("Оставить пустым"))
                {
                    _customerTempDishes[customerId].dishDescription = "";
                    await DataService.SetCustomerState(customerId, CustomerState.DishAddingRecipe);
                }
                else if (message.Text.Equals("Назад в меню"))
                {
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }
                else
                {
                    _customerTempDishes[customerId].dishDescription = message.Text;
                }

                await DataService.SetCustomerState(customerId, CustomerState.DishAddingRecipe);

                await botClient.SendTextMessageAsync(chatId,
                    $"Если желаете, введите рецепт.",
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню", "Без рецепта")
                    );

                break;

            case CustomerState.DishAddingRecipe:
                if (message.Text.Equals("Без рецепта"))
                {
                    _customerTempDishes[customerId].dishRecipe = "";
                }
                else if (message.Text.Equals("Назад в меню"))
                {
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }
                else
                {
                    _customerTempDishes[customerId].dishRecipe = message.Text;
                }

                await DataService.SetCustomerState(customerId, CustomerState.DishAddingPhotoUpload);

                await botClient.SendTextMessageAsync(chatId,
                    $"Добавьте фото",
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым")
                    );

                break;

            case CustomerState.DishAddingPhotoUpload:
                if (!message.Text.IsNullOrEmpty() && message.Text.Equals("Назад в меню"))
                {
                    await Console.Out.WriteLineAsync($"COUNT {message.Photo.Count()}");
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }

                if (!message.Text.IsNullOrEmpty() && message.Text.Equals("Оставить пустым"))
                {
                    //NULL ДЛЯ PHOTO
                    Console.WriteLine("ТИПА ОСТАВЛЯЕМ ПУСТЫМ");
                    _customerTempDishes[customerId].dishPhotoBase64 = "";
                }

                if (message.Photo == null)
                {
                    //ЧТО-ТО ПРО НЕПРАВИЛЬНЫЙ ВВОД
                    //break;
                }

                try
                {
                    if (message.Photo is not null)
                    {
                        await Console.Out.WriteLineAsync("ТИПА НЕ NULL ");
                        //ДОБАВЛЕНИЕ ФОТО
                        var fileId = update.Message.Photo.Last().FileId;
                        var fileInfo = await botClient.GetFileAsync(fileId);
                        var filePath = fileInfo.FilePath;

                        //Stream fileStream = new Stream();
                        MemoryStream ms = new MemoryStream();
                        //await using Stream fileStream = System.IO.File.OpenWrite(destinationFilePath);
                        var file = await botClient.GetInfoAndDownloadFileAsync(
                            fileId: fileId,
                            destination: ms,
                            cancellationToken: cancellationToken);

                        //byte[] dishPicBytes = ms.ToArray();
                        _customerTempDishes[customerId].dishPhotoBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync($"EXC {ex.Message}");
                }


                await Console.Out.WriteLineAsync("ДОШЛО ИЛИ НЕТ? ");

                //В БУДУЩЕМ ПЕРЕДАВАТЬ ТОЛЬКО ЭКЗЕМПЛЯР DISH
                var newDish = _customerTempDishes[customerId];

                await DataService.AddDish(newDish.dishName, newDish.dishDescription, newDish.dishRecipe, newDish.dishPhotoBase64, customerId);

                await botClient.SendTextMessageAsync(chatId,
                    $"Блюдо {_customerTempDishes[customerId].dishName} добавлено!",
                    cancellationToken: cancellationToken);

                //ВОЗВРАТ В МЕНЮ
                await DataService.SetCustomerState(customerId, CustomerState.Menu);
                await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                break;

            case CustomerState.DishListWatching:
                if (message.Text.Equals("Назад в меню"))
                {
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }

                break;

            case CustomerState.RandomDishGenerating:
                if (message.Text.Equals("Не подходит"))
                {
                    await SendRandomDish(chatId, customerId, cancellationToken);
                    break;
                }

                if (message.Text.Equals("Назад в меню"))
                {
                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                    break;
                }

                break;


            default:
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EXCEPTION: {ex}");
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

async Task OpenMenu(long chatId, long customerID, CancellationToken cancellationToken)
{
    //Узнать количество
    var count = await DataService.CountAllCustomerDishes(customerID: customerID);

    Message sendMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"В вашем меню {count} блюд.\n\n" +
                  $"1. Добавить блюдо\n" +
                  $"2. Посмотреть меню\n" +
                  $"3. Что сегодня на ужин?",
            cancellationToken: cancellationToken,
            replyMarkup: GetReplyButtons("1", "2", "3⚡️"));
}

async Task SendDish(long chatId, Dish dish, CancellationToken cancellationToken)
{
    var dishTxt = "";

    dishTxt += $"Название: {dish.dishName}\n";

    if (!dish.dishDescription.IsNullOrEmpty())
        dishTxt += $"Описание: {dish.dishDescription}\n";

    if (!dish.dishRecipe.IsNullOrEmpty())
        dishTxt += $"Рецепт:\n{dish.dishRecipe}\n";

    if (dish.dishPhotoBase64.IsNullOrEmpty())
    {
        await botClient.SendTextMessageAsync(chatId,
                    text: dishTxt,
                    cancellationToken: cancellationToken,
                    replyMarkup: GetInlineButtons(dish.dishId.ToString()));
    }

    if (!dish.dishPhotoBase64.IsNullOrEmpty())
    {
        using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(dish.dishPhotoBase64)))
        {
            await botClient.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(ms),
                caption: dishTxt,
                cancellationToken: cancellationToken,
                replyMarkup: GetInlineButtons(dish.dishId.ToString()));
        }
    }
}

async Task SendRandomDish(long chatId, long customerId, CancellationToken cancellationToken)
{
    //ЕСЛИ БЛЮДО ТОЛЬКО ОДНО ВЫДАТЬ ОШИБКУ
    var lastPos = await DataService.GetLastRandomGeneratedPos(customerId);

    Console.WriteLine($"LAST POS {lastPos}");

    var dishList = await DataService.GetAllDishes(customerId);

    await Console.Out.WriteLineAsync($"DISH LIST COUNT ВАЩЕТА {dishList.Count}");

    var randomDishNumber = GetRandomNumber(LastIndex: dishList.Count -1, LastGeneratedNumber: lastPos).Result;

    await DataService.SetLastRandomGeneratedPos(customerId, randomDishNumber);

    await botClient.SendTextMessageAsync(chatId,
                    $"Сегодняшнее блюдо: ", 
                    cancellationToken: cancellationToken,
                    replyMarkup: GetReplyButtons("Назад в меню", "Не подходит"));

    await SendDish(chatId, dishList[randomDishNumber], cancellationToken);
}

async Task<int> GetRandomNumber(int LastIndex, int LastGeneratedNumber)
{
    Random rnd = new Random();

    int randomNumber = rnd.Next(0, LastIndex);

    Console.WriteLine($"RND NUM {randomNumber}");

    if (randomNumber == LastGeneratedNumber)
    {
        Console.WriteLine($"ЗАШЛО RND NUM {randomNumber} AND LAST {LastGeneratedNumber}");
        return GetRandomNumber(LastIndex, LastGeneratedNumber).Result;
    }

    return randomNumber;
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
        /*new KeyboardButton[]
        { "1",
          "2",
          "3⚡️"
        },*/
    })
    {
        //Изменяет размер кнопки относительно размера элемента
        ResizeKeyboard = true
    };

    return replyKeyboardMarkup;

    //INLINE BUTTONS

    /*InlineKeyboardMarkup inlineKeyboard = new(new[]
    {
        // first row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "1", callbackData: "11"),
            InlineKeyboardButton.WithCallbackData(text: "2", callbackData: "12"),
        },
        // second row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "3", callbackData: "21"),
            InlineKeyboardButton.WithCallbackData(text: "2.2", callbackData: "22"),
        },
    });

    return inlineKeyboard;*/
}

IReplyMarkup GetInlineButtons(string callBackText)
{
    InlineKeyboardMarkup inlineKeyboard = new(new[]
    {
        // first row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "Изменить", callbackData: $"Change {callBackText}"),
        },
        // second row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "Удалить", callbackData: $"Delete {callBackText}"),
        },
    });

    return inlineKeyboard;
}