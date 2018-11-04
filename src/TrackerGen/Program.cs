using System;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Internal.Utilities;

namespace TrackerGen
{
    [Command("TrackerGen", Description = "Generates data and site for ProdConTracker")]
    [Subcommand(ImportCommand.Name, typeof(ImportCommand))]
    public class Program
    {
        public static int Main(string[] args)
        {
#if DEBUG
            DebugHelper.HandleDebugSwitch(ref args);
#endif

            var services = new ServiceCollection();
            ConfigureServices(services);
            var container = services.BuildServiceProvider();
            var logger = container.GetRequiredService<ILogger<Program>>();

            var app = new CommandLineApplication<Program>();
            app.Conventions.UseDefaultConventions().UseConstructorInjection(container);
            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred");
                return 1;
            }
        }

        private int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 0;
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(logging =>
            {
                logging.AddCliConsole();
            });

            services.AddSingleton<ToolSet>();
        }
    }
}
