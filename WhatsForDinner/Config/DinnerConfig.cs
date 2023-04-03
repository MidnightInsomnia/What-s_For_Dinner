using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsForDinner.Config
{
    public static class DinnerConfig
    {
        public static IConfiguration AppConfiguration { get; private set; }
        public static void Initiation()
        {
            AppConfiguration = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();
        }
    }
}
