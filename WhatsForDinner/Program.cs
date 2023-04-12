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
using WhatsForDinner.Config;
using WhatsForDinner.DataService;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;
using static System.Net.Mime.MediaTypeNames;
using static WhatsForDinner.DataService.DataService;

Dictionary<long, Dish> _customerTempDishes = new Dictionary<long, Dish>();

Dictionary<long, Dish> _customerDishesToManipulate = new Dictionary<long, Dish>();
Dictionary<long, string> _lastWarningMessage = new Dictionary<long, string>();

const int DishNameMaxLength = 120;
const int DishDescriptionMaxLength = 200;
const int DishRecipeMaxLength = 800;

DinnerConfig.Initiation();

await DataService.InitAllCustomerStates();

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
    try
    {
        Console.WriteLine("КОТИК 0");
        var valid = InputCheck(botClient, update, cancellationToken).Result;
        Console.WriteLine("КОТИК 1");

        if (update.Message != null)
        {
            if (update.Message.Photo != null && !update.Message.MediaGroupId.IsNullOrEmpty())
            {
                update.Message.Photo = null;
                update.Message.MediaGroupId = null;

                if (!_lastWarningMessage.ContainsKey(update.Message.Chat.Id))
                {
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                    $"Вы можете отправить только одно фото",
                    cancellationToken: cancellationToken);

                    _lastWarningMessage.Add(update.Message.Chat.Id, "Вы можете отправить только одно фото");
                }

                return;
            }
        }

        if (!valid)
        {
            Console.WriteLine("КОТИК 2");
            if (update.Message != null)
            {
                Console.WriteLine("КОТИК 3");
                await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        $"Нет такого варианта ответа",
                        cancellationToken: cancellationToken);
            }
            Console.WriteLine("КОТИК 3");
            Console.WriteLine("ВОЗВРАТ В VALID ");

            return;
        }
        Console.WriteLine("КОТИК 4");
        if (update.CallbackQuery != null)
        {
            Console.WriteLine("CALLBACK ПРОЗОШЁЛ ТАК-ТО !!!!!");
            string dataStr = update.CallbackQuery.Data;

            if (dataStr.Contains("Edit"))
            {
                //ПРОРВЕРКА СУЩЕСТВУЕТ ЛИ БЛЮДО, ЕСЛИ НЕТ - ОШИБКА
                Console.WriteLine("CALLBACK EDIT ВАЩЕТА !!!!!");

                int dishId = 0;

                int.TryParse(dataStr.Split(' ')[1], out dishId);
                
                var dishToEdit = await DataService.GetDishById(dishId);

                if (dishToEdit == null)
                {
                    await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        $"Блюдо отсутствует в вашем меню",
                        cancellationToken: cancellationToken);

                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(update.CallbackQuery.Message.Chat.Id, CustomerState.Menu);
                    await OpenMenu(chatId: update.CallbackQuery.Message.Chat.Id, customerID: update.CallbackQuery.Message.Chat.Id, cancellationToken: cancellationToken);

                    return;
                }

                await DataService.SetCustomerState(update.CallbackQuery.From.Id, CustomerState.EditingDishName);

                _customerDishesToManipulate.Add(update.CallbackQuery.From.Id, dishToEdit);

                await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        $"Начинаем редактирование блюда {dishToEdit.DishName}",
                        cancellationToken: cancellationToken);

                await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        $"Введите название блюда",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", dishToEdit.DishName));
            }

            if (dataStr.Contains("Delete"))
            {
                //ПРОРВЕРКА СУЩЕСТВУЕТ ЛИ БЛЮДО, ЕСЛИ НЕТ - ОШИБКА
                Console.WriteLine("CALLBACK DELETE ВАЩЕТА !!!!!");

                int dishId = 0;

                int.TryParse(dataStr.Split(' ')[1], out dishId);

                var dishToDelete = await DataService.GetDishById(dishId);

                if (dishToDelete == null)
                {
                    await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        $"Блюдо отсутствует в вашем меню",
                        cancellationToken: cancellationToken);

                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(update.CallbackQuery.Message.Chat.Id, CustomerState.Menu);
                    await OpenMenu(chatId: update.CallbackQuery.Message.Chat.Id, customerID: update.CallbackQuery.Message.Chat.Id, cancellationToken: cancellationToken);

                    return;
                }

                await DataService.SetCustomerState(update.CallbackQuery.From.Id, CustomerState.DeleteDishConfirmation);

                _customerDishesToManipulate.Add(update.CallbackQuery.From.Id, dishToDelete);

                await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        $"Вы уверены, что хотите удалить {dishToDelete.DishName}?",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Нет", "Да"));
            }

            return;
        }

        try
        {
            Console.WriteLine("КОТИК 5");
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            Console.WriteLine("КОТИК 6");
            var customerId = update.Message.From.Id;
            var chatId = update.Message.Chat.Id;
            Console.WriteLine("КОТИК 7");
            var customerState = DataService.GetCustomerState(customerId).Result;
            Console.WriteLine("КОТИК 8");
            Console.WriteLine($"КОТИК 8.5     {message.Text == null}     PHOTO IS {message.Photo == null} MEDIA GROUP {update.Message.MediaGroupId}");
            //ДЛЯ DISH ADDING RETURN НА ВСЕ ВИДЫ СООБЩЕНИЙ КРОМЕ PHOTO

            if (customerState != CustomerState.AddingDishPhoto && customerState != CustomerState.EditingDishPhoto && message.Text is not { } messageText)
                return;
            Console.WriteLine("КОТИК 9");
            //НЕ СЕЙЧАС
            // Only process text messages
            /*if (message.Text is not { } messageText)
                return;*/

            //var messageIsText = !(message.Text is not { } messageText);

            if (!_customerTempDishes.ContainsKey(customerId))
                _customerTempDishes.Add(customerId, null);

            if(_lastWarningMessage.ContainsKey(update.Message.Chat.Id))
            {
                await Console.Out.WriteLineAsync("ИНДУЛЬГЕНЦИЯ ПАЛУЧАИЦА");
                _lastWarningMessage.Remove(update.Message.Chat.Id);
            }

            if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("/start"))
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
                    var customerDishesCount = await DataService.CountAllCustomerDishes(customerId);

                    if (message.Text.Equals("1"))
                    {
                        await DataService.SetCustomerState(customerId, CustomerState.AddingDishName);

                        await botClient.SendTextMessageAsync(chatId,
                        $"Введите название блюда",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню"));
                        break;
                    }
                    if (message.Text.Equals("2"))
                    {
                        if (customerDishesCount == 0)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Необходимо добавить хотя бы одно блюдо, чтобы воспользоваться данной функцией.",
                            cancellationToken: cancellationToken);
                            break;
                        }

                        await DataService.SetCustomerState(customerId, CustomerState.WatchingDishList);

                        var dishList = await DataService.GetAllDishes(customerId);

                        await botClient.SendTextMessageAsync(chatId,
                        $"Ваши блюда:",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню"));

                        foreach (var item in dishList)
                        {
                            await SendDish(chatId, item, cancellationToken: cancellationToken);
                        }
                        break;
                    }
                    else if (message.Text.Equals("3🍽"))
                    {
                        if (customerDishesCount == 0)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Необходимо добавить минимум два блюда, чтобы воспользоваться данной функцией.",
                            cancellationToken: cancellationToken);
                            break;
                        }

                        await DataService.SetCustomerState(customerId, CustomerState.RandomDishGenerating);

                        await SendRandomDish(chatId, customerId, cancellationToken);

                        break;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        $"Нет такого варианта ответа",
                        cancellationToken: cancellationToken);
                    }

                    break;

                case CustomerState.AddingDishName:
                    if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }

                    //Вынести в константу
                    if(message.Text.Length > DishNameMaxLength)
                    {
                        await botClient.SendTextMessageAsync(chatId,
                        $"Недопустимая длина названия, максимум - {DishNameMaxLength} символов.",
                        cancellationToken: cancellationToken);
                        return;
                    }

                    var newDish = new Dish();
                    newDish.DishName = message.Text;
                    _customerTempDishes[customerId] = newDish;
                    await DataService.SetCustomerState(customerId, CustomerState.AddingDishDescription);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Введите описание блюда",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым"));

                    break;

                case CustomerState.AddingDishDescription:
                    if (message.Text.ToLower().Equals("оставить пустым"))
                    {
                        _customerTempDishes[customerId].DishDescription = "";
                        await DataService.SetCustomerState(customerId, CustomerState.AddingDishRecipe);
                    }
                    else if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }
                    else
                    {
                        //Вынести в константу
                        if (message.Text.Length > DishDescriptionMaxLength)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Недопустимая длина описания, максимум - {DishDescriptionMaxLength} символов.",
                            cancellationToken: cancellationToken);
                            return;
                        }

                        _customerTempDishes[customerId].DishDescription = message.Text;
                    }

                    await DataService.SetCustomerState(customerId, CustomerState.AddingDishRecipe);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Если желаете, введите рецепт.",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Без рецепта")
                        );

                    break;

                case CustomerState.AddingDishRecipe:
                    if (message.Text.ToLower().Equals("без рецепта"))
                    {
                        _customerTempDishes[customerId].DishRecipe = "";
                    }
                    else if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }
                    else
                    {
                        //Вынести в константу
                        if (message.Text.Length > DishRecipeMaxLength)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Недопустимая длина рецепта, максимум - {DishRecipeMaxLength} символов.",
                            cancellationToken: cancellationToken);
                            return;
                        }

                        _customerTempDishes[customerId].DishRecipe = message.Text;
                    }

                    await DataService.SetCustomerState(customerId, CustomerState.AddingDishPhoto);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Добавьте фото",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым")
                        );

                    break;

                case CustomerState.AddingDishPhoto:
                    if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }

                    if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("оставить пустым"))
                    {
                        //NULL ДЛЯ PHOTO
                        Console.WriteLine("ТИПА ОСТАВЛЯЕМ ПУСТЫМ");
                        _customerTempDishes[customerId].DishPhotoBase64 = "";
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

                            MemoryStream ms = new MemoryStream();

                            var file = await botClient.GetInfoAndDownloadFileAsync(
                                fileId: fileId,
                                destination: ms,
                                cancellationToken: cancellationToken);

                            //byte[] dishPicBytes = ms.ToArray();
                            _customerTempDishes[customerId].DishPhotoBase64 = Convert.ToBase64String(ms.ToArray());
                        }
                    }
                    catch (Exception ex)
                    {
                        await Console.Out.WriteLineAsync($"EXC {ex.Message}");
                    }

                    //В БУДУЩЕМ ПЕРЕДАВАТЬ ТОЛЬКО ЭКЗЕМПЛЯР DISH
                    var dish = _customerTempDishes[customerId];

                    dish.CustomerID = customerId;

                    await DataService.AddDish(dish);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Блюдо {_customerTempDishes[customerId].DishName} добавлено!",
                        cancellationToken: cancellationToken);

                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                    break;

                case CustomerState.WatchingDishList:
                    if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }

                    break;

                case CustomerState.RandomDishGenerating:
                    if (message.Text.ToLower().Equals("не подходит"))
                    {
                        await SendRandomDish(chatId, customerId, cancellationToken);
                        break;
                    }

                    if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }

                    break;


                case CustomerState.DeleteDishConfirmation:
                    if (message.Text.ToLower().Equals("да"))
                    {
                        //ТУТ УДАЛЕНИЕ
                        await DataService.DeleteDishById(_customerDishesToManipulate[customerId].DishID);

                        await botClient.SendTextMessageAsync(chatId,
                        $"Блюдо удалено.",
                        cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);

                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);
                        break;
                    }
                    else if (message.Text.ToLower().Equals("нет"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);
                        break;
                    }
                    break;

                case CustomerState.EditingDishName:
                    if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);
                        break;
                    }

                    //Вынести в константу
                    if (message.Text.Length > DishNameMaxLength)
                    {
                        await botClient.SendTextMessageAsync(chatId,
                        $"Недопустимая длина названия, максимум - {DishNameMaxLength} символов.",
                        cancellationToken: cancellationToken);
                        return;
                    }

                    _customerDishesToManipulate[customerId].DishName = message.Text;

                    await DataService.SetCustomerState(customerId, CustomerState.EditingDishDescription);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Введите описание блюда",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым", "Оставить текущее"));

                    break;

                case CustomerState.EditingDishDescription:
                    if (message.Text.ToLower().Equals("оставить пустым"))
                    {
                        _customerDishesToManipulate[customerId].DishDescription = "";
                        await DataService.SetCustomerState(customerId, CustomerState.EditingDishRecipe);
                    }
                    else if (message.Text.ToLower().Equals("оставить текущее"))
                    {
                        await DataService.SetCustomerState(customerId, CustomerState.EditingDishRecipe);
                    }
                    else if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);
                        break;
                    }
                    else
                    {
                        if (message.Text.Length > DishDescriptionMaxLength)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Недопустимая длина описания, максимум - {DishDescriptionMaxLength} символов.",
                            cancellationToken: cancellationToken);
                            return;
                        }

                        _customerDishesToManipulate[customerId].DishDescription = message.Text;
                    }

                    await DataService.SetCustomerState(customerId, CustomerState.EditingDishRecipe);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Если желаете, введите рецепт.",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Оставить текущий", "Без рецепта")
                        );

                    break;

                case CustomerState.EditingDishRecipe:
                    if (message.Text.ToLower().Equals("без рецепта"))
                    {
                        _customerDishesToManipulate[customerId].DishRecipe = "";
                    }
                    else if (message.Text.ToLower().Equals("назад в меню"))
                    {
                        //ВОЗВРАТ В МЕНЮ
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);
                        break;
                    }
                    else if (message.Text.ToLower().Equals("оставить текущий"))
                    {
                        //ХА-ХА УБРАТЬ ЭТОТ ПОЗОР ПОТОМ
                    }
                    else
                    {
                        if (message.Text.Length > DishRecipeMaxLength)
                        {
                            await botClient.SendTextMessageAsync(chatId,
                            $"Недопустимая длина рецепта, максимум - {DishRecipeMaxLength} символов.",
                            cancellationToken: cancellationToken);
                            return;
                        }

                        _customerDishesToManipulate[customerId].DishRecipe = message.Text;
                    }

                    await DataService.SetCustomerState(customerId, CustomerState.EditingDishPhoto);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Добавьте фото",
                        cancellationToken: cancellationToken,
                        replyMarkup: GetReplyButtons("Назад в меню", "Оставить пустым", "Оставить текущее")
                        );

                    break;

                case CustomerState.EditingDishPhoto:

                    if (!message.Text.IsNullOrEmpty() && !message.Text.ToLower().Equals("назад в меню") && !message.Text.ToLower().Equals("оставить пустым") && !message.Text.ToLower().Equals("оставить текущее"))
                    {
                        await Console.Out.WriteLineAsync("ОТРАБАТЫВАЕТ ЗАЩИТА ");
                        break;
                    }

                    if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("назад в меню"))
                    {
                        await DataService.SetCustomerState(customerId, CustomerState.Menu);
                        await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                        _customerDishesToManipulate.Remove(customerId);
                        break;
                    }

                    if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("оставить пустым"))
                    {
                        //NULL ДЛЯ PHOTO
                        Console.WriteLine("ТИПА ОСТАВЛЯЕМ ПУСТЫМ");
                        _customerDishesToManipulate[customerId].DishPhotoBase64 = "";
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

                            MemoryStream ms = new MemoryStream();
                            var file = await botClient.GetInfoAndDownloadFileAsync(
                                fileId: fileId,
                                destination: ms,
                                cancellationToken: cancellationToken);

                            _customerDishesToManipulate[customerId].DishPhotoBase64 = Convert.ToBase64String(ms.ToArray());
                        }
                    }
                    catch (Exception ex)
                    {
                        await Console.Out.WriteLineAsync($"EXC {ex.Message}");
                    }

                    await DataService.UpdateDish(_customerDishesToManipulate[customerId]);

                    await botClient.SendTextMessageAsync(chatId,
                        $"Блюдо {_customerDishesToManipulate[customerId].DishName} обновлено!",
                        cancellationToken: cancellationToken);

                    //ВОЗВРАТ В МЕНЮ
                    await DataService.SetCustomerState(customerId, CustomerState.Menu);
                    await OpenMenu(chatId: chatId, customerID: customerId, cancellationToken: cancellationToken);

                    _customerDishesToManipulate.Remove(customerId);
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
    catch (Exception ex)
    {
        Console.WriteLine($"HANDLE UPDATE EXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX {ex.Message}");
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

async Task<bool> InputCheck(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    var valid = true;

    if(update.Message != null)
    {
        var chatId = update.Message.Chat.Id;

        var customerId = update.Message.From.Id;

        var customerState = DataService.GetCustomerState(customerId).Result;

        //ТУТ НАДО РАЗРЕШИТЬ В СЛУЧАЕ ЕСЛИ STATE СВЯЗАН С ФОТО
        if (update.Message.Photo != null && customerState != CustomerState.EditingDishPhoto && customerState != CustomerState.AddingDishPhoto)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА PHOTO");
            valid = false;
        }

        if (update.Message.Animation != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА ANIMATION");
            valid = false;
        }

        if (update.Message.Audio != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА AUDIO");
            valid = false;
        }

        if (update.Message.Document != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА DOCUMENT");
            valid = false;
        }

        if (update.Message.Video != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА VIDEO");
            valid = false;
        }

        if (update.Message.VideoNote != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА VIDEONOTE");
            valid = false;
        }

        if (update.Message.Voice != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА VOICE");
            valid = false;
        }

        if (update.Message.Sticker != null)
        {
            await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА STICKER");
            valid = false;
        }
    }

    if (update.Message == null && update.CallbackQuery == null)
    {
        await Console.Out.WriteLineAsync("ЗАЩИТА ОТРАБОТАЛА MESSAGE & CALLBACK QUERY");
        valid = false;
    }        

    return valid;
}

