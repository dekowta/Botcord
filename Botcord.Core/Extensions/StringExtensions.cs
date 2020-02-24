using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace Botcord.Core.Extensions
{
    public enum DiscordMarkdown
    {
        Italics,
        Bold,
        BoldItalics,
        Underline, 
        UnderlineItalics,
        UnderlineBold,
        UnderlineBoldItalics,
        Strikethrough,
        CodeLine,
        CodeBlock,
    }


    public static class StringExtensions
    {
        public static Embed BuildEmbed(this string content, string title)
        {
            return BuildEmbed(content, title, Color.Green);
        }
        public static Embed BuildEmbed(this string content, string title, Color colour)
        {
            try
            {
                EmbedBuilder eb = new EmbedBuilder();
                eb.Color = colour;

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.Name = title;
                eb.Author = eab;
                eb.Description = content;

                return eb.Build();
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Script, ex, "Failed to build embed");
                return null;
            }
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public static string Markdown(this string value, DiscordMarkdown markdown)
        {
            switch(markdown)
            {
                case DiscordMarkdown.Italics:
                    return $"*{value}*";
                case DiscordMarkdown.Bold:
                    return $"**{value}**";
                case DiscordMarkdown.BoldItalics:
                    return $"***{value}***";
                case DiscordMarkdown.Underline:
                    return $"__{value}__";
                case DiscordMarkdown.UnderlineItalics:
                    return $"__*{value}*__";
                case DiscordMarkdown.UnderlineBold:
                    return $"__**{value}**__";
                case DiscordMarkdown.UnderlineBoldItalics:
                    return $"__***{value}***__";
                case DiscordMarkdown.Strikethrough:
                    return $"~~{value}~~";
                default:
                    return value;
            }
        }

        public static List<string> SplitAndWrapString(this string text, string split = " ", int wrapLimit = 1950)
        {
            List<string> wrapped = new List<string>();
            if (text.Length <= wrapLimit)
            {
                wrapped.Add(text);
                return wrapped;
            }

            int currentCharCount = 0;
            string[] words = text.Split(new string[] { split }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();
            foreach (var word in words)
            {
                if (word.Length > wrapLimit)
                {
                    int startIndex = 0;
                    int addedChars = 0;
                    while (addedChars != word.Length)
                    {
                        string cutSection = word.Substring(startIndex, wrapLimit);
                        addedChars += cutSection.Length;
                        wrapped.Add(cutSection);
                        startIndex += cutSection.Length;
                        if ((addedChars + wrapLimit) >= word.Length)
                        {
                            int length = word.Length - startIndex;
                            cutSection = word.Substring(startIndex, length);
                            addedChars += cutSection.Length;
                            wrapped.Add(cutSection);
                        }
                    }
                    currentCharCount = 0;
                }
                else
                {
                    if ((currentCharCount + (word.Length + split.Length)) >= wrapLimit && sb.Length != 0 && sb.Length <= wrapLimit)
                    {
                        wrapped.Add(sb.ToString());
                        currentCharCount = 0;
                        sb.Clear();
                    }
                    currentCharCount += word.Length + split.Length;
                    sb.Append(word + split);
                }
            }

            if (currentCharCount > 0)
                wrapped.Add(sb.ToString());

            return wrapped;

        }
    }
}
