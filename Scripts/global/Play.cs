using Botcord.Audio;
using Botcord.Core;
using Botcord.Core.Extensions;
using Botcord.Core.DiscordExtensions;
using Botcord.Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;

//ASM: System.Private.Xml.Linq; System.Private.Xml; System.Xml.XDocument; System.Runtime.Extensions; System.IO.FileSystem; 
//ASM: System.ComponentModel.TypeConverter; System.ComponentModel.Primitives;
namespace Scripts.global
{
    public class UserVoiceConnectedRule : IDiscordRule
    {
        public bool IsEventSupported(DiscordEventType eventType)
        {
            return eventType == DiscordEventType.VoiceStateUpdated;
        }

        public bool Validate(object e)
        {
            if(e is SocketVoiceConnection)
            {
                SocketVoiceConnection connection = e as SocketVoiceConnection;
                if(connection.OldState.VoiceChannel == null &&
                   connection.NewState.VoiceChannel != null &&
                   connection.User.Id != DiscordHost.Instance.ThisBot.Id)
                {
                    return true;
                }
            }

            return false;
        }

        public Dictionary<string, object> BuildParameters(object e)
        {
            return new Dictionary<string, object>();
        }
    }

    public class Play : IDiscordScript
    {
        public string Name
        {
            get { return "Play 2.0"; }
        }

        public class Clip
        {
            public string File;
            public string Tag;
            public int Count;
            public string AddedBy;
        }


        private static string sourceFolder = "audio_clips";
        private static string sourceFile = sourceFolder + "\\play_clip_dictionary.xml";
        private static string sourceIntroFile = sourceFolder + "\\play_intro_dictionary.xml";

        private string ClipStore
        {
            get
            {
                return Path.Combine(Utilities.DataFolder, m_host.Guild.Id.ToString(), sourceFile);
            }
        }

        private string IntroStore
        {
            get
            {
                return Path.Combine(Utilities.DataFolder, m_host.Guild.Id.ToString(), sourceIntroFile);
            }
        }

        private string ClipStoreFolder
        {
            get
            {
                return Path.Combine(Utilities.DataFolder, m_host.Guild.Id.ToString(), sourceFolder);
            }
        }

        private string ClipStoreShortFolder
        {
            get
            {
                return Path.Combine(Utilities.DataShortFolder, m_host.Guild.Id.ToString(), sourceFolder);
            }
        }

        private static TimeSpan clipTimeMax = new TimeSpan(0, 0, 16);
        private static TimeSpan introTimeMax = new TimeSpan(0, 0, 8);

        private static string RandomTag = "meme";
        private static string verionId = "1.4";
        private string help = $"\nPlay Memé on demand ({verionId})." +
              "\nTo use just use !play <tag> and if the name exits the clip will play." +
              "\nTo add a clip use !play add <tag> <link> and the clip will be added if one doesnt exist." +
              "\nTo find a list of all clips use !play list and it will message you all the clips." +
              "\nBot will auto disconnect if nothing has played for 2 minutes and is connected" +
              "\n\nIn order to get best performance it is recommended to have files with a sample quality of 128kbps or less" +
              "\nAlso play keys can be linked by , so <name1>,<name2>,<name3> will play name1 then name2 then name3" +
              "\nmeme to random" +
              "\nLimits are as followed: Mp3 only, files < 750kb, and files < 16 seconds. Tag < 50 character (will be Truncated if higher)";

        private DiscordScriptHost m_host = null;
        private SoundPlayer player = null;

        private ISocketMessageChannel m_fixedChannel = null;

        private Dictionary<string, Clip> m_clips = new Dictionary<string, Clip>();
        private int m_clipLimit = 100;

        private Dictionary<ulong, Clip> m_intros = new Dictionary<ulong, Clip>();
        private int m_introLimit = 100;

        private const long memeTeamId   = 137641379177168896;
        private const long skylarServer = 371484428658016257;

        private object m_mutex = new object();
        private System.Timers.Timer m_autoDisconnectTimer = null;
        private bool m_disposing = false;

        private static UserVoiceConnectedRule voiceConnectionRule = new UserVoiceConnectedRule();

