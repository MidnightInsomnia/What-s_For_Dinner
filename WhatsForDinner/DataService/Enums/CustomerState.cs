using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.DataService.Enums
{
    public enum CustomerState
    {
        None,
        Menu,

        //DISH ADD
        AddingDishName,
        AddingDishDescription,
        AddingDishDayTime,
        AddingDishPhoto,

        //DISH EDIT
        EditingDishName,
        EditingDishDescription,
        EditingDishDayTime,
        EditingDishPhoto,

        DeleteDishConfirmation,

        //DISH LIST & RANDOM
        WatchingDishList,
        RandomDishGenerating,
    }
}
