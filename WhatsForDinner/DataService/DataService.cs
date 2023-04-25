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

        /// <summary>
        /// При запуске присваивает всем пользователям нейтральное состояние
        /// </summary>
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

        /// <summary>
        /// Проверяет есть ли пользователь в базе
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <returns>True - если пользователь в базе; False - если нет</returns>
        public static async Task<bool> IsCustomerExists(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = await db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID);

                if (customer != null)
                    Console.WriteLine($"{customer.CustomerID} \t{customer.CustomerName} \t{customer.EnterDate}");

                return customer != null;
            }
        }

        /// <summary>
        /// Получает ID последнего рандомного блюда, нужен для отсутствия повторной генерации
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <returns>int ID блюда, по-умолчанию: 0</returns>
        public static async Task<int> GetLastRandomDishPos(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                int res = 0;

                var customer = await db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID);

                if (customer != null && customer.LastRandomDishPos != null)
                    res = (int)customer.LastRandomDishPos;

                return res;
            }
        }

        /// <summary>
        /// Обновляет ID последнего рандомного блюда в базе
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <param name="Pos">значение параметра</param>
        public static async Task SetLastRandomDishPos(long customerID, int Pos)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID).Result;

                if(customer != null) customer.LastRandomDishPos = Pos;

                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Получает текущее состояние пользователя
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <returns>Экземпляр CustomerState, с информацией о состоянии</returns>
        public static async Task<CustomerState> GetCustomerState(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var res = CustomerState.None;

                var customer = db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID).Result;

                if (customer != null) res = (CustomerState)customer.StateID;

                await Console.Out.WriteLineAsync($"РЕЗУЛЬТАТ STATE {res.ToString()}");

                return res;
            }
        }

        /// <summary>
        /// Обновляет текущее состояние пользователя в базе
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <param name="customerState">состояние пользователя</param>
        public static async Task SetCustomerState(long customerID, CustomerState customerState)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID).Result;

                if (customer != null) customer.StateID = (int)customerState;

                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Добавляет пользователя в базу
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <param name="customerName">Никнейм пользователя</param>
        /// <param name="timeStamp">Дата и время добавления</param>
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

        /// <summary>
        /// Удаляет пользователя из базы
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        public static async Task DeleteCustomer(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID).Result;

                if(customer != null) db.Customers.Remove(customer);

                await db.SaveChangesAsync();

                await Console.Out.WriteLineAsync($"CUSTOMER DELETED");
            }
        }

        /// <summary>
        /// Получает все никнеймы пользователей из базы
        /// </summary>
        /// <returns>Список с именами пользователей</returns>
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

        /// <summary>
        /// Обновляет дату последнего использования бота в базе
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        public static async Task UpdateCustomerLastDate(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var customer = db.Customers.FirstOrDefaultAsync(cust => cust.CustomerID == customerID).Result;

                if(customer!= null) customer.LastDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                
                await db.SaveChangesAsync();

                await Console.Out.WriteLineAsync("UPDATED");
            }
        }

        //********************************************************************//
        //**                              DISH                              **//
        //********************************************************************//

        /// <summary>
        /// Добавляет новое блюдо в базу
        /// </summary>
        /// <param name="dish">Экземпляр с информацией о новом блюде</param>
        public static async Task AddDish(Dish dish)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                db.Dishes.Add(dish);
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Считает количество блюд пользователя в базе
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <returns>int с количеством блюд пользователя</returns>
        public static async Task<int> CountAllCustomerDishes(long customerID)
        {
            //Возвращение количества блюд пользователя
            using (ApplicationContext db = new ApplicationContext())
            {
                return db.Dishes.Where(dish => dish.CustomerID == customerID).ToList().Count();
            }
        }

        /// <summary>
        /// Получает в базе список всех блюд пользователя
        /// </summary>
        /// <param name="customerID">ID пользователя в базе</param>
        /// <returns>Список со всеми экземплярами Dish принадлежащих пользователю</returns>
        public static async Task<List<Dish>> GetAllDishes(long customerID)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                return db.Dishes.Where(dish => dish.CustomerID == customerID).ToList();
            }
        }

        /// <summary>
        /// Получает блюдо по его ID
        /// </summary>
        /// <param name="dishId">ID блюда в базе</param>
        /// <returns>Экземпляр блюда из базы, null если блюдо не существует</returns>
        public static async Task<Dish> GetDishById(int dishId)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var dish = await db.Dishes.FirstOrDefaultAsync(dish => dish.DishID == dishId);

                return dish;
            }
        }

        /// <summary>
        /// Удаляет блюдо по его ID
        /// </summary>
        /// <param name="dishId">ID блюда в базе</param>
        public static async Task DeleteDishById(int dishId)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                var dishToDelete = await db.Dishes.FirstOrDefaultAsync(dish => dish.DishID == dishId);

                db.Dishes.Remove(dishToDelete);

                db.SaveChanges();
            }
        }

        /// <summary>
        /// Обновляет блюдо в базе
        /// </summary>
        /// <param name="dish">Обновлённый экземпляр блюда</param>
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
