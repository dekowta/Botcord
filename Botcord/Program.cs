using Botcord.Core;
using Botcord.Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;

namespace Botcord
{
    class Program
    {
        private static string commandLine = "(-token=<t> -admin=<a> | -compile=<s>) [-timeout=<t>] [-name=<n>] [-debug=<d>] [-help]";
        private static string help = "  -token: The discord bot token https://discordapp.com/login?redirect_to=/developers/applications/me \n" +
            "   -admin: The admin user ID (long number) \n" +
            "   -timeout: Optional Time it takes for a random disconnection to reconnect in ms \n" +
            "   -name: Optional bot name \n" +
            "   -debug: The server Id to log errors to \n" +
            "   -help: will show this message \n";
        private static CommandLineArgs cmdArgs = new CommandLineArgs(commandLine, help);


        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            Console.OutputEncoding = System.Text.Encoding.Unicode;

            if (!cmdArgs.IsMatch(args))
            {
                Logging.LogError(LogType.Bot, "Command Line Arguments " + string.Join(" ", args) + "\nAre Invalid.");
                WaitAnyKey();
                return 1;
            }

            Dictionary<string, object> arguments = cmdArgs.Match(args);
            if (arguments.ContainsKey("-help"))
            {
                Console.WriteLine(cmdArgs.Help);
                WaitAnyKey();
                return 0;
            }

            //Used to test compile scripts without having to run the whole discord system
            if (arguments.ContainsKey("-compile"))
            {
                ValueObject obj = arguments["-compile"] as ValueObject;
                string file = obj.ToString();
                if(File.Exists(file))
                {
                    DiscordScriptManager sm = new DiscordScriptManager();
                    bool success = false;
                    Utilities.ExecuteAndWait(async () =>
                    {
                        success = await sm.TryScriptCompile(file);
                    });

                    if(!success)
                    {
                        return 2;
                    }
                }
                else
                {
                    Logging.LogError(LogType.Bot, "File does not exist to compile.");
                    return 1;
                }

                return 0;
            }

            Utilities.Execute(() => DiscordHost.Instance.Initalise(arguments));

            while (WaitAnyKey())
            {
                System.Threading.Thread.Sleep(5000);
            }

            return 0;
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception)
            {
                Exception ex = e.ExceptionObject as Exception;
                Logging.LogException(LogType.Bot, ex, "Unhandled Exception Occured.");
            }
        }

        public static bool WaitAnyKey()
        {
            Logging.LogInfo(LogType.Bot, "Type 'exit' to quit... ");
            string text = Console.ReadLine();
            if (text.Equals("exit", StringComparison.Ordinal))
                return true;

            return false;
        }
    }
}

