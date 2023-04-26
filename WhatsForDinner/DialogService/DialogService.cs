using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using Telegram.Bot.Types;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;
using WhatsForDinner.MessageService;
using WhatsForDinner.DataService;
using System.Threading;

namespace WhatsForDinner.DialogService
{
    public class DialogService
    {
        const int DishNameMaxLength = 120;
        const int DishDescriptionMaxLength = 200;

        public static Dictionary<long, Dish> _customerTempDishes = new Dictionary<long, Dish>();
        public static Dictionary<long, string> _lastWarningMessage = new Dictionary<long, string>();

        public static async Task Init(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            MessageReciever messageReciever = new MessageReciever(botClient, update, cancellationToken);
            var messageInfo = messageReciever.GetMessageInfo();
            MessageSender messageSender = new MessageSender(botClient, messageInfo, cancellationToken);

            var customerId = messageInfo.CustomerId;
            var chatId = messageInfo.ChatId;
            var userName = messageInfo.UserName;

            var customerState = customerId != -1 ? DataService.DataService.GetCustomerState(customerId).Result : CustomerState.None;

            var valid = InputCheck(customerState, messageInfo, messageSender).Result;

            if (!valid) return;

            if (update.CallbackQuery != null)
            {
                if (update.CallbackQuery.Data != null)
                    CallBackQueryHandle(customerId, update.CallbackQuery.Data, messageSender);

                return;
            }

            var message = update.Message;

            if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("/start"))
            {
                StartCommand(customerId, userName, messageSender);

                return;
            }

            if (!message.Text.IsNullOrEmpty() && message.Text.ToLower().Equals("назад в меню"))
            {
                if(_customerTempDishes.ContainsKey(customerId))
                    _customerTempDishes.Remove(customerId);

                //ВОЗВРАТ В МЕНЮ
                await DataService.DataService.SetCustomerState(customerId, CustomerState.Menu);
                await messageSender.SendMenu();

                return;
            }

            switch (customerState)
            {
                case CustomerState.Menu:
                    OnMenuAnswer(customerId, message.Text, messageSender);
                    break;

                case CustomerState.AddingDishName:
                    OnDishNameEdit(customerId, message.Text, messageSender, isNewDish: true);
                    break;

                case CustomerState.AddingDishDescription:
                    OnDishDescriptionEdit(customerId, message.Text, messageSender, isNewDish: true);
                    break;

                case CustomerState.AddingDishDayTime:
                    OnDishDayTimeEdit(customerId, message.Text, messageSender, isNewDish: true);
                    break;

                case CustomerState.AddingDishPhoto:
                    OnDishPhotoEdit(customerId, message, messageSender, messageReciever, isNewDish: true);
                    break;

                case CustomerState.RandomDishGenerating:
                    OnRandomDishGenerating(message.Text, messageSender);
                    break;

                case CustomerState.DeleteDishConfirmation:
                    OnDeleteDishConfirmation(customerId, message.Text, messageSender);
                    break;

                case CustomerState.EditingDishName:
                    OnDishNameEdit(customerId, message.Text, messageSender, isNewDish: false);
                    break;

                case CustomerState.EditingDishDescription:
                    OnDishDescriptionEdit(customerId, message.Text, messageSender, isNewDish: false);
                    break;

                case CustomerState.EditingDishDayTime:
                    OnDishDayTimeEdit(customerId, message.Text, messageSender, isNewDish: false);
                    break;

                case CustomerState.EditingDishPhoto:
                    OnDishPhotoEdit(customerId, message, messageSender, messageReciever, isNewDish: false);
                    break;

                default:
                    break;
            }
        }

        public static async void CallBackQueryHandle(long customerId, string data, MessageSender messageSender)
        {
            int dishId = 0;

            int.TryParse(data.Split(' ')[1], out dishId);

            var dishToEdit = await DataService.DataService.GetDishById(dishId);

            if (dishToEdit == null)
            {
                await messageSender.SendError("Блюдо отсутствует в вашем меню");

                //ВОЗВРАТ В МЕНЮ
                await DataService.DataService.SetCustomerState(customerId, CustomerState.Menu);
                await messageSender.SendMenu();
                return;
            }

            if (data.Contains("Edit"))
            {
                DialogService._customerTempDishes.Add(customerId, dishToEdit);
                await DataService.DataService.SetCustomerState(customerId, CustomerState.EditingDishName);

                await messageSender.SendNextStage(CustomerState.EditingDishName, dishToEdit.DishName);
            }

            if (data.Contains("Delete"))
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.DeleteDishConfirmation);
                DialogService._customerTempDishes.Add(customerId, dishToEdit);

