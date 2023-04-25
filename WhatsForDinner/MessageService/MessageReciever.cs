using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WhatsForDinner.DataService;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;

namespace WhatsForDinner.MessageService
{
    public class MessageReciever
    {
        private ITelegramBotClient BotClient { get; set; }
        private Update Update { get; set; }
        private CancellationToken CancellationToken { get; set; }
        private MessageInfo MessageInfo { get; set; }

        public MessageReciever(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) 
        {
            BotClient = botClient;
            Update = update;
            CancellationToken = cancellationToken;
            MessageInfo = GetInfoFromUpdate();
        }

        private MessageInfo GetInfoFromUpdate()
        {
            CustomerInputType customerInputType = CustomerInputType.NotAllowedType;
            long chatId = -1; 
            long customerId = -1;
            string mediaGroupId = "";
            string userName = "";

            switch (Update.Type)
            {
                case UpdateType.Message:
                    if (Update.Message != null 
                        && Update.Message.From != null 
                        && Update.Message.From.Username != null)
                    {
                        chatId = Update.Message.Chat.Id;
                        customerId = Update.Message.From.Id;
                        userName = Update.Message.From.Username;

                        if (Update.Message.Photo != null)
                        {
                            if (!Update.Message.MediaGroupId.IsNullOrEmpty())
                            {
                                if(Update.Message.MediaGroupId != null)
                                    mediaGroupId = Update.Message.MediaGroupId;

                                customerInputType = CustomerInputType.PhotoMediaGroup;
                                break;
                            }

                            customerInputType = CustomerInputType.Photo;
                            break;
                        }
                    }

                    if(Update.Message != null 
                        && Update.Message.From != null 
                        && Update.Message.From.Username != null 
                        && !Update.Message.Text.IsNullOrEmpty())
                    {
                        chatId = Update.Message.Chat.Id;
                        customerId = Update.Message.From.Id;
                        userName = Update.Message.From.Username;

                        customerInputType = CustomerInputType.Text;
                        break;
                    }
                    break;

                case UpdateType.CallbackQuery:
                    if (Update.CallbackQuery != null 
                        && Update.CallbackQuery.From != null 
                        && Update.CallbackQuery.From.Username != null
                        && Update.CallbackQuery.Message != null
                        && Update.CallbackQuery.Message.Chat != null)
                    {
                        chatId = Update.CallbackQuery.Message.Chat.Id;
                        customerId = Update.CallbackQuery.From.Id;
                        userName = Update.CallbackQuery.From.Username;

                        customerInputType = CustomerInputType.CallbackQuery;
                    }
                    break;

                default:
                    customerInputType = CustomerInputType.NotAllowedType;
                    break;
            }

            return new MessageInfo(customerInputType, customerId, chatId, userName, mediaGroupId);
        }

        public MessageInfo GetMessageInfo()
        {
            return MessageInfo;
        }
        public async Task<string> TelegramPhotoToBase64(PhotoSize[] photo)
        {
            var fileId = photo.Last().FileId;

            MemoryStream ms = new MemoryStream();

            await BotClient.GetInfoAndDownloadFileAsync(
                fileId: fileId,
                destination: ms,
                cancellationToken: CancellationToken);

            return Convert.ToBase64String(ms.ToArray());
        }
    }
}
