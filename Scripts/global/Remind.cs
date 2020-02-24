using Botcord.Core;
using Botcord.Core.Extensions;
using Botcord.Discord;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

//ASM: System.Private.Xml.Linq; System.Private.Xml; System.Xml.XDocument; System.Runtime.Extensions; System.IO.FileSystem; 
//ASM: System.Text.RegularExpressions;
namespace Scripts.global
{
    public class Reminder
    {
        public DateTime NextAnnounce
        {
            get;
            private set;
        }

        public bool Repeat
        {
            get;
            private set;
        }

        public string Content
        {
            get;
            private set;
        }

        public ulong Channel
        {
            get;
            private set;
        }

        private TimeSpan triggerSpan = new TimeSpan(0);

        public Reminder()
        {
            Channel = 0;
            triggerSpan = new TimeSpan(0);
            NextAnnounce = DateTime.MinValue;
            Content = string.Empty;
        }

        public Reminder(ulong channelId, TimeSpan triggerTime, bool repeat, string content)
        {
            triggerSpan = triggerTime;
            Channel = channelId;
            NextAnnounce = DateTime.UtcNow + triggerTime;
            Repeat = repeat;
            Content = content;
        }

        public bool ShouldAnnounce()
        {
            if (DateTime.UtcNow > NextAnnounce)
            {
                if (Repeat)
                {
                    NextAnnounce = DateTime.UtcNow + triggerSpan;
                }
                else
                {
                    NextAnnounce = DateTime.MaxValue;
                }
                return true;
            }

            return false;
        }

        public bool Load(XElement element)
        {
            bool repeat;
            if (!element.TryGetAttribute("Repeat", out repeat))
            {
                return false;
            }
            else
            {
                Repeat = repeat;
            }

            long span;
            if (!element.TryGetAttribute("Span", out span))
            {
                return false;
            }
            else
            {
                triggerSpan = new TimeSpan(span);
            }

            long next;
            if (!element.TryGetAttribute("Next", out next))
            {
                return false;
            }
            else
            {
                NextAnnounce = new DateTime(next, DateTimeKind.Utc);
                if (!Repeat && NextAnnounce < DateTime.UtcNow)
                {
                    return false;
                }
                else
                {
                    while(DateTime.UtcNow > NextAnnounce)
                    {
                        NextAnnounce += triggerSpan;
                    }
                }
            }

            ulong channel;
            if(!element.TryGetAttribute("Channel", out channel))
            {
                return false;
            }
            else
            {
                Channel = channel;
            }

            return true;
        }

        public XElement Save()
        {
            XElement element = new XElement("Reminder",
                new XAttribute("Channel", Channel),
                new XAttribute("Span", triggerSpan.Ticks),
                new XAttribute("Next", NextAnnounce.Ticks),
                new XAttribute("Repeat", Repeat));
            element.SetValue(Content);
            return element;
        }
    }

    public class Remind : IDiscordScript
    {
        public string Name => "Announcer 1.0";

        private string ReminderStore
        {
            get
            {
                return Path.Combine(Utilities.DataFolder, m_host.Guild.Id.ToString(), reminderFile);
            }
        }

        private string ReminderStoreFolder
        {
            get
            {
                return Path.Combine(Utilities.DataFolder, m_host.Guild.Id.ToString(), reminderFolder);
            }
        }

        private static string reminderFolder = "reminder";
        private static string reminderFile = reminderFolder + "\\reminders.xml";

        private object mutex = new object();
        private Dictionary<string, Reminder> reminders = new Dictionary<string, Reminder>();

        private string help = "\nAuto Reminder helper" +
            "\nThis system provides support for triggering a post in a specific channel at a" +
            "\nspecific time with the option of being repeatable\n" +
            "\nThe format for a specific time is day/month@hour:minute and for a specific duration" +
            "\nthe format is days@hours:minutes or hours:minutes" +
            "\nfor example 1/2@10:40 will trigger on the 1st of February at 10:40am Utc time" +
            "\nthe in also uses this format so 1/2@10:40GMT for duration so the following will" +
            "\ntrigger 1 day 2 months 10 hours and 40 minutes from the current Utc time" +
            "\nUse <https://www.timeanddate.com/worldclock/converter.html?iso=20200222T200000&p1=1440> to convert to UTC";

