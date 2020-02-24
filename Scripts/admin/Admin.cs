using Botcord.Discord;
using Botcord.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;
using Discord.WebSocket;
using Botcord.Core.Extensions;
using Discord;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

//ASM: System.IO.FileSystem; System.IO;
namespace DiscordSharp.CSharp
{
    public class AttachmentRuleTest : IDiscordRule
    {
        public Dictionary<string, object> BuildParameters(object e)
        {
            return new Dictionary<string, object>() { ["test"] = true };
        }

        public bool IsEventSupported(DiscordEventType eventType)
        {
            return eventType == DiscordEventType.PrivateMessageRecieved;
        }

        public bool Validate(object e)
        {
            if (e is SocketMessage)
            {
                SocketMessage msg = e as SocketMessage;
                if (msg.Author == DiscordHost.Instance.ThisBot &&
                    msg.Attachments.Count > 0 && 
                    msg.Content.Equals("unit_test"))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class Admin : IDiscordScript
    {
        public string Name
        {
            get { return "Admin 1.0"; }
        }


        private DiscordScriptHost m_host;

        private static AttachmentRuleTest attachRule = new AttachmentRuleTest();

        public void Initalise(DiscordScriptHost ActiveHost)
        {
            m_host = ActiveHost;
        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            ActiveHost.RegisterAdminCommand(DiscordAdmin.DM, "admin", "ping", "Test the bots ping with discord", Ping);
            ActiveHost.RegisterAdminCommand(DiscordAdmin.DM, "admin", "server (list|leave <id>)", "Server information", ServerInfo);
            ActiveHost.RegisterAdminCommand(DiscordAdmin.DM, "admin", "set (((name|playing) <value>) | avatar)", "Sets one of the settings", Set);
            ActiveHost.RegisterAdminCommand(DiscordAdmin.DM, "admin", "get (avatar [<size>])", "Gets one of the settings", Get);
            ActiveHost.RegisterAdminCommand(DiscordAdmin.DM, "admin", "test [exception|download|disconnect]", "Run Bot Unit Tests", AdminTests);
            ActiveHost.RegisterAdminEvent<SocketMessage>(DiscordAdmin.DM, attachRule, DiscordEventType.PrivateMessageRecieved, AdminTests);
        }

        public void Dispose()
        {
        }

        public void Ping(Dictionary<string, object> parameters, SocketMessage e)
        { 
            e.Channel.SendMessageAsync($":tools: | Bot Ping is {DiscordHost.Instance.Client.Latency}ms");
        }

        public void ServerInfo(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (parameters.ContainsKey("list"))
            {
                IReadOnlyCollection<SocketGuild> guilds = DiscordHost.Instance.Client.Guilds;
                guilds.BuildCustomEmbed((guild) =>
                {
                    return $"{guild.Name.Truncate(50)} - {guild.Id} - {guild.Owner.Username.Truncate(50)}";
                }, "Servers", e.Channel);
            }
            else if(parameters.ContainsKey("leave") && parameters.ContainsKey("<id>"))
            {
                ValueObject idValue = parameters["<id>"] as ValueObject;
                if(!idValue.IsULong)
                {
                    e.Channel.SendMessageAsync($":warning: | Id was not a ulong type");
                    return;
                }

                ulong id = idValue.AsULong;
                SocketGuild guild = DiscordHost.Instance.Client.GetGuild(id);
                if(guild == null)
                {
                    e.Channel.SendMessageAsync($":warning: | No Guild with id {id} found.");
                    return;
                }

                string name = guild.Name;
                guild.LeaveAsync();

                e.Channel.SendMessageAsync($":tools: | Left Guild {name.Truncate(50)} ({id})");
            }
        }

        public async void Set(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (parameters.ContainsKey("name"))
            {
                if (!parameters.ContainsKey("<value>"))
                    return;

                ValueObject value = parameters["<value>"] as ValueObject;

                string name = value.ToString().Truncate(50);
                if (!string.IsNullOrEmpty(name))
                {
                    string nameBefore = DiscordHost.Instance.ThisBot.Username;
                    await DiscordHost.Instance.ThisBot.ModifyAsync((prop) =>
                    {
                        prop.Username = name;
                    });

                    await e.Channel.SendMessageAsync($":tools: | Changed User name from `{nameBefore}` to `{name}`");
                    return;
                }
                else
                {
                    await e.Channel.SendMessageAsync($":warning: | Name was empty");
                    return;
                }
            }
            else if (parameters.ContainsKey("avatar"))
            {
                if (e.Attachments.Count == 0)
                {
                    await e.Channel.SendMessageAsync($":warning: | No attchment Found");
                    return;
                }
                else if(e.Attachments.Count >= 1)
                {
                    Attachment first = e.Attachments.First();
                    string downloadLocation = Utilities.TempFilePath(".png");
                    if(first.Width == null || first.Height == null)
                    {
                        await e.Channel.SendMessageAsync($":warning: | Attchment is not an image");
                        return;
                    }
                    else if(first.Width > 1024 || first.Height > 1024)
                    {
                        await e.Channel.SendMessageAsync($":warning: | image bigger than 1024x1024");
                        return;
                    }
                    else if (first.Width < 32 || first.Height < 32)
                    {
                        await e.Channel.SendMessageAsync($":warning: | image smaller than 32x32");
                        return;
                    }

                    bool downloaded = await TryDownloadFile(first.Url, downloadLocation, 4.MiB(), e.Channel);
                    if (downloaded == true)
                    {
                        await DiscordHost.Instance.ThisBot.ModifyAsync((prop) =>
                        {
                            prop.Avatar = new Image(downloadLocation);
                        });

                        File.Delete(downloadLocation);
                        await e.Channel.SendMessageAsync($":tools: | Avatar Changed");
                    }
                }
            }
            else if (parameters.ContainsKey("playing"))
            {
                if (!parameters.ContainsKey("<value>"))
                    return;

                ValueObject value = parameters["<value>"] as ValueObject;

                string text = value.ToString().Truncate(50);
                if (!string.IsNullOrEmpty(text))
                {
                    await DiscordHost.Instance.Client.SetGameAsync(text);
                    await e.Channel.SendMessageAsync($":tools: | Changed playing text to `{text}`");
                }
            }
        }

        public async void Get(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (parameters.ContainsKey("avatar"))
            {
                ushort size = 128;
                if(parameters.ContainsKey("<size>"))
                {
                    ValueObject sizeValue = parameters["<size>"] as ValueObject;
                    if(sizeValue.IsInt)
                    {
                        size = (ushort)Utilities.Clamp(32, 128, sizeValue.AsInt);
                    }
                }

                SocketSelfUser bot = DiscordHost.Instance.ThisBot;
                string avatarUrl = bot.GetAvatarUrl(ImageFormat.Png, size);
                string downloadLocation = Utilities.TempFilePath(".png");
                bool downloaded = await TryDownloadFile(avatarUrl, downloadLocation, 4.MiB(), e.Channel);
                if (downloaded == true)
                {
                    await e.Channel.SendFileAsync(downloadLocation, $":tools: | Current Avatar at {size}x{size}");
                    File.Delete(downloadLocation);
                }
            }
        }

        public async void AdminTests(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (parameters.ContainsKey("exception"))
            {
                try
                {
                    await e.Channel.SendMessageAsync(":tools: | Running Exception Test.");
                    UnitTestThrow();
                }
                catch(Exception ex)
                {
                    Logging.LogException(LogType.Script, ex, "Unit Test Exception");
                }

                await e.Channel.SendMessageAsync(":tools: | Run Exception Test Complete.");
            }

            if(parameters.ContainsKey("download"))
            {
                string unitTestFile = Path.Combine(Utilities.DataFolder, "unit_test.png");
                await e.Channel.SendFileAsync(unitTestFile, "unit_test");
            }

            if(parameters.ContainsKey("disconnect"))
            {
                await e.Channel.SendMessageAsync(":tools: | Logging out.");
                await DiscordHost.Instance.Logout();
                await Task.Delay(5.Second());
                await DiscordHost.Instance.Login();
                await Task.Delay(5.Second());
                await e.Channel.SendMessageAsync(":tools: | Logged in.");
            }

            if(parameters.ContainsKey("test") && parameters["test"] is bool)
            {
                if(e.Attachments.Count >= 1)
                {
                    Attachment first = e.Attachments.First();
                    string downloadLocation = Utilities.TempFilePath(".png");
                    bool downloaded = await TryDownloadFile(first.Url, downloadLocation, 100.KiB(), e.Channel);
                    if (downloaded)
                    {
                        await e.Channel.SendMessageAsync($":tools: | Download Test Completed successfully");
                        File.Delete(downloadLocation);
                    }
                    else
                    {
                        await e.Channel.SendMessageAsync($":warning: | Test Failed file could not be downloaded");
                    }
                }
                else
                {
                    await e.Channel.SendMessageAsync($":warning: | Test Failed No attachment sent");
                }
            }

        }

        private void UnitTestThrow()
        {
            throw new Exception("This is a admin unit test exception");
        }

        private bool IsInServer()
        {
            return m_host.Guild != null;
        }

        private async Task<bool> TryDownloadFile(string link, string downloadLocation, int fileSizeLimit, ISocketMessageChannel logger)
        {
            if (!link.StartsWith("http:") && !link.StartsWith("https:"))
            {
                Logging.LogError(LogType.Script, $"Cant download file {link} as its not a http:// or https:// link");
                await logger.SendMessageAsync($":warning: | Cant download file {link} as its not a http:// or https:// link");
                return false;
            }

            if (File.Exists(downloadLocation))
            {
                File.Delete(downloadLocation);
            }

            try
            {
                CancellationTokenSource token = new CancellationTokenSource();
                bool downloaded = await Utilities.TryDownloadFileAsync(link, downloadLocation, fileSizeLimit, token.Token);
                if (!downloaded)
                {
                    Logging.LogError(LogType.Script, $"File failed to download (file size most likely)");
                    await logger.SendMessageAsync($":warning: | Failed to download file (Is the file size too big > {fileSizeLimit}).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Script, ex, $"Something went wrong while trying to download file '{link}'");
                await logger.SendMessageAsync($":warning: | Something went wrong while trying to download file.");
                return false;
            }

            return true;
        }
    }
}