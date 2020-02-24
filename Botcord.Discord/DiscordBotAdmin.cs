using Botcord.Core;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Botcord.Discord
{
    public class DiscordBotAdmin : Singleton<DiscordBotAdmin>
    {
        public SocketUser Admin
        {
            get
            {
                if (m_admin == null)
                    Initalise(m_host, m_adminId);

                return m_admin;
            }
        }

        private static bool s_adminSet = false;

        private SocketUser  m_admin;
        private DiscordSocketClient m_host;
        private ulong m_adminId = 0;

        public bool Initalise(DiscordSocketClient host, ulong adminId)
        {
            if(s_adminSet)
            {
                Logging.LogWarn(LogType.Bot, $"Admin already set to {m_adminId}. Can't be set more than once.");
                return false;
            }

            m_host = host;
            m_adminId = adminId;

            if(m_host != null && m_adminId != 0)
            {
                m_admin = m_host.GetUser(m_adminId);
                if(m_admin != null)
                {
                    m_host.Disconnected += host_Disconnected;

                    s_adminSet = true;
                    Logging.LogInfo(LogType.Bot, $"Admin set to user {m_admin.Username} ({m_adminId}).");
                    return true;
                }
                else
                {
                    Logging.LogWarn(LogType.Bot, $"Could not find admin user {m_adminId}.");
                    return false;
                }
            }
            else
            {
                Logging.LogWarn(LogType.Bot, $"Host or Admin Id was invalid while setting admin.");
                return false;
            }
        }

        private Task host_Disconnected(Exception arg)
        {
            m_host      = null;
            m_admin     = null;
            m_adminId   = 0;
            s_adminSet  = false;

            return Task.CompletedTask;
        }
    }
}