        private DiscordScriptHost m_host = null;
        private Timer timerTick = null;
        public void Initalise(DiscordScriptHost ActiveHost)
        {
            m_host = ActiveHost;

            if (!Directory.Exists(ReminderStoreFolder))
            {
                Directory.CreateDirectory(ReminderStoreFolder);
            }

            LoadAnnouncements();

            timerTick = new Timer(TickTimer, null, 0, 1.Minute());
        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            ActiveHost.RegisterCommand("reminder", "version", help, (param, e) => { });
            ActiveHost.RegisterCommand("reminder", "time", "Display the current Utc time the reminder is using", Time);
            ActiveHost.RegisterCommand("reminder", "(list | view <tag>)", "lists all the active announcements and when they will next trigger or view the content of a tag", List);
            ActiveHost.RegisterCommand("reminder", "new <tag> (in <time> [repeat] | at <time>) <content>", "Add a new announcement note: content should be wrapped in \"\"", New);
            ActiveHost.RegisterCommand("reminder", "remove <tag>", "Removes the specified announcement from the list", Remove);
        }

        public void Dispose()
        {
            timerTick.Change(Timeout.Infinite, Timeout.Infinite);
            timerTick.Dispose();
        }

        public async void Time(Dictionary<string, object> parameters, SocketMessage e)
        {
            string UtcTime = DateTime.UtcNow.ToString("dd/MM @ HH:mm");
            await e.Channel.SendMessageAsync($":clock1: | Current Utc Time is {UtcTime}");
        }

        public async void New(Dictionary<string, object> parameters, SocketMessage e)
        {
            string tag = parameters["<tag>"].ToString();
            if (reminders.ContainsKey(tag))
            {
                await e.Channel.SendMessageAsync($":warning: | tag `{tag}` has already been set either remove it or try a different tag");
                return;
            }

            string time = parameters["<time>"].ToString();
            TimeSpan timeSpanFromNow = new TimeSpan(0);
            if (parameters.ContainsKey("at"))
            {
                try
                {
                    timeSpanFromNow = ParseTime(time);
                    if (timeSpanFromNow.Ticks == 0)
                    {
                        await e.Channel.SendMessageAsync($":warning: | time `{time}` is not valid (format may be wrong day/month@hour:minute or Time is less then the current time)");
                    }
                }
                catch (Exception)
                {
                    await e.Channel.SendMessageAsync($":warning: | time `{time}` is not valid (value may be in an invalid range e.g 25 hour)");
                    return;
                }
            }

            if (parameters.ContainsKey("in"))
            {
                try
                {
                    timeSpanFromNow = ParseDuration(time);
                    if (timeSpanFromNow.Ticks == 0)
                    {
                        await e.Channel.SendMessageAsync($":warning: | time `{time}` is not valid (format may be wrong days@hours:minutes or time is negative)");
                    }
                }
                catch (Exception)
                {
                    await e.Channel.SendMessageAsync($":warning: | time `{time}` is not valid (value may be in an invalid range e.g 25 hour)");
                    return;
                }
            }

            bool repeatTime = false;
            if (parameters.ContainsKey("repeat"))
            {
                repeatTime = true;
            }

            string content = parameters["<content>"].ToString();

            Reminder reminder = new Reminder(e.Channel.Id, timeSpanFromNow, repeatTime, content);
            reminders.Add(tag, reminder);
            AddNewReminder(tag, reminder);

            if (parameters.ContainsKey("in"))
            {
                string nextTime = reminder.NextAnnounce.ToString("dd/MM @ HH:mm");
                await e.Channel.SendMessageAsync($":alarm_clock: | Reminder `{tag}` hase been set and will next trigger {nextTime}");
            }

            if (parameters.ContainsKey("at"))
            {
                TimeSpan timebetween = reminder.NextAnnounce - DateTime.UtcNow;
                string nextDuration = string.Empty;
                if(timebetween.Days > 0)
                {
                    nextDuration += $"{timebetween.Days} Day" + (timebetween.Days > 1 ? "s " : " ");
                }
                if(timebetween.Hours > 0)
                {
                    nextDuration += !string.IsNullOrEmpty(nextDuration) && timebetween.Minutes == 0 ? $" and {timebetween.Hours} Hour" : $"{timebetween.Hours} Hour";
                    nextDuration += (timebetween.Hours > 1 ? "s " : " ");
                }
                if (timebetween.Hours > 0)
                {
                    nextDuration += !string.IsNullOrEmpty(nextDuration) ? $" and {timebetween.Minutes} Minute" : $"{timebetween.Minutes} Minute";
                    nextDuration += (timebetween.Minutes > 1 ? "s " : " ");
                }

                await e.Channel.SendMessageAsync($":alarm_clock: | Reminder `{tag}` hase been set and will trigger in {nextDuration}");
            }
        }

