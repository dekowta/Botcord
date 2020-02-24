
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Botcord.Core.Extensions
{
    public static class EnumerableExtensions
    {
        public static async void BuildCustomEmbed<T>(this IEnumerable<T> collection, Func<T, string> func, string initalTitle, IMessageChannel channel)
        {
            if (channel != null)
            {
                IEnumerable<Embed> embeds = collection.BuildCustomEmbed(func, initalTitle);
                foreach (Embed em in embeds)
                {
                    await channel.SendMessageAsync("", false, em);
                    await Task.Delay(2000);
                }
            }
        }

        public static IEnumerable<Embed> BuildCustomEmbed<T>(this IEnumerable<T> collection, Func<T, string> func, string initalTitle)
        {
            List<Embed> m_embeds = new List<Embed>();
            string title = initalTitle;
            bool complete = false;
            int completed = 0;
            StringBuilder sb = new StringBuilder(EmbedBuilder.MaxDescriptionLength);
            while (!complete)
            {
                
                foreach (var commandRule in collection.Skip(completed))
                {
                    string message = func(commandRule);
                    if ((sb.Length + (message.Length + 2)) > EmbedBuilder.MaxDescriptionLength)
                    {
                        Embed em = BuildEmbed(title, sb.ToString());
                        if(em == null)
                        {
                            title = string.Empty;
                            sb = new StringBuilder(EmbedBuilder.MaxDescriptionLength);
                            break;
                        }

                        m_embeds.Add(em);
                        title = string.Empty;
                        sb = new StringBuilder(EmbedBuilder.MaxDescriptionLength);
                        break;
                    }
                    if (message.Length >= EmbedBuilder.MaxDescriptionLength)
                    {
                        Embed em = BuildEmbed(title, message.Truncate(EmbedBuilder.MaxDescriptionLength));
                        if (em == null)
                        {
                            title = string.Empty;
                            sb = new StringBuilder(EmbedBuilder.MaxDescriptionLength);
                            break;
                        }

                        m_embeds.Add(em);
                        title = string.Empty;
                        sb = new StringBuilder(EmbedBuilder.MaxDescriptionLength);
                        break;
                    }

                    sb.AppendLine(message);
                    completed++;
                }

                if (completed >= collection.Count())
                {
                    complete = true;
                }
            }

            if (sb.Length != 0)
            {
                if (sb.Length <= EmbedBuilder.MaxDescriptionLength)
                {
                    Embed em = BuildEmbed(title, sb.ToString());
                    if(em != null)
                        m_embeds.Add(em);
                }
                if (sb.Length > EmbedBuilder.MaxDescriptionLength)
                {
                    Embed em = BuildEmbed(title, sb.ToString().Truncate(EmbedBuilder.MaxDescriptionLength));
                    if (em != null)
                        m_embeds.Add(em);
                }
            }

            return m_embeds;
        }

        private static Embed BuildEmbed(string title, string text)
        {
            try
            {
                EmbedBuilder eb = new EmbedBuilder();
                eb.Color = Color.Green;

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.Name = title;
                eb.Author = eab;
                eb.Description = text;

                return eb.Build();
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Script, ex, "Failed to build embed");
                return null;
            }
        }

    }
}
