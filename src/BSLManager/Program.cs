using System;
using System.Text;
using BSLManager.Tools;

namespace BSLManager;

internal static class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Default;

        var settingsManager = new SettingsManager();
        var settings = settingsManager.GetSettings();

        //var builder = new ConfigurationBuilder()
        //    .AddEnvironmentVariables();
        //var configuration = builder.Build();

        //var dbSettings = configuration.GetSection("Db").Get<DbSettings>();
        //var instanceSettings = configuration.GetSection("Instance").Get<InstanceSettings>();

        var bootstrapper = new Bootstrapper(settings.dbSettings, settings.instanceSettings);
        var container = bootstrapper.Init();

        var app = container.GetInstance<App>();
        app.Run();
    }
}