        public void Initalise(DiscordScriptHost ActiveHost)
        {
            m_host = ActiveHost;
            player = new SoundPlayer(ActiveHost.Guild);

            if (!Directory.Exists(ClipStoreFolder))
            {
                Directory.CreateDirectory(ClipStoreFolder);
            }

            if (ActiveHost.Guild.Id == memeTeamId || ActiveHost.Guild.Id == skylarServer)
            {
                m_clipLimit = -1;
                m_introLimit = -1;
            }

            //m_autoDisconnectTimer = new System.Timers.Timer(10.Second());
            m_autoDisconnectTimer = new System.Timers.Timer(2.Minute());
            m_autoDisconnectTimer.Elapsed += AutoDisconnectTimer_Elapsed;
            m_autoDisconnectTimer.Start();

            LoadExistingClips();
            LoadExistingIntros();
        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            string additionalHelp = m_clipLimit != -1 ? $"\nCurrent Server has limit of {m_clipLimit} and used {m_clips.Count}" : $"\nCurrent Clip Count {m_clips.Count}";
            ActiveHost.RegisterCommand("play", "version", help + additionalHelp, (param, e) => { });
            if (ActiveHost.Guild.Id != memeTeamId)
            {
                ActiveHost.RegisterRoleCommand("play", "list (top [<amount>] | [<letter>])", "Sends over a list of all the clips via direct message.\nUse Top for a list of top played or enter text to search tag stating with that text.",
                    MinimumLevel(), List);
                ActiveHost.RegisterRoleCommand("play", "stop", "Stop the current playing clip.",
                    MinimumLevel(), Stop);
                ActiveHost.RegisterRoleCommand("play", "disconnect", "Disconnects from the voice channel.",
                    MinimumLevel(), Disconnect);
                ActiveHost.RegisterRoleCommand("play", "add <tag> <link>", "Adds a new clip (Silver + Gold only).",
                    MediumLevel(), Add);
                ActiveHost.RegisterRoleCommand("play", "update <tag> <link>", "Updates a existing clip with a new clip (Silver + Gold only).",
                    MediumLevel(), Update);
                ActiveHost.RegisterRoleCommand("play", "rename <tag> <newtag>", "Renames the existing clip (Silver + Gold only).",
                    MediumLevel(), Rename);
                ActiveHost.RegisterRoleCommand("play", "remove <tag>", "Removes a clip (Gold Only).",
                    MaximumLevel(), Remove);
                ActiveHost.RegisterRoleCommand("play", "reboot", "Reboots the player. Can sometimes fix problems (Gold Only)",
                    MaximumLevel(), Reboot);
                ActiveHost.RegisterRoleCommand("play", "<tag> [<amount>]", "Plays a clip with optional amount.",
                    MinimumLevel(), PlayClip);

                ActiveHost.RegisterRoleCommand("intro", "(add | update) <link>", "Adds or Updates a new intro clip for when you join.",
                    MediumLevel(), AddIntro);
                ActiveHost.RegisterRoleCommand("intro", "(add | update) <link> <user>", "Adds or Updates a new intro clip for when a user joins (Gold Only).",
                    MaximumLevel(), AddIntro);
                ActiveHost.RegisterRoleCommand("intro", "remove", "Will Remove your intro clip.",
                    MediumLevel(), RemoveInto);
                ActiveHost.RegisterRoleCommand("intro", "remove <user>", "Will Remove a users intro clip (Gold Only).",
                    MaximumLevel(), RemoveInto);
            }
            else
            {
                ActiveHost.RegisterCommand("play", "list (top [<amount>] | [<letter>])", "Sends over a list of all the clips via direct message.\nUse Top for a list of top played or enter text to search tag stating with that text.", List);
                ActiveHost.RegisterCommand("play", "stop", "Stop the current playing clip.", Stop);
                ActiveHost.RegisterCommand("play", "disconnect", "Disconnects from the voice channel.", Disconnect);
                ActiveHost.RegisterCommand("play", "add <tag> <link>", "Adds a new clip.", Add);
                ActiveHost.RegisterCommand("play", "update <tag> <link>", "Updates a existing clip with a new clip.", Update);
                ActiveHost.RegisterCommand("play", "rename <tag> <newtag>", "Renames the existing clip.", Rename);
                ActiveHost.RegisterCommand("play", "remove <tag>", "Removes a clip.", Remove);
                ActiveHost.RegisterCommand("play", "<tag> [<amount>]", "Plays a clip with optional amount", PlayClip);

                ActiveHost.RegisterAdminCommand("play", "reboot", "Reboots the player (admin only)", Reboot);

                ActiveHost.RegisterCommand("intro", "(add | update) <link>", "Adds or Updates a new intro clip for when you join.", AddIntro);
                ActiveHost.RegisterAdminCommand("intro", "(add | update) <link> <user>", "Adds or Updates a new intro clip for when a user joins (admin Only).", AddIntro);
                ActiveHost.RegisterCommand("intro", "remove", "Will Remove your intro clip.", RemoveInto);
                ActiveHost.RegisterAdminCommand("intro", "remove <user>", "Will Remove a users intro clip (admin Only).", RemoveInto);
            }

            ActiveHost.RegisterEvent<SocketVoiceConnection>(voiceConnectionRule, DiscordEventType.VoiceStateUpdated, UserConnected);
        }

        public void Dispose()
        {
            m_autoDisconnectTimer.Stop();
        }

        public async void List(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);
            IDMChannel userDM = await e.Author.GetOrCreateDMChannelAsync();

            if (!parameters.ContainsKey("list"))
            {
                Logging.LogError(LogType.Script, "list argument is not valid");
                await logger.SendMessageAsync($":warning: | list argument is invalid");
                return;
            }

