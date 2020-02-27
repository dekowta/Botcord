using Botcord.Core;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Botcord.Core.Extensions;
using Discord;
using System.Threading;

namespace Botcord.Discord
{
    public enum DiscordEventType
    {
        PrivateMessageRecieved,
        PrivateMessageDeleted,
        MessageRecieved,
        MessageDeleted,
        MessageEdited,
        Connected,
        RoleUpdated,
        RoleDeleted,
        MemeberAdded,
        MemeberRemoved,
        LeftVoiceChannel,
        UserSpeaking,
        UserTyping,
        UserUpdated,
        VoiceClientConnected,
        VoiceStateUpdated,
        URLUpdate
    }

    public enum DiscordAdmin
    {
        DM,
        Global,
    }

    public struct DiscordEvent
    {
        public IDiscordRule Rule;
        public DiscordEventType Type;
        public string Help;
        public Delegate Event;
    }

    public class DiscordScriptHost : IDisposable
    {
        public SocketGuild Guild
        {   
            get { return m_guild; }
        }

        IDiscordScript m_script;
        DiscordSocketClient m_client;
        SocketGuild m_guild;

        private Dictionary<DiscordEventType, List<DiscordEvent>> m_discordEvents = new Dictionary<DiscordEventType, List<DiscordEvent>>();

        public DiscordScriptHost(DiscordSocketClient client, IDiscordScript script)
        {
            m_client = client;
            m_script = script;
        }

        public void AttachGuild(SocketGuild guild)
        {
            m_guild = guild;

            RegisterCommand("help", "[<command>]", "Shows this message", OnHelpMessage);
            RegisterAdminCommand(DiscordAdmin.DM, "help", "[<command>]", "Shows this message", OnHelpMessage);

            string name = m_guild != null ? guild.Name : "Admin DM Custom Guild";

            m_script.Initalise(this);

            Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Script Initalised in guild hook {name}");

            m_client.MessageReceived        += client_MessageReceived;
            m_client.UserVoiceStateUpdated  += client_UserVoiceStateUpdated;
            m_client.UserJoined             += client_UserJoined;

            Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Attaching Events in guild hook {name}");

            m_script.RegisterCommands(this);
            
            Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Registed {m_discordEvents.Sum(e => e.Value.Count)} Script commands in guild hook {name}");
        }

        public void DettachGuild(SocketGuild guild)
        {
            string name = m_guild != null ? guild.Name : "Admin DM Custom Guild";
            
            Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Disposing of script on guild {name}");
            try
            {
                m_script.Dispose();
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, $"Failed to dispose of script {m_script.Name}");
            }

            if (m_guild?.Id != guild?.Id)
            {
                string newGuildName = guild != null ? guild.Name : "Unknown Guild";

                Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Dettaching guild {newGuildName} does not match guild {name} will dettach any way.");
            }
            
            m_client.MessageReceived         -= client_MessageReceived;
            m_client.UserVoiceStateUpdated   -= client_UserVoiceStateUpdated;
            m_client.UserJoined              -= client_UserJoined;

            Logging.LogInfo(LogType.Bot, $"({m_script.Name}) Dettached from guild {name}");
        }

        #region Register Events

