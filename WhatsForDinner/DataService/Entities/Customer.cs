using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.DataService.Entities
{
    //Модель для сущности Customer
    [Table("Customer")]
    public class Customer
    {
        [Key]
        public long CustomerID { get; set; }
        public string CustomerName { get; set; } = "";
        public string EnterDate { get; set; } = "";
        public string LastDate { get; set; } = "";
        public int StateID { get; set; }
        public int? LastRandomDishPos { get; set; }
    }
}