            if (parameters.ContainsKey("top"))
            {
                int amount = 10;
                if (parameters.ContainsKey("<amount>"))
                {
                    ValueObject amountObj = parameters["<amount>"] as ValueObject;
                    if (amountObj.IsInt)
                    {
                        if (amountObj.AsInt > m_clips.Count)
                        {
                            amount = m_clips.Count;
                        }
                        else
                        {
                            amount = amountObj.AsInt;
                        }
                    }
                }

                if (amount != 0 && m_clips.Count > 0)
                {
                    IEnumerable<Embed> embeds = null;
                    lock (m_mutex)
                    {
                        var orderedClips = m_clips.OrderByDescending(kvp => kvp.Value.Count).Take(amount);
                        embeds = orderedClips.BuildCustomEmbed(kvp =>
                        {
                            Clip clip = kvp.Value;
                            return $"`{clip.Tag}` - {clip.Count} - Added By: {clip.AddedBy}";
                        }, $"Top {amount} clips");
                    }

                    if (embeds != null)
                    {
                        foreach (var embed in embeds)
                        {
                            await userDM.SendMessageAsync("", false, embed);
                            await Task.Delay(1000);
                        }
                    }
                }
            }
            else if (parameters.ContainsKey("<letter>"))
            {

                string letter = parameters["<letter>"].ToString();
                var clips = m_clips.Where(kvp => kvp.Key.StartsWith(letter));
                if (clips.Count() > 0)
                {
                    IEnumerable<Embed> embeds = null;
                    lock (m_mutex)
                    {
                        embeds = clips.BuildCustomEmbed(kvp =>
                        {
                            Clip clip = kvp.Value;
                            return $"`{clip.Tag}` - {clip.Count} - Added By: {clip.AddedBy}";
                        }, $"Clips starting with {letter.Truncate(50)}");
                    }

                    if (embeds != null)
                    {
                        foreach (var embed in embeds)
                        {
                            await userDM.SendMessageAsync("", false, embed);
                            await Task.Delay(1000);
                        }
                    }
                }
                else
                {
                    Logging.LogError(LogType.Script, $"No tag starting with {letter}");
                    await logger.SendMessageAsync($":warning: | No clips start with {letter}");
                }
            }
            else
            {
                IEnumerable<Embed> embeds = null;
                lock (m_mutex)
                {
                    var ordered = m_clips.OrderBy(kvp => kvp.Key);
                    embeds = ordered.BuildCustomEmbed(kvp =>
                    {
                        Clip clip = kvp.Value;
                        return $"`{clip.Tag}` - {clip.Count} - Added By: {clip.AddedBy}";
                    }, $"Clips {m_clips.Count}");
                }

                if (embeds != null)
                {
                    foreach (var embed in embeds)
                    {
                        await userDM.SendMessageAsync("", false, embed);
                        await Task.Delay(1000);
                    }
                }
            }
        }

        public void Stop(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (player != null)
            {
                player.Stop();
            }
        }
        
        public void Disconnect(Dictionary<string, object> parameters, SocketMessage e)
        {
            if (player != null)
            {
                player.Stop();
                player.DisconnectFromVoice();
            }
        }

        public void PlayClip(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<tag>"))
            {
                Logging.LogError(LogType.Script, "Tag or Link is not set in add command");
                logger.SendMessageAsync($":warning: | Could not find a tag in argument.");
                return;
            }

            if(m_disposing)
            {
                Logging.LogError(LogType.Script, $"Cant play while bot is rebooting on server {m_host.Guild.Name}");
                logger.SendMessageAsync($":warning: | Can't play while bot is rebooting on server.");
                return;
            }

            string tag = parameters["<tag>"].ToString();
            tag = Utilities.SanitiseString(tag.Truncate(50));

            //ensure connected
            Connect(parameters, e);
            if(!player.IsConnected())
            {
                Logging.LogError(LogType.Script, $"Failed to connect player on guild {m_host.Guild.Name}");
                logger.SendMessageAsync($":warning: | Failed to connect to voice channel.");
                return;
            }

            if (tag.Contains(","))
            {
                foreach (var tagItem in tag.Split(','))
                {
                    if (tagItem == RandomTag)
                    {
                        EnqueueRandom();
                    }
                    else
                    {
                        EnqueueClip(tagItem);
                    }
                }
            }
            else
            {
                if (tag != RandomTag && !m_clips.ContainsKey(tag))
                {
                    Logging.LogError(LogType.Script, "Clip {tag} doesnt exist to play.");
                    logger.SendMessageAsync($":warning: | Clip {tag} doesnt exist to play.");
                    return;
                }

                int amount = 1;
                if (parameters.ContainsKey("<amount>") && parameters["<amount>"] is ValueObject)
                {
                    ValueObject amountObj = parameters["<amount>"] as ValueObject;
                    if (amountObj.IsInt)
                    {
                        amount = amountObj.AsInt;
                    }
                }

                for (int i = 0; i < amount; i++)
                {
                    if (tag == RandomTag)
                    {
                        EnqueueRandom();
                    }
                    else
                    {
                        EnqueueClip(tag);
                    }
                }
            }

        }

        public async void Add(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<tag>") || !parameters.ContainsKey("<link>"))
            {
                Logging.LogError(LogType.Script, "Tag or Link is not set in add command");
                await logger.SendMessageAsync($":warning: | tag or link argument is invalid");
                return;
            }

            if (m_clips.Count >= m_clipLimit && m_clipLimit != -1)
            {
                Logging.LogError(LogType.Script, $"Clip limit reached for server {m_host.Guild.Name} | {m_host.Guild.Id}");
                await logger.SendMessageAsync($":warning: | You have reached the maximum clip limit of {m_clipLimit}. Remove a clip before adding a new one.");
                return;
            }

            string tag = parameters["<tag>"].ToString();
            string link = parameters["<link>"].ToString();
            tag = Utilities.SanitiseString(tag.Truncate(50));

            string shortFilePath = Path.Combine(ClipStoreShortFolder, tag + ".mp3");
            string downloadLocation = await TryDownloadFile(tag, link, clipTimeMax, logger);
            if (string.IsNullOrEmpty(downloadLocation))
            {
                return;
            }

            Embed embedItem = null;
            lock (m_mutex)
            {
                AddNewClip(shortFilePath, tag, e.Author.Username);
                CreateClip(shortFilePath, tag, 0, e.Author.Username);

                Clip clip = m_clips[tag];

                embedItem = CreateAddedEmebed(clip, e.Author);
            }

            Logging.LogError(LogType.Script, $"Added new clip {tag} from file '{link}'.");
            await logger.SendMessageAsync("", false, embedItem);
        }

