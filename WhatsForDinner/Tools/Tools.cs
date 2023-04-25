using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.RandomService
{
    public static class Tools
    {
        private static Random rnd = new Random();
        public static async Task<int> GetRandomNumber(int LastIndex, int LastGeneratedNumber)
        {
            int randomNumber = rnd.Next(0, LastIndex);

            if (randomNumber == LastGeneratedNumber)
            {
                return await GetRandomNumber(LastIndex, LastGeneratedNumber);
            }

            return randomNumber;
        }

        public static string GetDishDayTimeStr(int dishDayTimeID)
        {
            switch (dishDayTimeID)
            {
                case 1:
                    return "Завтрак";
                case 2:
                    return "Обед";
                case 3:
                    return "Ужин";
                default:
                    return "Любой";
            }
        }
    }
}
