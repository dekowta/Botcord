using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotConsole
{
    public class Program
    {
        private static string help = "  -target=<name>: name of the process to ensure is running \n" +
            "   \"-args=<targetArgs>\": Arguments for the target (should be quoted) \n" +
            "   -timeout=<time>: time to wait before rebooting the boot (Optional) \n" +
            "   -help: display this message \n";

        private static bool enableReboot = true;
        private static int botTimeout = 30000;
        private static string botProcessTarget = string.Empty;
        private static string botProcessArgs = string.Empty;
        private static Process botProcess;
        private static StreamWriter botInputStream;
        private static CancellationTokenSource tokenSource;
        private static TaskCompletionSource<bool> completionSource;
        static int Main(string[] args)
        {
            if(args.Contains("-help"))
            {
                Console.WriteLine(help);
                WaitAnyKey();
                return 0;
            }

            string executable;
            if (TryGetToken(args, "-target", out executable))
            {
                if(!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
                {
                    botProcessTarget = executable;
                    Console.WriteLine($"Target set to '{botProcessTarget}'");

                    string arguments;
                    if (TryGetToken(args, "-args", out arguments))
                    {
                        botProcessArgs = arguments;
                        Console.WriteLine($"Arguments set to '{botProcessArgs}'");
                    }

                    int timeout;
                    if(TryGetToken(args, "-timeout", out timeout))
                    {
                        botTimeout = timeout;
                        Console.WriteLine($"Timeout set to '{botTimeout}'");
                    }
                    
                    StartBot(true).Wait();
                }
            }
            else
            {
                Console.WriteLine("Taget tag is missing or set wrong");
                Console.WriteLine(help);
                WaitAnyKey();
                return -1;
            }

            return -1;
        }

        public static async Task StartBot(bool firstBoot)
        {
            while (enableReboot)
            {
                tokenSource = new CancellationTokenSource();

                string workingDir = Path.GetDirectoryName(Path.GetFullPath(botProcessTarget));

                botProcess = new Process();
                botProcess.StartInfo.FileName = botProcessTarget;
                botProcess.StartInfo.Arguments = botProcessArgs;
                botProcess.StartInfo.WorkingDirectory = workingDir;
                botProcess.StartInfo.UseShellExecute = false;
                botProcess.StartInfo.CreateNoWindow = false;
                botProcess.StartInfo.RedirectStandardInput = true;
                botProcess.Start();
                botInputStream = botProcess.StandardInput;

                var processMonitor = Task.Run(PipeReadAync);

                botProcess.WaitForExit();

                tokenSource.Cancel();
                processMonitor.Wait();
                tokenSource = null;
                botInputStream.Close();

                if (botProcess.ExitCode != 0 && firstBoot)
                {
                    enableReboot = false;
                }
                else
                {
                    firstBoot = false;
                    await Task.Delay(botTimeout);
                }
            }
        }

        private static async void PipeReadAync()
        {
            completionSource = new TaskCompletionSource<bool>();

            tokenSource.Token.Register(() =>
            {
                completionSource.TrySetCanceled();
            });

            await Task.WhenAny(Task.Run(PipeCommand, tokenSource.Token), completionSource.Task);
        }

        public static bool PipeCommand()
        {
            while (true)
            {
                string inputText = Console.ReadLine();
                if (inputText.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
                {
                    botProcess.Kill();
                    enableReboot = false;
                    tokenSource.Cancel();
                    break;
                }
                else
                {
                    if (inputText.Length > 0)
                    {
                        botInputStream.WriteLine(inputText);
                        botInputStream.Flush();
                    }
                }
            }

            return true;
        }

        private static bool TryGetToken<T>(string[] args, string tag, out T value)
        {
            value = default;

            string content = args.FirstOrDefault(arg => arg.StartsWith(tag));
            string[] parts = content.Split(new[] { '=' }, 2);
            if(parts.Length > 1)
            {
                try
                {
                    TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof(T));
                    value = (T)typeConverter.ConvertFromString(parts[1]);
                    return true;
                }
                catch
                {
                    
                }
            }
            return false;
        }

        private static bool WaitAnyKey()
        {
            Console.WriteLine("Type 'exit' to quit... ");
            string text = Console.ReadLine();
            if (text.Equals("exit", StringComparison.Ordinal))
                return true;

            return false;
        }
    }
}
