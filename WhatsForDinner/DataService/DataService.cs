using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using WhatsForDinner.Config;

namespace WhatsForDinner.DataService
{
    public static class DataService
    {
        public static string ConnectionString { get; set; } = "";

        public static async Task<bool> IsCustomerExists(long CustomerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"SELECT * FROM CUSTOMER WHERE CUSTOMERID LIKE '{CustomerID}'";

                var reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    // выводим названия столбцов
                    string columnName1 = reader.GetName(0);
                    string columnName2 = reader.GetName(1);
                    string columnName3 = reader.GetName(2);

                    Console.WriteLine($"{columnName1}\t{columnName3}\t{columnName2}");

                    while (await reader.ReadAsync()) // построчно считываем данные
                    {
                        object id = reader.GetValue(0);
                        object name = reader.GetValue(2);
                        object enterDate = reader.GetValue(1);

                        Console.WriteLine($"{id} \t{name} \t{enterDate}");
                    }
                }

                await reader.CloseAsync();

                return await command.ExecuteScalarAsync() != null;
            }
        }

        public static async Task AddCustomer(long customerID, string customerName, string timeStamp)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"INSERT INTO CUSTOMER (CUSTOMERID,CUSTOMERNAME,ENTERDATE,LASTDATE) VALUES ({customerID}, '{customerName}', '{timeStamp}', '{timeStamp}')";

                await Console.Out.WriteLineAsync($"КОРОЧЕ VALUES ({customerID}, {customerName}, {timeStamp}, {timeStamp}");

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task DeleteCustomer(long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"DELETE FROM CUSTOMER WHERE CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync($"CUSTOMER DELETED");

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task<List<string>> GetAllCustomers()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                var clientNames = new List<string>();

                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = "SELECT CUSTOMERNAME FROM CUSTOMER";
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    clientNames.Add(reader.GetString(0));
                }

                await reader.CloseAsync();

                return clientNames;
            }
        }

        public static async Task UpdateCustomerLastDate(long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"UPDATE CUSTOMER " +
                                      $"SET LASTDATE = '{DateTime.Now.ToString("yyyy-MM-dd HH:mm")}'" +
                                      $"WHERE CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync("UPDATED");

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task AddDish(string dishName, string dishDescription, string dishRecipe, string dishPhoto, long ownerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;

                //DISH PHOTO ВРЕМЕННО NULL
                command.CommandText = $"INSERT INTO DISH (DISHNAME,DISHDESCRIPTION,DISHRECIPE,DISHPHOTO,CUSTOMERID) " +
                                      $"VALUES ('{dishName}', '{dishDescription}', '{dishRecipe}', NULL, {ownerID})";

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task UpdateDish(int DishID, string dishName, string dishDescription, string dishRecipe, string dishPhoto, long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;

                //DISH PHOTO ВРЕМЕННО NULL
                command.CommandText = $"UPDATE DISH " +
                                      $"SET DISHNAME = '{dishName}'," +
                                          $"DISHDESCRIPTION = '{dishDescription}'," +
                                          $"DISHRECIPE = '{dishRecipe}'," +
                                          $"DISHPHOTO = NULL," +
                                          $"CUSTOMERID = {customerID}) " +
                                      $"WHERE DISHID = {DishID}";

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task<List<string>> GetAllDishes()
        {
            //Возвращение всех блюд
            return new List<string>();
        }

        public static async Task<List<string>> GetAllDishesOfCustomer()
        {
            //Возвращение всех блюд конкретного пользователя
            return new List<string>();
        }
    }
}