        public async void Remove(Dictionary<string, object> parameters, SocketMessage e)
        {
            string tag = parameters["<tag>"].ToString();
            if (reminders.ContainsKey(tag))
            {
                reminders.Remove(tag);
                RemoveReminder(tag);
                await e.Channel.SendMessageAsync($":white_check_mark: | tag `{tag}` has been removed");
            }
            else
            {
                await e.Channel.SendMessageAsync($":warning: | tag `{tag}` has already been removed or didnt exist");
            }
        }

        public async void List(Dictionary<string, object> parameters, SocketMessage e)
        {
            IDMChannel userDM = await e.Author.GetOrCreateDMChannelAsync();
            if (parameters.ContainsKey("list"))
            {
                reminders.BuildCustomEmbed(reminder =>
                {
                    return $"{reminder.Key} - Triggering Next: {reminder.Value.NextAnnounce.ToString("dd/MM HH:mm")}";
                }, $"Reminders {reminders.Count}", userDM);

            }
            else if(parameters.ContainsKey("view"))
            {
                string tag = parameters["<tag>"].ToString();
                if(reminders.ContainsKey(tag))
                {
                    await userDM.SendMessageAsync("", false, BuildReminderEmebed(tag, reminders[tag]));
                }
                else
                {
                    await e.Channel.SendMessageAsync($":warning: | tag `{tag}` does not exist");
                }
            }
        }

        private void LoadAnnouncements()
        {
            if (!File.Exists(ReminderStore))
            {
                XDocument newClipStore = new XDocument();
                XElement element = new XElement("ReminderStore");
                newClipStore.Add(element);
                newClipStore.Save(ReminderStore);
                Logging.Log(LogType.Script, LogLevel.Info, $"Creating Reminder store {ReminderStore}");
            }
            else
            {
                List<string> keyRemoval = new List<string>();
                XDocument clipStoreDoc = XDocument.Load(ReminderStore);
                foreach (var element in clipStoreDoc.Root.Elements())
                {
                    string tag;
                    if (!element.TryGetAttribute("tag", out tag)) continue;
                    Reminder reminder = new Reminder();
                    if(reminder.Load(element))
                    {
                        reminders[tag] = reminder;
                    }
                    else
                    {
                        keyRemoval.Add(tag);
                    }
                }

                foreach(var remove in keyRemoval)
                {
                    RemoveReminder(remove);
                }
            }
        }

        private void AddNewReminder(string tag, Reminder reminder)
        {
            if (string.IsNullOrEmpty(ReminderStore) || string.IsNullOrEmpty(tag))
            {
                return;
            }

            lock (mutex)
            {
                if (!File.Exists(ReminderStore))
                {
                    XDocument newReminderStore = new XDocument();
                    XElement element = new XElement("ReminderStore");
                    XElement reminderElement = reminder.Save();
                    reminderElement.Add(new XAttribute("tag", tag));
                    element.Add(reminderElement);
                    newReminderStore.Add(element);
                    newReminderStore.Save(ReminderStore);
                    Logging.Log(LogType.Script, LogLevel.Info, $"Creating Reminder store {ReminderStore}");
                }
                else
                {
                    XDocument clipStoreDoc = XDocument.Load(ReminderStore);
                    XElement reminderElement = reminder.Save();
                    reminderElement.Add(new XAttribute("tag", tag));
                    clipStoreDoc.Root.Add(reminderElement);
                    clipStoreDoc.Save(ReminderStore);
                }
            }
        }

