using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner
{
    public enum CustomerState
    {
        None,
        Menu,
        DishAdding,
        DishAddingDescription,
        DishAddingRecipe,
        DishAddingPhotoUpload,
        DishDeleting,
        DishListWatching,
        RandomDishGenerating
    }
}
