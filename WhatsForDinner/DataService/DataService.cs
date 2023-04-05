using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

        public static async Task InitAllUserStates()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"UPDATE CUSTOMER " +
                                      $"SET STATEID = {(int)CustomerState.None}";

                await Console.Out.WriteLineAsync($"КОРОЧЕ КОМАНДА {command.CommandText}");

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task<int> GetLastRandomGeneratedPos(long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                int res = 0;

                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"SELECT LASTRANDOMDISHPOS " +
                                      $"FROM CUSTOMER " +
                                      $"WHERE CUSTOMER.CUSTOMERID = {customerID}";

                await command.ExecuteNonQueryAsync();

                var reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    while (await reader.ReadAsync())
                    {
                        res = reader.GetInt32(0);
                    }
                }

                return res;
            }
        }

        public static async Task SetLastRandomGeneratedPos(long customerID, int Pos)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"UPDATE CUSTOMER " +
                                      $"SET LASTRANDOMDISHPOS = {Pos} " +
                                      $"WHERE CUSTOMER.CUSTOMERID = {customerID}";

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task<bool> IsCustomerExists(long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"SELECT * FROM CUSTOMER WHERE CUSTOMERID LIKE '{customerID}'";

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

        public static async Task<CustomerState> GetCustomerState(long customerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                var res = CustomerState.None;
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"SELECT STATEID " +
                                      $"FROM CUSTOMER " +
                                      $"WHERE CUSTOMER.CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync($"КОРОЧЕ КОМАНДА {command.CommandText}");

                await command.ExecuteNonQueryAsync();

                var reader = command.ExecuteReader();

                if(reader.HasRows)
                {
                    while (reader.Read())
                    {
                        res = (CustomerState)reader.GetInt32(0);
                    }
                }

                await reader.CloseAsync();

                await Console.Out.WriteLineAsync($"РЕЗУЛЬТАТ STATE {res.ToString()}");

                return res;
            }
        }

        public static async Task SetCustomerState(long customerID, CustomerState customerState)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;
                command.CommandText = $"UPDATE CUSTOMER " +
                                      $"SET STATEID = {(int)customerState} " +
                                      $"WHERE CUSTOMER.CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync($"КОРОЧЕ КОМАНДА {command.CommandText}");

                await command.ExecuteNonQueryAsync();
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

        public static async Task AddDish(string dishName, string dishDescription, string dishRecipe, string dishPhotoBase64, long ownerID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;

                //DISH PHOTO ВРЕМЕННО NULL
                command.CommandText = $"INSERT INTO DISH (DISHNAME,DISHDESCRIPTION,DISHRECIPE,DISHPHOTO,CUSTOMERID) " +
                                      $"VALUES ('{dishName}', '{dishDescription}', '{dishRecipe}', '{dishPhotoBase64}', {ownerID})";

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

        public static async Task<int> CountAllCustomerDishes(long customerID)
        {
            //Возвращение количества блюд пользователя
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                int res = 0;
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;

                //DISH PHOTO ВРЕМЕННО NULL
                command.CommandText = $"SELECT COUNT(*) " +
                                      $"FROM DISH " +
                                      $"WHERE DISH.CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync("КОМАНДА ТИПА: " + command.CommandText);

                await command.ExecuteNonQueryAsync();

                var reader = command.ExecuteReader();

                if (reader.HasRows)
                {
                    while(reader.Read())
                    {
                        res = reader.GetInt32(0);

                    }
                }

                await reader.CloseAsync();

                return res;
            }
        }

        //Возвращение всех блюд
        public static async Task<List<Dish>> GetAllDishes(long customerID)
        {
            var res = new List<Dish>();
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand();

                command.Connection = connection;

                //DISH PHOTO ВРЕМЕННО NULL
                command.CommandText = $"SELECT * " +
                                      $"FROM DISH " +
                                      $"WHERE DISH.CUSTOMERID = {customerID}";

                await Console.Out.WriteLineAsync("КОМАНДА ТИПА: " + command.CommandText);

                await command.ExecuteNonQueryAsync();

                var reader = command.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var tmpDish = new Dish(reader.GetString(1));
                        tmpDish.dishId = reader.GetInt32(0);
                        tmpDish.dishDescription = reader.GetString(2);
                        tmpDish.dishRecipe = reader.GetString(3);
                        //ТЕСТ ФОТО
                        if (!reader.IsDBNull(4))
                            tmpDish.dishPhotoBase64 = reader.GetString(4);

                        res.Add(tmpDish);
                    }
                }

                await reader.CloseAsync();

                return res;
            }
        }

        public static async Task<List<string>> GetAllDishesOfCustomer()
        {
            //Возвращение всех блюд конкретного пользователя
            return new List<string>();
        }

        private static byte[] ConvertVarbinaryToBytes(string varbinaryStr)
        {
            char[] charArray = varbinaryStr.ToCharArray();
            byte[] byteArray = new byte[charArray.Length];

            for (int i = 0; i < charArray.Length; i++)
            {
                byteArray[i] = (byte)charArray[i];
            }

            return byteArray;
        }
    }
}
