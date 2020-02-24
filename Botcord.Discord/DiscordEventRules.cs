
using Botcord.Core;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Botcord.Discord
{
    public class SocketVoiceConnection
    {
        public SocketUser User { get; private set; }
        public SocketVoiceState OldState { get; private set; }
        public SocketVoiceState NewState { get; private set; }

        public SocketVoiceConnection(SocketUser user, SocketVoiceState old, SocketVoiceState @new)
        {
            User = user;
            OldState = old;
            NewState = @new;
        }
    }


    public interface IDiscordRule
    {
        bool IsEventSupported(DiscordEventType eventType);

        bool Validate(object e);

        Dictionary<string, object> BuildParameters(object e);
    }

    public class DiscordBasicRule : IDiscordRule
    {
        public bool IsEventSupported(DiscordEventType eventType)
        {
            if(eventType != DiscordEventType.MessageRecieved || eventType != DiscordEventType.PrivateMessageRecieved)
            {
                return true;
            }

            return false;
        }

        public Dictionary<string, object> BuildParameters(object e)
        {
            return new Dictionary<string, object>();
        }

        public bool Validate(object e)
        {
            return true;
        }
    }

    public class DiscordMessageRule : IDiscordRule
    { 
        public string Command
        {
            get { return m_triggerCommand; }
            set { m_triggerCommand = value; }
        }

        public string Pattern
        {
            get { return m_pattern; }
        }

        public string Help
        {
            get { return m_help; }
        }

        private string m_triggerCommand;
        private string m_pattern    = string.Empty;
        private string m_help       = string.Empty;
        private DiscordCommandLineArgs m_arguments;


        public DiscordMessageRule(string command, string help)
        {
            m_triggerCommand    = command;
            m_help              = help;
        }

        public DiscordMessageRule(string command, string pattern, string help)
        {
            m_triggerCommand    = command;
            m_pattern           = pattern;
            m_help              = help;
            m_arguments = new DiscordCommandLineArgs(command, pattern, help);
        }

        public bool IsEventSupported(DiscordEventType eventType)
        {
            if (eventType == DiscordEventType.MessageRecieved ||
                eventType == DiscordEventType.PrivateMessageRecieved)
            {
                return true;
            }

            return false;
        }

        public virtual bool Validate(object e)
        {
            bool valid = false;
            if (e is SocketMessage)
            {
                valid = ValidateMessage((SocketMessage)e);
            }
            else
            {
                valid = false;
            }

            return valid;
        }

        public bool ValidateMessage(SocketMessage e)
        {
            string content = e.Content;
            return ValidateContent(content);
        }

        private bool ValidateContent(string content)
        {
            bool valid = false;
            try
            {
                
                if(m_arguments != null)
                {
                    return m_arguments.IsMatch(content);
                }
                else
                {
                    string paddedContent = content + " "; //add padding when arguments arent specified
                    return paddedContent.StartsWith(DiscordCommandLineArgs.CommandKey + Command + " ");
                }
            }
            catch
            {
                valid = false;
            }

            return valid;
        }

        public Dictionary<string, object> BuildParameters(object e)
        {
            if (e is SocketMessage && m_arguments != null)
            {
                return m_arguments.Match(((SocketMessage)e).Content);
            }
            else
            {
                return new Dictionary<string, object>();
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", m_triggerCommand, m_pattern);
        }
    }

    public class DiscordAdminMessageRule : DiscordMessageRule
    {
        public DiscordAdminMessageRule(string command, string help)
           : base(command, help)
        {
        }

        public DiscordAdminMessageRule(string command, string pattern, string help)
            : base(command, pattern, help)
        {
        }

        public override bool Validate(object e)
        {
            if (DiscordBotAdmin.Instance.Admin == null)
            {
                return false;
            }

            if((e is SocketMessage))
            {
                SocketMessage msg = e as SocketMessage;
                SocketUser admin = DiscordBotAdmin.Instance.Admin;
                if (msg.Author.Id == admin.Id)
                {
                    return base.Validate(e);
                }
            }

            return false;
        }
    }

    public class DiscordRoleMessageRule : DiscordMessageRule
    {
        private SocketGuild m_guild;
        private List<string> m_roleNames = new List<string>();

        public DiscordRoleMessageRule(SocketGuild guild, string roleName, string command, string help) 
            : base(command, help)
        {
            m_roleNames = new List<string>(roleName.Split(','));
            m_guild     = guild;
        }

        public DiscordRoleMessageRule(SocketGuild guild, string roleName, string command, string pattern, string help) 
            : base(command, pattern, help)
        {
            m_roleNames = new List<string>(roleName.Split(','));
            m_guild     = guild;
        }

        public override bool Validate(object e)
        {
            foreach (var role in m_roleNames)
            {
                SocketRole hasRole = m_guild.Roles.FirstOrDefault(r => r.Name.Equals(role, StringComparison.InvariantCultureIgnoreCase));

                if ((hasRole != null) && (e is SocketMessage))
                {
                    SocketMessage msg = e as SocketMessage;
                    if (hasRole.Members.Contains(msg.Author))
                    {
                        return base.Validate(e);
                    }
                }
            }

            return false;          
        }
    }
}
