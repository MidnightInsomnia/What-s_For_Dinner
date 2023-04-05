using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner
{
    public class Dish
    {
        public long dishId;
        public string dishName = "";
        public string dishDescription = "";
        public string dishRecipe = "";
        public string dishPhotoBase64 = "";
        public long customerId;

        public Dish(string name)
        {
            dishName = name;
        }
    }
}
