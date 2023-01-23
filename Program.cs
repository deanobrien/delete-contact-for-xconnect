using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DeleteContact
{
    class Program
    {
        static void Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddXmlFile("settings.xml", false);
            var config = configBuilder.Build();

            var app = new Application(config);
            Task.Run(() => app.RunAsync()).Wait();
        }
    }
}
