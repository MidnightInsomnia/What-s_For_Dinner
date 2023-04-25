using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;

namespace WhatsForDinner.MessageService
{
    public class MessageInfo
    {
        public long ChatId { get; private set; }
        public long CustomerId { get; private set; }
        public string UserName { get; private set; }
        public string MediaGroupId { get; private set; }
        public CustomerInputType CustomerInputType { get; private set; }

        public MessageInfo(CustomerInputType customerInputType, long customerId, long chatId, string userName, string mediaGroupId)
        {
            CustomerInputType = customerInputType;
            ChatId = chatId;
            CustomerId = customerId;
            UserName = userName;
            MediaGroupId = mediaGroupId;
        }
    }
}
