using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WhatsForDinner.Config;
using WhatsForDinner.DataService;

using static WhatsForDinner.DataService.DataService;

DinnerConfig.Initiation();
DataService.ConnectionString = DinnerConfig.AppConfiguration["WhatsForDinner:ConnectionStrings:DishDBConnectionString"];


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
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;

    var chatId = message.Chat.Id;

    //Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

    // Echo received message text
    /*Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: "You said:\n" + messageText,
        cancellationToken: cancellationToken);*/

    if (message.Text.Equals("/start"))
    {
        if (await DataService.IsCustomerExists(update.Message.From.Id))
        {
            await Console.Out.WriteLineAsync("CUSTOMER СУЩЕСТВУЕТ");
            await DataService.UpdateCustomerLastDate(update.Message.From.Id);
        }
        else
        {
            await Console.Out.WriteLineAsync("CUSTOMER НЕ СУЩЕСТВУЕТ");
            await DataService.AddCustomer(update.Message.From.Id, update.Message.From.Username, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        }
    }

    if (message.Text.Equals("/allUsers"))
    {
        string userStr = "";
        var res = DataService.GetAllCustomers().Result;
        foreach (var name in res)
        {
            userStr += name + Environment.NewLine;
            //await Console.Out.WriteLineAsync($"USER: {res}");
        }

        Message sendMessage = await botClient.SendTextMessageAsync(chatId,
            $"Пользователи:\n{userStr}", cancellationToken: cancellationToken);
    }

    if (message.Text.Equals("/deleteMe"))
    {
        if (await DataService.IsCustomerExists(update.Message.From.Id))
        {
            await DataService.DeleteCustomer(update.Message.From.Id);
            await Console.Out.WriteLineAsync("CUSTOMER УДАЛЁН");
        }
        else
        {
            await Console.Out.WriteLineAsync("CUSTOMER НЕ ЗАРЕГИСТРИРОВАН");
        }
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