        public async void Update(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<tag>") || !parameters.ContainsKey("<link>"))
            {
                Logging.LogError(LogType.Script, "Tag or Link is not set in add command");
                await logger.SendMessageAsync($":warning: | tag or link argument is invalid");
                return;
            }

            string tag = parameters["<tag>"].ToString();
            string link = parameters["<link>"].ToString();
            tag = Utilities.SanitiseString(tag.Truncate(50));

            if (m_clips.ContainsKey(tag))
            {
                Logging.LogError(LogType.Script, $"No tag found '{tag}'");
                await logger.SendMessageAsync($":warning: | tag does not exist already.");
                return;
            }

            string shortFilePath = Path.Combine(ClipStoreShortFolder, tag + ".mp3");
            string downloadLocation = await TryDownloadFile(tag, link, clipTimeMax, logger);
            if (string.IsNullOrEmpty(downloadLocation))
            {
                return;
            }

            string file = string.Empty;
            lock (m_mutex)
            {
                Clip foundClip = m_clips[tag];
                file = FullPath(foundClip);
            }

            if (player.IsInQueue(file))
            {
                Logging.LogError(LogType.Script, $"Clip in playback queue '{tag}'");
                await logger.SendMessageAsync($":warning: | Clip `{tag}` already in queue. Please clear before removing.");
                return;
            }

            lock (m_mutex)
            {
                File.Delete(file);

                RemoveClip(tag);
                m_clips.Remove(tag);

                AddNewClip(shortFilePath, tag, e.Author.Username, 0);
                CreateClip(shortFilePath, tag, 0, e.Author.Username);
            }

            Logging.LogInfo(LogType.Script, $"Updated clip {tag} from file '{link}'.");
            await logger.SendMessageAsync($":white_check_mark:  | `{tag}` Has been updated.");
        }

        public void Rename(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<tag>") || !parameters.ContainsKey("<newtag>"))
            {
                Logging.LogError(LogType.Script, "Tag or Link is not set in add command");
                logger.SendMessageAsync($":warning: | tag or newtag argument is invalid");
                return;
            }

            string tag = parameters["<tag>"].ToString();
            string newtag = parameters["<newtag>"].ToString();
            tag = Utilities.SanitiseString(tag.Truncate(50));
            newtag = Utilities.SanitiseString(newtag.Truncate(50));

            if (!m_clips.ContainsKey(tag))
            {
                Logging.LogError(LogType.Script, $"No tag found '{tag}'");
                logger.SendMessageAsync($":warning: | tag does not exist already.");
                return;
            }

            if (m_clips.ContainsKey(newtag))
            {
                Logging.LogError(LogType.Script, $"tag already exists as '{newtag}' so cant rename");
                logger.SendMessageAsync($":warning: | tag `{newtag}` already exists.");
                return;
            }

            string oldFile = string.Empty;
            string newFile = string.Empty;
            string newShortFile = string.Empty;
            lock (m_mutex)
            {
                Clip foundClip = m_clips[tag];
                oldFile = FullPath(foundClip);
                newFile = Path.Combine(ClipStoreFolder, newtag + ".mp3");
                newShortFile = Path.Combine(ClipStoreShortFolder, newtag + ".mp3");
            }

            if (player.IsInQueue(oldFile))
            {
                Logging.LogError(LogType.Script, $"Clip in playback queue '{tag}'");
                logger.SendMessageAsync($":warning: | Clip `{tag}` already in queue. Please clear before removing.");
                return;
            }

            lock (m_mutex)
            {
                Clip foundClip = m_clips[tag];

                File.Move(oldFile, newFile);

                string addedBy = foundClip.AddedBy;
                int count = foundClip.Count;

                RemoveClip(tag);
                m_clips.Remove(tag);

                if(string.IsNullOrEmpty(addedBy))
                {
                    addedBy = e.Author.Username;
                }

                AddNewClip(newShortFile, newtag, addedBy, count);
                CreateClip(newShortFile, newtag, count, addedBy);
            }

            Logging.LogError(LogType.Script, $"Renamed clip {tag} to '{newtag}'.");
            logger.SendMessageAsync($":white_check_mark:  | `{tag}` Renamed to `{newtag}`.");
        }

        public void Remove(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<tag>"))
            {
                Logging.LogError(LogType.Script, "Tag argument is not valid");
                logger.SendMessageAsync($":warning: | tag argument is invalid");
                return;
            }

            string tag = parameters["<tag>"].ToString();
            tag = Utilities.SanitiseString(tag.Truncate(50));

            if (!m_clips.ContainsKey(tag))
            {
                Logging.LogError(LogType.Script, $"No tag found '{tag}'");
                logger.SendMessageAsync($":warning: | tag does not exist already.");
                return;
            }

            string filePath = string.Empty;
            lock (m_mutex)
            {
                Clip foundClip = m_clips[tag];
                filePath = FullPath(foundClip);
            }

            if (player.IsInQueue(filePath))
            {
                Logging.LogError(LogType.Script, $"Clip in playback queue '{tag}'");
                logger.SendMessageAsync($":warning: | Clip `{tag}` already in queue. Please clear before removing.");
                return;
            }

            lock (m_mutex)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                RemoveClip(tag);
                m_clips.Remove(tag);
            }

