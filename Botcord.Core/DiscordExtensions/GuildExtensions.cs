using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Discord.Audio;
using Discord;
using Botcord.Core.Extensions;

namespace Botcord.Core.DiscordExtensions
{
    public static class GuildExtensions
    {
        public static IAudioClient ConnectToVoice(this SocketGuild guild, string channel)
        {
            var voiceChannel = guild.GetVoiceChannel(channel);
            if (voiceChannel == null)
            {
                string channelName = string.IsNullOrEmpty(channel) ? "FirstOrDefault" : channel;
                Logging.LogError(LogType.Bot, $"Failed to find voice channel {channelName} in server {guild.Name}");
                return null;
            }
            else
            {
                return ConnectToVoice(guild, voiceChannel);
            }
        }

        public static IAudioClient ConnectToVoice(this SocketGuild guild, ISocketAudioChannel channel)
        {
            var voiceChannel = channel;
            if (voiceChannel == null)
            {
                Logging.LogError(LogType.Bot, $"Voice channel is null in server {guild.Name}");
                return null;
            }
            else
            {
                Logging.LogInfo(LogType.Bot, $"Found Channel {channel.Name} in server {guild.Name} attempting to connect...");

                IAudioClient client = null;
                bool success = Utilities.ExecuteAndWait(async (token) =>
                {
                    try
                    {
                        client = await voiceChannel.ConnectAsync().ConfigureAwait(false);
                    }
                    catch(Exception ex)
                    {
                        Logging.LogException(LogType.Discord, ex, "Failed to connect to voice channel");
                    }
                }, 30.Second());

                if(!success)
                {
                    Logging.LogError(LogType.Bot, "Failed to connect to voice channel (timed out after 30 seconds).");
                }
                else
                {
                    Logging.LogInfo(LogType.Bot, "Connected to voice channel.");
                }

                if (client != null && (client.ConnectionState == ConnectionState.Connected || client.ConnectionState == ConnectionState.Connecting))
                {
                    Logging.LogInfo(LogType.Bot, $"Successfully connected to Channel {channel.Name} in server {guild.Name}.");
                    return client;
                }
                else
                {
                    Logging.LogError(LogType.Bot, $"Failed to connected to Channel {channel.Name} in server {guild.Name}.");
                    return null;
                }
            }
        }

        public static SocketVoiceChannel GetVoiceChannel(this SocketGuild guild, string channel = "")
        {
            return string.IsNullOrEmpty(channel) ? guild.VoiceChannels.FirstOrDefault() :
                guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals(channel, StringComparison.OrdinalIgnoreCase));
        }

        public static SocketTextChannel GetTextChannel(this SocketGuild guild, string channel = "")
        {
            return string.IsNullOrEmpty(channel) ? guild.TextChannels.FirstOrDefault() :
                guild.TextChannels.FirstOrDefault(c => c.Name.Equals(channel, StringComparison.OrdinalIgnoreCase));
        }

        public static SocketVoiceChannel GetUserVoiceChannel(this SocketGuild guild, SocketUser user)
        {
            return guild.VoiceChannels.FirstOrDefault(vc => vc.Users.FirstOrDefault(u => u.Id == user.Id) != null);
        }
    }
}
