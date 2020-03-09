using Botcord.Core;
using Botcord.Core.Extensions;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Botcord.Discord
{
    /// <summary>
    /// The Host for discord which manages
    /// the logging system and script manager
    /// </summary>
    public class DiscordHost : Singleton<DiscordHost>
    {
        /// <summary>
        /// The Current Discord Client
        /// </summary>
        public DiscordSocketClient Client
        {
            get { return m_client; }
        }

        /// <summary>
        /// The current bot user
        /// </summary>
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

        /// <summary>
        /// Initalise the the Host
        /// </summary>
        /// <param name="args">The launch arguments</param>
        /// <returns>true if the host has been initlaised correctly</returns>
        public async Task<bool> Initalise(Dictionary<string, object> args)
        {
            Logging.LogInfo(LogType.Bot, "Initalising the Host");

            var commandKey = args.GetValue<ValueObject>("-commandkey");
            if (commandKey != null && !commandKey.IsNullOrEmpty)
            {
                string key = commandKey.ToString();
                if(Regex.IsMatch(key, @"^(\S|\d|\w)"))
                {
                    DiscordCommandLineArgs.CommandKey = key[0];
                }
            }

            m_scriptManager = new DiscordScriptManager();

            int timeout = 30.Second();
            var timeoutArg = args.GetValue<ValueObject>("-timeout");
            if (timeoutArg != null && timeoutArg.IsInt)
                timeout = timeoutArg.AsInt;

            m_client = new DiscordSocketClient(new DiscordSocketConfig()
            {
               // LogLevel = LogSeverity.Verbose,
                ConnectionTimeout = timeout,
            });


            m_client.Log += client_Log;

            var tokenArg = args.GetValue<ValueObject>("-token");
            if (tokenArg != null && tokenArg.IsString)
            {
                m_botToken = tokenArg.ToString();
            }
            else
            {
                Logging.LogError(LogType.Bot, "Discord Bot Token not set. Get a token at (https://discordapp.com/developers/applications)");
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

            Logging.LogInfo(LogType.Bot, "Starting Script Manager");
            await m_scriptManager.Initalise(m_client);

            m_client.Connected      += client_Connected;
            m_client.Disconnected   += client_Disconnected;
            await Login();

            return true;
        }

        /// <summary>
        /// Log the bot into discord
        /// </summary>
        public async Task Login()
        {
            Logging.LogInfo(LogType.Bot, "Logging Bot Discord Client in.");
            await m_client.LoginAsync(TokenType.Bot, m_botToken);
            Logging.LogInfo(LogType.Bot, "Starting Bot Discord Client");
            await m_client.StartAsync();
        }

        /// <summary>
        /// Logout the bot from discord
        /// </summary>
        public async Task Logout()
        {
            Logging.LogInfo(LogType.Bot, "Logging out Bot Discord Client");
            await m_client.LogoutAsync();
        }

        private async Task client_Connected()
        {
            Logging.LogDebug(LogType.Discord, "Client signaled Connected");

            if (m_adminId != 0)
            {
                Logging.LogInfo(LogType.Bot, $"Starting Bot Admin with user id {m_adminId}.");
                DiscordBotAdmin.Instance.Initalise(m_client, m_adminId);
            }

            //disabiling for now as it looks like it can lead to swamping of the thread pool
            //if(m_debugId != 0)
            //{
            //    Logging.LogInfo(LogType.Bot, $"Starting Bot Discord Logging with id {m_debugId}.");
            //    DiscordLogger.Instance.Initalise(m_client, m_debugId);
            //}

            Logging.LogInfo(LogType.Bot, "Starting Script Manager");
            await m_scriptManager.Initalise(m_client);
        }

        private async Task client_Disconnected(Exception arg)
        {
            Logging.Log(LogType.Discord, LogLevel.Debug, "Client signaled Disconnected");

            Logging.LogInfo(LogType.Bot, "Uninitalising Script Manager");
            await m_scriptManager.Uninitalise();
        }

        private Task client_Log(LogMessage arg)
        {
            if (arg.Exception == null)
            {
                Logging.Log(LogType.Discord, LogLevel.Debug, arg.Message);
            }
            else
            {
                Logging.LogException(LogType.Discord, arg.Exception, arg.Message);
            }
            return Task.CompletedTask;
        }
    }
}
