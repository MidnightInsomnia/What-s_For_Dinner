using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.DataService.Entities
{
    //Модель для сущности Dish
    [Table("Dish")]
    public class Dish
    {
        [Key]
        public int DishID { get; set; }
        public string DishName { get; set; } = "";
        public string? DishDescription { get; set; } = "";
        public string? DishRecipe { get; set; } = "";
        public string? DishPhotoBase64 { get; set; } = "";
        public long CustomerID { get; set; }
    }
}