async Task OpenMenu(long chatId, long customerID, CancellationToken cancellationToken)
{
    //Узнать количество
    var count = await DataService.CountAllCustomerDishes(customerID: customerID);

    Message sendMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"Количество блюд в вашем меню: {count} \n\n" +
                  $"1. Добавить блюдо\n" +
                  $"2. Посмотреть меню\n" +
                  $"3. Что сегодня на ужин? 🍽",
            cancellationToken: cancellationToken,
            replyMarkup: GetReplyButtons("1", "2", "3🍽"));
}

async Task SendDish(long chatId, Dish dish, CancellationToken cancellationToken)
{
    var dishTxt = "";

    dishTxt += $"Название: {dish.DishName}\n";

    if (!dish.DishDescription.IsNullOrEmpty())
        dishTxt += $"Описание: {dish.DishDescription}\n";

    if (!dish.DishRecipe.IsNullOrEmpty())
        dishTxt += $"Рецепт:\n{dish.DishRecipe}\n";

    if (dish.DishPhotoBase64.IsNullOrEmpty())
    {
        await botClient.SendTextMessageAsync(chatId,
                    text: dishTxt,
                    cancellationToken: cancellationToken,
                    replyMarkup: GetInlineButtons(dish.DishID.ToString()));
    }

    if (!dish.DishPhotoBase64.IsNullOrEmpty())
    {
        using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(dish.DishPhotoBase64)))
        {
            await botClient.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(ms),
                caption: dishTxt,
                cancellationToken: cancellationToken,
                replyMarkup: GetInlineButtons(dish.DishID.ToString()));
        }
    }
}

async Task SendRandomDish(long chatId, long customerId, CancellationToken cancellationToken)
{
    //ЕСЛИ БЛЮДО ТОЛЬКО ОДНО ВЫДАТЬ ОШИБКУ
    var lastPos = await DataService.GetLastRandomDishPos(customerId);

    Console.WriteLine($"LAST POS {lastPos}");

    var dishList = await DataService.GetAllDishes(customerId);

    await Console.Out.WriteLineAsync($"DISH LIST COUNT ВАЩЕТА {dishList.Count}");

    var randomDishNumber = GetRandomNumber(LastIndex: dishList.Count, LastGeneratedNumber: lastPos).Result;

    await DataService.SetLastRandomDishPos(customerId, randomDishNumber);

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
    Console.WriteLine($"CALLBACK DATA + {callBackText}");
    InlineKeyboardMarkup inlineKeyboard = new(new[]
    {
        // first row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "Изменить", callbackData: $"Edit {callBackText}"),
        },
        // second row
        new []
        {
            InlineKeyboardButton.WithCallbackData(text: "Удалить", callbackData: $"Delete {callBackText}"),
        },
    });

    return inlineKeyboard;
}