        private void RemoveReminder(string tag)
        {
            if (File.Exists(ReminderStore))
            {
                lock (mutex)
                {
                    XDocument reminderStoreDoc = XDocument.Load(ReminderStore);
                    XElement root = reminderStoreDoc.Root;
                    XElement item = root.FindElementByAttribute("tag", tag);
                    if (item != null)
                    {
                        item.Remove();
                        reminderStoreDoc.Save(ReminderStore);
                    }
                }
            }
        }

        private void UpdateReminder(string tag, Reminder reminder)
        {
            if (File.Exists(ReminderStore))
            {
                lock (mutex)
                {
                    XDocument reminderStoreDoc = XDocument.Load(ReminderStore);
                    XElement root = reminderStoreDoc.Root;
                    XElement item = root.FindElementByAttribute("tag", tag);
                    if (item != null)
                    {
                        item.Remove();
                        XElement updated = reminder.Save();
                        updated.Add(new XAttribute("tag", tag));
                    }
                    reminderStoreDoc.Save(ReminderStore);
                }
            }
        }

        private TimeSpan ParseTime(string input)
        {
            Match time = Regex.Match(input, @"(?<day>\d{1,2})\/(?<month>\d{1,2})@(?<hour>\d{1,2}):(?<minute>\d{1,2})");
            if (time.Success)
            {
                int day = int.Parse(time.Groups["day"].Value);
                int month = int.Parse(time.Groups["month"].Value);
                int hour = int.Parse(time.Groups["hour"].Value);
                int minute = int.Parse(time.Groups["minute"].Value);
                DateTime newTime = new DateTime(DateTime.UtcNow.Year, month, day, hour, minute, 0, DateTimeKind.Utc);
                if (newTime > DateTime.UtcNow)
                {
                    return newTime - DateTime.UtcNow;
                }
            }

            return new TimeSpan(0);
        }

        private TimeSpan ParseDuration(string input)
        {
            Match time = Regex.Match(input, @"(?>(?<days>\d{1})@|)(?<hours>\d{1,2}):(?<minutes>\d{1,2})");
            if (time.Success)
            {
                int day = !string.IsNullOrEmpty(time.Groups["days"].Value) ? int.Parse(time.Groups["days"].Value) : 0;
                int hour = int.Parse(time.Groups["hours"].Value);
                int minute = int.Parse(time.Groups["minutes"].Value);
                TimeSpan result = new TimeSpan(day, hour, minute, 0);
                if (DateTime.UtcNow + result > DateTime.UtcNow)
                {
                    return result;
                }
            }

            return new TimeSpan(0);
        }

        private void TickTimer(object state)
        {
            lock (reminders)
            {
                List<string> keyRemoval = new List<string>();
                foreach (var reminder in reminders)
                {
                    Reminder reminderItem = reminder.Value;
                    if (reminderItem.ShouldAnnounce())
                    {
                        SocketTextChannel channel = m_host.Guild.GetTextChannel(reminderItem.Channel);
                        if (channel != null)
                        {
                            channel.SendMessageAsync("", false, BuildReminderEmebed(reminder.Key, reminderItem));
                        }

                        if (!reminderItem.Repeat)
                        {
                            keyRemoval.Add(reminder.Key);
                        }
                        else
                        {
                            UpdateReminder(reminder.Key, reminderItem);
                        }
                    }

                    if(reminderItem.NextAnnounce < DateTime.UtcNow)
                    {
                        keyRemoval.Add(reminder.Key);
                    }
                }

                foreach (var removeKey in keyRemoval)
                {
                    reminders.Remove(removeKey);
                    RemoveReminder(removeKey);
                }
            }
        }

        private Embed BuildReminderEmebed(string tag, Reminder reminder)
        {
            string title = tag;
            if (reminder.Repeat)
            {
                title += $" Next Trigger: {reminder.NextAnnounce.ToString("dd/MM HH:mm")}";
            }
            else
            {
                title += $" Tiggers at: {reminder.NextAnnounce.ToString("dd/MM HH:mm")}";
            }

            return reminder.Content.BuildEmbed(title);
        }
    }
}
