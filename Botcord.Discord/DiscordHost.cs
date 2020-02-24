using Botcord.Core;
using Botcord.Core.Extensions;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Botcord.Discord
{
    public class DiscordHost : Singleton<DiscordHost>
    {
        public DiscordSocketClient Client
        {
            get { return m_client; }
        }

        public SocketSelfUser ThisBot
        {
            get
            {
                if(m_client != null)
                {
                    return m_client.CurrentUser;
                }

                return null;
            }
        }

        private DiscordScriptManager m_scriptManager;
        private DiscordSocketClient m_client;

        private ulong m_debugId = 0;
        private ulong m_adminId = 0;

        private string m_botToken = string.Empty;

        public async Task<bool> Initalise(Dictionary<string, object> args)
        {
            DiscordCommandLineArgs.CommandKey = '!';

            m_scriptManager = new DiscordScriptManager();

            int timeout = 30.Second();
            var timeoutArg = args.GetValue<ValueObject>("-timeout");
            if (timeoutArg != null && timeoutArg.IsInt)
                timeout = timeoutArg.AsInt;

            m_client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                //ConnectionTimeout = timeout
                ConnectionTimeout = timeout,
                //HandlerTimeout = 5.Second()
                //LogLevel = LogSeverity.Debug
            });


            m_client.Log += client_Log;

            var tokenArg = args.GetValue<ValueObject>("-token");
            if (tokenArg != null && tokenArg.IsString)
            {
                m_botToken = tokenArg.ToString();
            }
            else
            {
                return false;
            }

            m_botToken = tokenArg.ToString();

            var adminId = args.GetValue<ValueObject>("-admin");
            if(adminId != null && adminId.IsULong)
            {
                m_adminId = adminId.AsULong;
            }

            var debugId = args.GetValue<ValueObject>("-debug");
            if(debugId != null && debugId.IsULong)
            {
                m_debugId = debugId.AsULong;
            }

            m_client.Connected      += client_Connected;
            m_client.Disconnected   += client_Disconnected;
            await m_client.LoginAsync(TokenType.Bot, m_botToken);
            await m_client.StartAsync();

            return true;
        }

        public async Task Logout()
        {
            await m_client.LogoutAsync();
        }

        public async Task Login()
        {
            await m_client.LoginAsync(TokenType.Bot, m_botToken);
            await m_client.StartAsync();
        }

        private Task client_Connected()
        {
            if(m_adminId != 0)
            {
                DiscordBotAdmin.Instance.Initalise(m_client, m_adminId);
            }

            if(m_debugId != 0)
            {
                DiscordLogger.Instance.Initalise(m_client, m_debugId);
            }

            Logging.Log(LogType.Discord, LogLevel.Debug, "Client signaled Connected");
            Utilities.ExecuteAndWait(() => m_scriptManager.Initalise(m_client));

            return Task.CompletedTask;
        }

        private Task client_Disconnected(Exception arg)
        {
            Logging.Log(LogType.Discord, LogLevel.Debug, "Client signaled Disconnected");
            Utilities.ExecuteAndWait(() => m_scriptManager.Uninitalise());

            return Task.CompletedTask;
        }

        private Task client_Log(LogMessage arg)
        {
            Logging.Log(LogType.Discord, LogLevel.Debug, arg.Message);
            return Task.CompletedTask;
        }


    }
}
