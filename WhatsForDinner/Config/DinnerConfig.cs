using Microsoft.Extensions.Configuration;

namespace WhatsForDinner.Config;

//Единственная функция - добавляет sectets.json в config
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