                await messageSender.SendNextStage(CustomerState.DeleteDishConfirmation, dishToEdit.DishName);
            }
        }
        public static async void StartCommand(long customerId, string userName, MessageSender messageSender)
        {
            if (await DataService.DataService.IsCustomerExists(customerId))
            {
                await Console.Out.WriteLineAsync("CUSTOMER СУЩЕСТВУЕТ");
                await DataService.DataService.UpdateCustomerLastDate(customerId);

                await messageSender.SendMessageAsync($"{userName}, с возвращением!");
            }
            else
            {
                await Console.Out.WriteLineAsync("CUSTOMER НЕ СУЩЕСТВУЕТ");
                await DataService.DataService.AddCustomer(customerId, userName, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                await messageSender.SendMessageAsync($"{userName}, добро пожаловать!");
            }

            await DataService.DataService.SetCustomerState(customerId, CustomerState.Menu);
            await messageSender.SendMenu();
        }
        public static async void OnMenuAnswer(long customerId, string message, MessageSender messageSender)
        {
            var customerDishesCount = await DataService.DataService.CountAllCustomerDishes(customerId);

            if (message.Equals("1"))
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.AddingDishName);

                await messageSender.SendNextStage(CustomerState.AddingDishName);
                return;
            }

            if (message.Equals("2"))
            {
                if (customerDishesCount == 0)
                {
                    await messageSender.SendError("Необходимо добавить хотя бы одно блюдо, чтобы воспользоваться данной функцией.");
                    return;
                }

                await DataService.DataService.SetCustomerState(customerId, CustomerState.WatchingDishList);

                await messageSender.SendNextStage(CustomerState.WatchingDishList);
                return;
            }

            if (message.Equals("3🍽"))
            {
                if (customerDishesCount == 0)
                {
                    await messageSender.SendError("Необходимо добавить минимум два блюда, чтобы воспользоваться данной функцией.");
                    return;
                }

                await DataService.DataService.SetCustomerState(customerId, CustomerState.RandomDishGenerating);

                await messageSender.SendNextStage(CustomerState.RandomDishGenerating);
                return;
            }

            await messageSender.SendError("Нет такого варианта ответа!");
        }
        public static async void OnDishNameEdit(long customerId, string message, MessageSender messageSender, bool isNewDish)
        {
            if (message.Length > DishNameMaxLength)
            {
                await messageSender.SendError($"Недопустимая длина названия, максимум - {DishNameMaxLength} символов.");
                return;
            }

            if (!DialogService._customerTempDishes.ContainsKey(customerId))
                DialogService._customerTempDishes.Add(customerId, new Dish());

            DialogService._customerTempDishes[customerId].DishName = message;

            if (isNewDish)
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.AddingDishDescription);
                await messageSender.SendNextStage(CustomerState.AddingDishDescription);
            }
            else
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.EditingDishDescription);
                await messageSender.SendNextStage(CustomerState.EditingDishDescription);
            }
        }
        public static async void OnDishDescriptionEdit(long customerId, string message, MessageSender messageSender, bool isNewDish)
        {
            switch (message.ToLower())
            {
                case "оставить пустым":
                    DialogService._customerTempDishes[customerId].DishDescription = "";
                    break;

                case "оставить текущее":
                    if (isNewDish)
                    {
                        await messageSender.SendError("Нет такого варианта ответа.");
                        return;
                    }
                    break;

                default:
                    if (message.Length > DishDescriptionMaxLength)
                    {
                        await messageSender.SendError($"Недопустимая длина описания, максимум - {DishDescriptionMaxLength} символов.");
                        return;
                    }

                    DialogService._customerTempDishes[customerId].DishDescription = message;
                    break;
            }

            if (isNewDish)
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.AddingDishDayTime);
                await messageSender.SendNextStage(CustomerState.AddingDishDayTime);
            }
                

            if (!isNewDish)
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.EditingDishDayTime);
                await messageSender.SendNextStage(CustomerState.EditingDishDayTime);
            }
        }
        public static async void OnDishDayTimeEdit(long customerId, string message, MessageSender messageSender, bool isNewDish)
        {
            switch (message.ToLower())
            {
                case "любой":
                    _customerTempDishes[customerId].DishDayTimeID = (int)DishDayTime.Any;
                    break;

                case "завтрак":
                    _customerTempDishes[customerId].DishDayTimeID = (int)DishDayTime.Breakfast;
                    break;

                case "обед":
                    _customerTempDishes[customerId].DishDayTimeID = (int)DishDayTime.Lunch;
                    break;

                case "ужин":
                    _customerTempDishes[customerId].DishDayTimeID = (int)DishDayTime.Dinner;
                    break;

                case "оставить текущий":
                    if (isNewDish)
                    {
                        await messageSender.SendError("Нет такого варианта ответа.");
                        return;
                    }
                    break;

                default:
                    await messageSender.SendError("Нет такого варианта ответа.");
                    return;
            }

            if (isNewDish)
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.AddingDishPhoto);
                await messageSender.SendNextStage(CustomerState.AddingDishPhoto);
            }

            if (!isNewDish)
            {
                await DataService.DataService.SetCustomerState(customerId, CustomerState.EditingDishPhoto);
                await messageSender.SendNextStage(CustomerState.EditingDishPhoto);
            }
        }
        public static async void OnDishPhotoEdit(long customerId, Message message, MessageSender messageSender, MessageReciever messageReciever, bool isNewDish)
        {
            if (!string.IsNullOrEmpty(message.Text))
            {
                switch (message.Text.ToLower())
                {
                    case "без фото":
                        DialogService._customerTempDishes[customerId].DishPhotoBase64 = "";
                        break;
                    case "оставить текущее":
                        if (isNewDish)
                        {
                            await messageSender.SendError("Нет такого варианта ответа.");
                            return;
                        }
                        break;
                    default:
                        await messageSender.SendError("Нет такого варианта ответа.");
                        return;
                }
            }

            if (message.Photo is not null)
            {
                if (message.Caption is not null)
                {
                    await messageSender.SendError("Не нужно было добавлять текст к фотографии 🙄.");
                }
                //ДОБАВЛЕНИЕ ФОТО
                DialogService._customerTempDishes[customerId].DishPhotoBase64 = await messageReciever.TelegramPhotoToBase64(message.Photo);
            }

            EndDishEditing(customerId, messageSender, isNewDish);
        }
        public static async void EndDishEditing(long customerId, MessageSender messageSender, bool isNewDish)
        {
            if (isNewDish)
            {
                DialogService._customerTempDishes[customerId].CustomerID = customerId;

                await DataService.DataService.AddDish(DialogService._customerTempDishes[customerId]);
                await messageSender.SendMessageAsync($"Блюдо {DialogService._customerTempDishes[customerId].DishName} добавлено!");
            }

            if (!isNewDish)
            {
                await DataService.DataService.UpdateDish(DialogService._customerTempDishes[customerId]);
                await messageSender.SendMessageAsync($"Блюдо {DialogService._customerTempDishes[customerId].DishName} обновлено!");
            }

            DialogService._customerTempDishes.Remove(customerId);

            //ВОЗВРАТ В МЕНЮ
            await DataService.DataService.SetCustomerState(customerId, CustomerState.Menu);
            await messageSender.SendMenu();
        }
        public static async void OnRandomDishGenerating(string message, MessageSender messageSender)
        {
            if (message.ToLower().Equals("не подходит 🎲"))
            {
                await messageSender.SendRandomDish();
                return;
            }
        }
        public static async void OnDeleteDishConfirmation(long customerId, string message, MessageSender messageSender)
        {
            if (message.ToLower().Equals("да") || message.ToLower().Equals("нет"))
            {
                if (message.ToLower().Equals("да"))
                {
                    //УДАЛЕНИЕ
                    await DataService.DataService.DeleteDishById(DialogService._customerTempDishes[customerId].DishID);
                    await messageSender.SendMessageAsync("Блюдо удалено.");
                }

                DialogService._customerTempDishes.Remove(customerId);

                //ВОЗВРАТ В МЕНЮ
                await DataService.DataService.SetCustomerState(customerId, CustomerState.Menu);
                await messageSender.SendMenu();
                return;
            }

            await messageSender.SendError("Нет такого варианта ответа!");
            return;
        }

        public static async Task<bool> InputCheck(CustomerState customerState, MessageInfo messageInfo, MessageSender messageSender)
        {
            //Возврат при недопустимом типе Update
            if (messageInfo.CustomerInputType == CustomerInputType.NotAllowedType)
                return false;

            //При отправке нескольких фото
            if (messageInfo.CustomerInputType == CustomerInputType.PhotoMediaGroup)
            {
                string errorMessage = "Нет такого варианта ответа!";

                if (customerState == CustomerState.AddingDishPhoto || customerState == CustomerState.EditingDishPhoto)
                {
                    errorMessage = "Вы можете отправить только одно фото";
                }

                if (!messageInfo.MediaGroupId.IsNullOrEmpty())
                {
                    if (DialogService._lastWarningMessage.ContainsKey(messageInfo.CustomerId))
                    {
                        if (!DialogService._lastWarningMessage[messageInfo.CustomerId].Equals(messageInfo.MediaGroupId))
                        {
                            await messageSender.SendError(errorMessage);
                        }

                        DialogService._lastWarningMessage[messageInfo.CustomerId] = messageInfo.MediaGroupId;
                    }
                    else
                    {
                        DialogService._lastWarningMessage.Add(messageInfo.CustomerId, messageInfo.MediaGroupId);

                        await messageSender.SendError(errorMessage);
                    }
                }

                return false;
            }

            //При отправке фото не на стадии добавления/изменения фотографии
            if (messageInfo.CustomerInputType == CustomerInputType.Photo)
                if (customerState != CustomerState.AddingDishPhoto && customerState != CustomerState.EditingDishPhoto)
                {
                    await messageSender.SendError("Нет такого варианта ответа!");
                    return false;
                }

            return true;
        }
    }
}