using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.DataService.Enums
{
    public enum CustomerInputType
    {
        NotAllowedType,
        CallbackQuery,
        Text,
        Photo,
        PhotoMediaGroup
    }
}