            Logging.LogError(LogType.Script, $"Removed clip {tag}.");
            logger.SendMessageAsync($":white_check_mark:  | Removed tag `{tag}`.");
        }
        
        public void Reboot(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            lock (m_mutex)
            {
                m_disposing = true;
                player.Dispose();
                player = null;
                player = new SoundPlayer(m_host.Guild);
                m_disposing = false;
                logger.SendMessageAsync($":tools: | Reboot Complete.");
            }
        }

        public void Connect(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            SocketVoiceChannel voiceChannel = null;
            if (parameters.ContainsKey("<name>"))
            {
                string channelName = ((ValueObject)parameters["<name>"]).ToString();
                voiceChannel = m_host.Guild.GetVoiceChannel(channelName);
                if (voiceChannel == null)
                {
                    logger.SendMessageAsync($":warning: | Could not find a voice channel with the name {channelName}");
                    return;
                }
            }
            else
            {
                voiceChannel = m_host.Guild.GetUserVoiceChannel(e.Author);
                if (voiceChannel == null)
                {
                    logger.SendMessageAsync($":warning: | You are currently not in any voice channel");
                    return;
                }
                else if (player.IsConnected() && voiceChannel == player.ConnectedChannel())
                {
                    return;
                }
            }

            player.ConnectToVoice(logger, voiceChannel);
            Task.Delay(1000);
        }

        public async void AddIntro(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            if (!parameters.ContainsKey("<link>"))
            {
                Logging.LogError(LogType.Script, "Link is not set in add command");
                await logger.SendMessageAsync($":warning: | link argument is invalid");
                return;
            }

            if (m_intros.Count >= m_introLimit && m_introLimit != -1)
            {
                Logging.LogError(LogType.Script, $"Intro limit reached for server {m_host.Guild.Name} | {m_host.Guild.Id}");
                await logger.SendMessageAsync($":warning: | You have reached the maximum intro limit of {m_introLimit}. Remove an intro before adding a new one.");
                return;
            }

            ulong id            = e.Author.Id;
            string idString     = e.Author.Id.ToString();
            string username     = e.Author.Username; 
            string link         = parameters["<link>"].ToString();

            if(parameters.ContainsKey("<user>"))
            {
                ValueObject valueObj = parameters["<user>"] as ValueObject;
                if (valueObj.IsULong)
                {
                    id = valueObj.AsULong;
                    SocketGuildUser user = m_host.Guild.GetUser(id);
                    if(user == null)
                    {
                        Logging.LogError(LogType.Script, $"No user found with the id '{id}'");
                        await logger.SendMessageAsync($":warning: | No user found with the `{id}`.");
                        return;
                    }

                    username = user.Username;
                    idString = id.ToString();
                }
            }

            string filePath = string.Empty;
            lock (m_mutex)
            {
                if (m_intros.ContainsKey(id))
                {
                    Clip introFound = m_intros[id];
                    filePath = FullPath(introFound);
                }
            }

            if (player.IsInQueue(filePath))
            {
                Logging.LogError(LogType.Script, $"Intro in playback queue '{id}'");
                await logger.SendMessageAsync($":warning: | Intro `{id}` already in queue. Please clear before updating.");
                return;
            }

            lock (m_mutex)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                RemoveIntro(id);
                m_intros.Remove(id);
            }

            string shortFilePath = Path.Combine(ClipStoreShortFolder, idString + ".mp3");
            string downloadLocation = await TryDownloadFile(idString, link, introTimeMax, logger);
            if (string.IsNullOrEmpty(downloadLocation))
            {
                return;
            }
            
            lock (m_mutex)
            {
                AddNewIntro(shortFilePath, id, username);
                CreateIntro(shortFilePath, id, username);
            }

            Logging.LogError(LogType.Script, $"Added new into for user {username} from file '{link}'.");
            await logger.SendMessageAsync($"Added new into for user `{username}`.");
        }

        public void RemoveInto(Dictionary<string, object> parameters, SocketMessage e)
        {
            ISocketMessageChannel logger = LogChannel(e.Channel);

            ulong id            = e.Author.Id;
            string idString     = e.Author.Id.ToString();

            if(parameters.ContainsKey("<user>"))
            {
                ValueObject valueObj = parameters["<user>"] as ValueObject;
                if(valueObj.IsULong)
                {
                    id          = valueObj.AsULong;
                    idString    = id.ToString();
                }
            }

            string filePath = string.Empty;
            lock (m_mutex)
            {
                if (m_intros.ContainsKey(id))
                {
                    Clip introFound = m_intros[id];
                    filePath = FullPath(introFound);
                }
            }

            if (player.IsInQueue(filePath))
            {
                Logging.LogError(LogType.Script, $"Intro in playback queue '{id}'");
                logger.SendMessageAsync($":warning: | Intro `{id}` already in queue. Please clear before removing.");
                return;
            }

            lock (m_mutex)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                RemoveIntro(id);
                m_intros.Remove(id);
            }

            Logging.LogError(LogType.Script, $"Intro removed '{id}'");
            logger.SendMessageAsync($":white_check_mark: | Intro for user `{e.Author.Username}` Removed.");
        }

        public void UserConnected(Dictionary<string, object> parameters, SocketVoiceConnection e)
        {
            ulong id = e.User.Id;
            if(m_intros.ContainsKey(id))
            {
                SocketVoiceChannel voiceChannel = m_host.Guild.GetUserVoiceChannel(e.User);
                if (voiceChannel == null)
                {
                    Logging.LogError(LogType.Script, $"Could not find a voice channel with user {e.User.Username} in. {m_host.Guild.Name}");
                    return;
                }
                else if (!player.IsConnected() || voiceChannel != player.ConnectedChannel())
                {
                    player.ConnectToVoice(null, voiceChannel);
                    Task.Delay(1000);
                    if (!player.IsConnected())
                    {
                        Logging.LogError(LogType.Script, $"Failed to connect player on guild {m_host.Guild.Name}");
                        return;
                    }
                }

                int chance = Utilities.Rand(0, 100);
                if (e.User.Id == 138409183107219456 && m_clips.ContainsKey("rock2") && m_clips.ContainsKey("rock3") && chance <= 5)
                {
                    int count = Utilities.Rand(2, 6);
                    EnqueueClip("rock2", count);
                    EnqueueClip("rock3", 1);
                }
                else
                {
                    EnqueueIntro(e.User.Id);
                }
            }
            else
            {
                int chance = Utilities.Rand(0, 100);
                if (e.User.Id == 138409183107219456 && m_clips.ContainsKey("rock2") && m_clips.ContainsKey("rock3") && chance <= 5)
                {
                    int count = Utilities.Rand(2, 6);
                    EnqueueClip("rock2", count);
                    EnqueueClip("rock3", 1);
                }
            }
        }


        private void LoadExistingClips()
        {
            if (!File.Exists(ClipStore))
            {
                XDocument newClipStore = new XDocument();
                XElement element = new XElement("AudioClipStore");
                newClipStore.Add(element);
                newClipStore.Save(ClipStore);
                Logging.Log(LogType.Script, LogLevel.Info, $"Creating new clip store {ClipStore}");
                return;
            }

            List<string> duplicateTags = new List<string>();

            XDocument clipStoreDoc = XDocument.Load(ClipStore);
            foreach (var element in clipStoreDoc.Root.Elements())
            {
                string file = string.Empty;
                if (!element.TryGetAttribute("name", out file)) continue;
                string tag = string.Empty;
                if (!element.TryGetAttribute("tag", out tag)) continue;
                int count = 0;
                if (!element.TryGetAttribute("count", out count)) { }
                string addedBy = string.Empty;
                if (!element.TryGetAttribute("add", out addedBy)) { }

                if (!CreateClip(file, tag, count, addedBy))
                {
                    duplicateTags.Add(tag);
                    Logging.Log(LogType.Script, LogLevel.Error, $"Failed to add clip {tag} file {file} as its already added.");
                }
            }

            foreach(var dupe in duplicateTags)
            {
                RemoveClip(dupe);
            }
        }

        private void AddNewClip(string file, string tag, string user, int count = 0)
        {
            if(string.IsNullOrEmpty(file) || string.IsNullOrEmpty(tag))
            {
                return;
            }

            if(string.IsNullOrEmpty(user))
            {
                user = string.Empty;
            }

            user = Utilities.SanitiseString(user.Truncate(50));

            lock (m_mutex)
            {
                if (!File.Exists(ClipStore))
                {
                    XDocument newClipStore = new XDocument();
                    XElement element = new XElement("AudioClipStore");
                    XElement clip = new XElement("AudioClip");
                    clip.Add(new XAttribute("name", file));
                    clip.Add(new XAttribute("tag", tag));
                    clip.Add(new XAttribute("count", count));
                    clip.Add(new XAttribute("add", user));
                    element.Add(clip);
                    newClipStore.Add(element);
                    newClipStore.Save(ClipStore);
                    Logging.Log(LogType.Script, LogLevel.Info, $"Creating new clip store {ClipStore}");
                    return;
                }
                else
                {
                    XDocument clipStoreDoc = XDocument.Load(ClipStore);
                    XElement clip = new XElement("AudioClip");
                    clip.Add(new XAttribute("name", file));
                    clip.Add(new XAttribute("tag", tag));
                    clip.Add(new XAttribute("count", count));
                    clip.Add(new XAttribute("add", user));
                    clipStoreDoc.Root.Add(clip);
                    clipStoreDoc.Save(ClipStore);
                }
            }
        }

        private void RemoveClip(string tag)
        {
            if (File.Exists(ClipStore))
            {
                lock (m_mutex)
                {
                    XDocument clipStoreDoc = XDocument.Load(ClipStore);
                    XElement root = clipStoreDoc.Root;
                    XElement item = root.FindElementByAttribute("tag", tag);
                    if (item != null)
                    {
                        item.Remove();
                        clipStoreDoc.Save(ClipStore);
                    }
                }
            }
        }

        private void UpdateClip(string tag, int count)
        {
            if (File.Exists(ClipStore))
            {
                lock (m_mutex)
                {
                    XDocument clipStoreDoc = XDocument.Load(ClipStore);
                    XElement root = clipStoreDoc.Root;
                    XElement item = root.FindElementByAttribute("tag", tag);
                    if (item != null)
                    {
                        if (!item.UpdateAttribute("count", count))
                        {
                            item.Add(new XAttribute("count", count));
                        }

                        if(!item.HasAttribute("add"))
                        {
                            item.Add(new XAttribute("add", "Memé - Bot"));
                        }
                    }
                    clipStoreDoc.Save(ClipStore);
                }
            }
        }

        private bool CreateClip(string file, string tag, int count, string addedBy)
        {
            lock (m_mutex)
            {
                if (!m_clips.ContainsKey(tag))
                {
                    Clip newClip = new Clip() { File = file, Tag = tag, Count = count, AddedBy = addedBy };
                    m_clips.Add(tag, newClip);
                    return true;
                }
            }

            return false;
        }

        private bool EnqueueRandom(int count = 1)
        {
            lock (m_mutex)
            {
                Clip random = RandomClip();
                if (random != null)
                {
                    m_autoDisconnectTimer.Stop();
                    m_autoDisconnectTimer.Start();

                    Logging.Log(LogType.Script, LogLevel.Info, $"Enqueued Clip {random.Tag}.");
                    player.EnqueueClip(FullPath(random), count);
                    UpdateClip(random.Tag, ++random.Count);
                    return true;
                }
            }
            return false;
        }

        private bool EnqueueClip(string tag, int count = 1)
        {
            lock (m_mutex)
            {
                if (m_clips.ContainsKey(tag))
                {
                    m_autoDisconnectTimer.Stop();
                    m_autoDisconnectTimer.Start();

                    Clip clip = m_clips[tag];
                    Logging.Log(LogType.Script, LogLevel.Info, $"Enqueued Clip {clip.Tag}.");
                    player.EnqueueClip(FullPath(clip), count);
                    UpdateClip(clip.Tag, ++clip.Count);
                    return true;
                }
            }
            return false;
        }

        private bool EnqueueIntro(ulong id)
        {
            lock (m_mutex)
            {
                if (m_intros.ContainsKey(id))
                {
                    m_autoDisconnectTimer.Stop();
                    m_autoDisconnectTimer.Start();

                    Clip clip = m_intros[id];
                    Logging.Log(LogType.Script, LogLevel.Info, $"Enqueued Clip {clip.Tag}.");
                    player.EnqueueClip(FullPath(clip));
                    return true;
                }
            }
            return false;
        }

        private string FullPath(Clip clip)
        {
            return Path.Combine(Utilities.AssemblyPath, clip.File);
        }

        private Clip RandomClip()
        {
            lock (m_mutex)
            {
                if (m_clips.Count > 0)
                {
                    int random = Utilities.Rand(0, m_clips.Count - 1);
                    return m_clips.ElementAt(random).Value;
                }
            }

            return null;
        }

        private async Task<string> TryDownloadFile(string tag, string link, TimeSpan maxLimit, ISocketMessageChannel logger)
        {
            string downloadLocation = Path.Combine(ClipStoreFolder, tag + ".mp3");

            if(!link.StartsWith("http:") && !link.StartsWith("https:"))
            {
                Logging.LogError(LogType.Script, $"Cant download file {link} as its not a http:// or https:// link");
                await logger.SendMessageAsync($":warning: | Cant download file {link} as its not a http:// or https:// link");
                return string.Empty;
            }

            if(File.Exists(downloadLocation))
            {
                File.Delete(downloadLocation);
            }

            try
            {
                CancellationTokenSource token = new CancellationTokenSource();
                bool downloaded = await Utilities.TryDownloadFileAsync(link, downloadLocation, 750.KiB(), token.Token);
                if (!downloaded)
                {
                    Logging.LogError(LogType.Script, $"File failed to download (file size most likely)");
                    await logger.SendMessageAsync($":warning: | Failed to download file (Is the file size too big > 750kb).");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Script, ex, $"Something went wrong while trying to download file '{link}'");
                await logger.SendMessageAsync($":warning: | Something went wrong while trying to download file.");
                return string.Empty;
            }

            if (!SoundHelper.IsClipMP3(downloadLocation))
            {
                Logging.LogError(LogType.Script, $"Download file '{link}' is not a Mp3 file type.");
                await logger.SendMessageAsync($":warning: | Provided link is not an mp3 file type.");
                File.Delete(downloadLocation);
                return string.Empty;
            }

            TimeSpan clipLength = SoundHelper.GetClipTime(downloadLocation);
            if (clipLength > maxLimit || clipLength == TimeSpan.MaxValue)
            {
                Logging.LogError(LogType.Script, $"Download file '{link}' is too long {clipLength}.");
                await logger.SendMessageAsync($":warning: | Provided link plays for longer than {maxLimit.Seconds} seconds.");
                File.Delete(downloadLocation);
                return string.Empty;
            }

            return downloadLocation;
        }

        private ISocketMessageChannel LogChannel(ISocketMessageChannel backupChannel)
        {
            if (m_fixedChannel != null)
                return m_fixedChannel;
            else
                return backupChannel;
        }

        private string MinimumLevel()
        {
            if (m_host.Guild.Id != memeTeamId)
                return "Meme Bronze,Meme Silver,Meme Gold";
            else
                return string.Empty;
        }

        private string MediumLevel()
        {
            if (m_host.Guild.Id != memeTeamId)
                return "Meme Silver,Meme Gold";
            else
                return string.Empty;
        }

        private string MaximumLevel()
        {
            if (m_host.Guild.Id != memeTeamId)
                return "Meme Gold";
            else
                return string.Empty;
        }

        private Embed CreateAddedEmebed(Clip newClip, SocketUser user)
        {
            try
            {
                EmbedBuilder eb = new EmbedBuilder();
                eb.Color = Color.DarkBlue;

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.Name = "New Meme added:";
                eab.IconUrl = user.GetAvatarUrl();
                eb.Author = eab;

                string reducedName = ":joy: :ok_hand:";
                eb.Description = reducedName;

                EmbedFieldBuilder efbTag = new EmbedFieldBuilder();
                efbTag.Name = "Tag".Markdown(DiscordMarkdown.Underline);
                efbTag.Value = newClip.Tag.Truncate(EmbedBuilder.MaxDescriptionLength - 10).Markdown(DiscordMarkdown.BoldItalics);
                efbTag.IsInline = true;
                eb.Fields.Add(efbTag);

                TimeSpan clipLength = SoundHelper.GetClipTime(FullPath(newClip));
                string lengthString = clipLength.ToString("s\\.ffff") + " Seconds";
                EmbedFieldBuilder efbDuration = new EmbedFieldBuilder();
                efbDuration.Name    = "Duration".Markdown(DiscordMarkdown.Underline);
                efbDuration.Value   = lengthString.Markdown(DiscordMarkdown.BoldItalics);
                efbDuration.IsInline = true;
                eb.Fields.Add(efbDuration);

                return eb.Build();
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Script, ex, "Failed to build embed");
                return null;
            }
        }

        private void AutoDisconnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!m_disposing && player.IsQueueEmpty() && player.IsConnected())
            {
                player.DisconnectFromVoice();
                Logging.LogInfo(LogType.Script, $"Timer elapsed on guild {m_host.Guild.Name} auto disconnecting.");
            }
        }

        private void LoadExistingIntros()
        {
            if (!File.Exists(IntroStore))
            {
                XDocument newClipStore = new XDocument();
                XElement element = new XElement("AudioClipStore");
                newClipStore.Add(element);
                newClipStore.Save(IntroStore);
                Logging.Log(LogType.Script, LogLevel.Info, $"Creating new clip store {IntroStore}");
                return;
            }

            List<ulong> duplicateTags = new List<ulong>();

            XDocument clipStoreDoc = XDocument.Load(IntroStore);
            foreach (var element in clipStoreDoc.Root.Elements())
            {
                string file = string.Empty;
                if (!element.TryGetAttribute("name", out file)) continue;
                ulong userId = 0;
                if (!element.TryGetAttribute("user", out userId)) continue;
                string addedBy = string.Empty;
                if (!element.TryGetAttribute("userName", out addedBy)) { }

                if (!CreateIntro(file, userId, addedBy))
                {
                    duplicateTags.Add(userId);
                    Logging.Log(LogType.Script, LogLevel.Error, $"Failed to add clip {userId} file {file} as its already added.");
                }
            }

            foreach (var dupe in duplicateTags)
            {
                RemoveIntro(dupe);
            }
        }

        private void AddNewIntro(string file, ulong id, string user)
        {
            if (string.IsNullOrEmpty(file) || id == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(user))
            {
                user = string.Empty;
            }

            user = Utilities.SanitiseString(user.Truncate(50));

            lock (m_mutex)
            {
                if (!File.Exists(IntroStore))
                {
                    XDocument newIntroStore = new XDocument();
                    XElement element = new XElement("AudioIntroStore");
                    XElement intro = new XElement("AudioIntro");
                    intro.Add(new XAttribute("name", file));
                    intro.Add(new XAttribute("user", id.ToString()));
                    intro.Add(new XAttribute("userName", user));
                    element.Add(intro);
                    newIntroStore.Add(element);
                    newIntroStore.Save(IntroStore);
                    Logging.Log(LogType.Script, LogLevel.Info, $"Creating new intro store {IntroStore}");
                    return;
                }
                else
                {
                    XDocument introStoreDoc = XDocument.Load(IntroStore);
                    XElement intro = new XElement("AudioClip");
                    intro.Add(new XAttribute("name", file));
                    intro.Add(new XAttribute("user", id.ToString()));
                    intro.Add(new XAttribute("userName", user));
                    introStoreDoc.Root.Add(intro);
                    introStoreDoc.Save(IntroStore);
                }
            }
        }

        private void RemoveIntro(ulong id)
        {
            if (File.Exists(IntroStore))
            {
                lock (m_mutex)
                {
                    XDocument introStoreDoc = XDocument.Load(IntroStore);
                    XElement root = introStoreDoc.Root;
                    XElement item = root.FindElementByAttribute("user", id.ToString());
                    if (item != null)
                    {
                        item.Remove();
                        introStoreDoc.Save(IntroStore);
                    }
                }
            }
        }

        private bool CreateIntro(string file, ulong id, string userName)
        {
            lock (m_mutex)
            {
                if (!m_intros.ContainsKey(id))
                {
                    Clip newClip = new Clip() { File = file, Tag = id.ToString(), Count = 0, AddedBy = userName };
                    m_intros.Add(id, newClip);
                    return true;
                }
            }

            return false;
        }
    }
}

