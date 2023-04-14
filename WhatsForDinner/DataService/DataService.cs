using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using WhatsForDinner.Config;
using WhatsForDinner.DataService.Entities;
using WhatsForDinner.DataService.Enums;

namespace WhatsForDinner.DataService
{
    public static class DataService
    {
        //********************************************************************//
        //**                            CUSTOMER                            **//
        //********************************************************************//

        public static async Task InitAllCustomerStates()
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                foreach (var customer in db.Customers)
                {
                    customer.StateID = (int)CustomerState.None;
                }

                await db.SaveChangesAsync();
            }
        }

        public static async Task<bool> IsCustomerExists(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = await db.Customers.SingleAsync(cust => cust.CustomerID == customerID);

                if (customer != null)
                    Console.WriteLine($"{customer.CustomerID} \t{customer.CustomerName} \t{customer.EnterDate}");

                return customer != null;
            }
        }

        public static async Task<int> GetLastRandomDishPos(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                int res = 0;

                var customer = await db.Customers.SingleAsync(cust => cust.CustomerID == customerID);

                if (customer != null && customer.LastRandomDishPos != null)
                    res = (int)customer.LastRandomDishPos;

                return res;
            }
        }

        public static async Task SetLastRandomDishPos(long customerID, int Pos)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.Single(cust => cust.CustomerID == customerID);

                if(customer != null)
                    customer.LastRandomDishPos = Pos;

                await db.SaveChangesAsync();
            }
        }

        public static async Task<CustomerState> GetCustomerState(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var res = CustomerState.None;

                var customer = db.Customers.Single(cust => cust.CustomerID == customerID);

                if (customer != null)
                    res = (CustomerState)customer.StateID;

                await Console.Out.WriteLineAsync($"РЕЗУЛЬТАТ STATE {res.ToString()}");

                return res;
            }
        }

        public static async Task SetCustomerState(long customerID, CustomerState customerState)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.Single(cust => cust.CustomerID == customerID);

                if (customer != null)
                    customer.StateID = (int)customerState;

                await db.SaveChangesAsync();
            }
        }

        public static async Task AddCustomer(long customerID, string customerName, string timeStamp)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var newCustomer = new Customer();

                newCustomer.CustomerID = customerID;
                newCustomer.CustomerName = customerName;
                newCustomer.EnterDate = timeStamp;
                newCustomer.LastDate = timeStamp;

                db.Customers.Add(newCustomer);

                await db.SaveChangesAsync();

                await Console.Out.WriteLineAsync($"КОРОЧЕ VALUES ({newCustomer.CustomerID}, {newCustomer.CustomerName}, {newCustomer.EnterDate}, {newCustomer.LastDate}");
            }
        }

        public static async Task DeleteCustomer(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.Single(cust => cust.CustomerID == customerID);

                db.Customers.Remove(customer);

                await db.SaveChangesAsync();

                await Console.Out.WriteLineAsync($"CUSTOMER DELETED");
            }
        }

        public static async Task<List<string>> GetAllCustomerNames()
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var clientNames = new List<string>();

                foreach (var customer in db.Customers)
                {
                    if(customer.CustomerName != null)
                        clientNames.Add(customer.CustomerName);
                }

                return clientNames;
            }
        }

        public static async Task UpdateCustomerLastDate(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.Single(cust => cust.CustomerID == customerID);

                customer.LastDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                
                await db.SaveChangesAsync();

                await Console.Out.WriteLineAsync("UPDATED");
            }
        }

        //********************************************************************//
        //**                              DISH                              **//
        //********************************************************************//

        public static async Task AddDish(Dish dish)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                db.Dishes.Add(dish);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<int> CountAllCustomerDishes(long customerID)
        {
            //Возвращение количества блюд пользователя
            using (ApplicationContext db = new ApplicationContext())
            {
                return db.Dishes.Where(dish => dish.CustomerID == customerID).ToList().Count();
            }
        }

        //Возвращение всех блюд
        public static async Task<List<Dish>> GetAllDishes(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                return db.Dishes.Where(dish => dish.CustomerID == customerID).ToList();
            }
        }

        public static async Task<Dish> GetDishById(int dishId)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var dish = await db.Dishes.SingleAsync(dish => dish.DishID == dishId);

                return dish;
            }
        }

        public static async Task DeleteDishById(int dishId)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var dishToDelete = await db.Dishes.SingleAsync(dish => dish.DishID == dishId);

                db.Dishes.Remove(dishToDelete);

                db.SaveChanges();
            }
        }

        public static async Task UpdateDish(Dish dish)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                db.Dishes.Update(dish);

                await db.SaveChangesAsync();
            }
        }
    }
}
