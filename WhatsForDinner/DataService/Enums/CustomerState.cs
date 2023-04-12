﻿using System;
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
        AddingDishRecipe,
        AddingDishPhoto,

        DeletingDish,

        //DISH LIST & RANDOM
        WatchingDishList,
        RandomDishGenerating,
        
        //DISH EDIT
        EditingDishName,
        EditingDishDescription,
        EditingDishRecipe,
        EditingDishPhoto,

        DeleteDishConfirmation,
    }
}