        public void RegisterCommand(string command, string pattern, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if (m_guild != null)
            {
                DiscordMessageRule rule = new DiscordMessageRule(command, pattern, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public void RegisterCommand(string command, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if (m_guild != null)
            {
                DiscordMessageRule rule = new DiscordMessageRule(command, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public void RegisterRoleCommand(string command, string pattern, string help, string roleTags, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if (m_guild != null)
            {
                DiscordRoleMessageRule rule = new DiscordRoleMessageRule(m_guild, roleTags, command, pattern, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public void RegisterRoleCommand(string command, string help, string roleTags, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if (m_guild != null)
            {
                DiscordRoleMessageRule rule = new DiscordRoleMessageRule(m_guild, roleTags, command, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public void RegisterAdminCommand(string command, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            RegisterAdminCommand(DiscordAdmin.Global, command, help, eventMethod);
        }

        public void RegisterAdminCommand(string command, string pattern, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            RegisterAdminCommand(DiscordAdmin.Global, command, pattern, help, eventMethod);
        }

        public void RegisterAdminCommand(DiscordAdmin type, string command, string pattern, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if ((type == DiscordAdmin.DM && m_guild == null) || type == DiscordAdmin.Global)
            {
                DiscordMessageRule rule = new DiscordAdminMessageRule(command, pattern, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = type == DiscordAdmin.DM ? DiscordEventType.PrivateMessageRecieved : DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public void RegisterAdminCommand(DiscordAdmin type, string command, string help, Action<Dictionary<string, object>, SocketMessage> eventMethod)
        {
            if ((type == DiscordAdmin.DM && m_guild == null) || type == DiscordAdmin.Global)
            {
                DiscordMessageRule rule = new DiscordMessageRule(command, help);
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = type == DiscordAdmin.DM ? DiscordEventType.PrivateMessageRecieved : DiscordEventType.MessageRecieved;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
            }
        }

        public bool RegisterEvent<T>(DiscordEventType eventType, Action<Dictionary<string, object>, T> eventMethod)
        {
            DiscordBasicRule rule = new DiscordBasicRule();
            if (rule.IsEventSupported(eventType))
            {
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = eventType;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
                return false;
            }
            return true;
        }

        public bool RegisterEvent<T>(IDiscordRule rule, DiscordEventType eventType, Action<Dictionary<string, object>, T> eventMethod)
        {
            if (rule.IsEventSupported(eventType))
            {
                DiscordEvent discordEvent = new DiscordEvent();
                discordEvent.Rule = rule;
                discordEvent.Type = eventType;
                discordEvent.Event = eventMethod;
                AddDiscordEvent(discordEvent);
                return true;
            }

            return false;
        }

        public bool RegisterAdminEvent<T>(DiscordAdmin type, IDiscordRule rule, DiscordEventType eventType, Action<Dictionary<string, object>, T> eventMethod)
        {
            if ((type == DiscordAdmin.DM && m_guild == null) || type == DiscordAdmin.Global)
            {
                if (rule.IsEventSupported(eventType))
                {
                    DiscordEvent discordEvent = new DiscordEvent();
                    discordEvent.Rule = rule;
                    discordEvent.Type = eventType;
                    discordEvent.Event = eventMethod;
                    AddDiscordEvent(discordEvent);
                    return true;
                }
            }
            return false;
        }

        #endregion

        private void AddDiscordEvent(DiscordEvent eventAction)
        {
            DiscordEventType type = eventAction.Type;
            if (m_discordEvents.ContainsKey(type))
            {
                if (m_discordEvents[type] == null)
                {
                    m_discordEvents[type] = new List<DiscordEvent>();
                    m_discordEvents[type].Add(eventAction);
                }
                else
                {
                    m_discordEvents[type].Add(eventAction);
                }
            }
            else
            {
                m_discordEvents.Add(type, new List<DiscordEvent>());
                m_discordEvents[type].Add(eventAction);
            }
        }

        private Task client_MessageReceived(SocketMessage arg)
        {
            if(m_guild != null && m_guild.Channels.Any(channel => channel == arg.Channel))
            {
                return InvokeEvent<SocketMessage>(DiscordEventType.MessageRecieved, arg);
            }
            else if(arg.Channel is SocketDMChannel)
            {
                return InvokeEvent<SocketMessage>(DiscordEventType.PrivateMessageRecieved, arg);
            }

            return Task.CompletedTask;
        }

        private Task client_UserJoined(SocketGuildUser arg)
        {
            if(m_guild != null && arg.Guild == m_guild)
            {
                return InvokeEvent<SocketGuildUser>(DiscordEventType.MemeberAdded, arg);
            }

            return Task.CompletedTask;
        }

        private Task client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if(m_guild != null && arg3.VoiceChannel != null &&
                m_guild.GetVoiceChannel(arg3.VoiceChannel.Id) != null && !arg1.IsBot)
            {
                SocketVoiceConnection arg = new SocketVoiceConnection(arg1, arg2, arg3);
                return InvokeEvent<SocketVoiceConnection>(DiscordEventType.VoiceStateUpdated, arg);
            }

            return Task.CompletedTask;
        }

        private Task InvokeEvent<T>(DiscordEventType eventType, object e)
        {
            T discordEventArgs = default(T);
            if (e.TryCast<T>(out discordEventArgs))
            {
                if (m_discordEvents.ContainsKey(eventType))
                {
                    foreach (var eventObject in m_discordEvents[eventType])
                    {
                        if (eventObject.Type == eventType &&
                            eventObject.Rule.IsEventSupported(eventObject.Type) &&
                            eventObject.Rule.Validate(e))
                        {
                            Dictionary<string, object> parameters = eventObject.Rule.BuildParameters(e);

                            Logging.LogInfo(LogType.Bot, $"Running guild {m_guild?.Name} event {eventObject.Rule} with arg {e}");

                            Task invokedEvent = new Task(sender =>
                            {
                                Thread.CurrentThread.Name = $"Running guild {m_guild?.Name} event {eventObject.Rule} with arg {e}";
                                try
                                {
                                    ((Action<Dictionary<string, object>, T>)eventObject.Event)?.Invoke((Dictionary<string, object>)sender, discordEventArgs);
                                }
                                catch (Exception ex)
                                {
                                    Logging.LogException(LogType.Script, ex, "Failed to run script command");
                                }

                            }, parameters);
                            invokedEvent.Start();
                            return Task.CompletedTask;
                        }

                    }
                }
            }

            return Task.CompletedTask;
        }

        private void OnHelpMessage(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (parameters.ContainsKey("<command>"))
            {
                string command = parameters["<command>"].ToString();

                if (m_discordEvents.ContainsKey(DiscordEventType.MessageRecieved))
                {
                    List<DiscordEvent> rules = m_discordEvents[DiscordEventType.MessageRecieved];
                    IEnumerable<DiscordEvent> commandRules = rules.Where(ev => ev.Rule is DiscordMessageRule ? ((DiscordMessageRule)ev.Rule).Command == command : false);
                    if (commandRules.Count() > 0)
                    {
                        string title = $"Commands For {command}";

                        commandRules.BuildCustomEmbed(evt =>
                        {
                            DiscordMessageRule cmd = (evt.Rule as DiscordMessageRule);
                            return $"{cmd.Pattern.Markdown(DiscordMarkdown.UnderlineBold)}\n{cmd.Help}\n";
                        }, title, e.Channel);
                    }
                }

                if (m_discordEvents.ContainsKey(DiscordEventType.PrivateMessageRecieved))
                {
                    List<DiscordEvent> rules = m_discordEvents[DiscordEventType.PrivateMessageRecieved];
                    IEnumerable<DiscordEvent> commandRules = rules.Where(ev => ev.Rule is DiscordMessageRule ? ((DiscordMessageRule)ev.Rule).Command == command : false);
                    if (commandRules.Count() > 0)
                    {
                        string title = $"Commands For {command}";

                        commandRules.BuildCustomEmbed(evt =>
                        {
                            DiscordMessageRule cmd = (evt.Rule as DiscordMessageRule);
                            return $"{cmd.Pattern.Markdown(DiscordMarkdown.UnderlineBold)}\n{cmd.Help}\n";
                        }, title, e.Channel);
                    }
                }
            }
            //else
            //{
            //    HashSet<string> commands = new HashSet<string>();
            //    foreach (var kvp in m_discordEvents)
            //    {
            //        foreach(var events in kvp.Value)
            //        {
            //            if(events.Rule is DiscordMessageRule)
            //            {
            //                DiscordMessageRule rule = (events.Rule as DiscordMessageRule);
            //                commands.Add(rule.Command);
            //            }
            //        }
            //    }

            //    string title = $"Commands List";
            //    commands.BuildCustomEmbed(cmd =>
            //    {
            //        return $"Command - {cmd}";
            //    }, title, e.Channel);
            //}
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DettachGuild(m_guild);
                    m_discordEvents.Clear();
                    m_script.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DiscordScriptHost() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
