using Botcord.Core;
using Botcord.Core.DiscordExtensions;
using Botcord.Core.Extensions;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VideoLibrary;

namespace Botcord.Audio
{
    public enum Location
    {
        Last,
        Next
    }

    public enum StreamState
    {
        Resolving,
        Queued,
        Playing,
        Completed
    }

    public class MediaPlayer : IDisposable
    {
        public Playlist ActivePlaylist
        {
            get { return m_activePlaylist; }
        }

        public bool Repeat
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.Repeat;
                return false;
            }
            set
            {
                if (m_activePlaylist != null) m_activePlaylist.Repeat = value;
            }
        }

        public bool ShuffleOnLoad
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.ShuffleOnLoad;
                return false;
            }
            set
            {
                if (m_activePlaylist != null) m_activePlaylist.ShuffleOnLoad = value;
            }
        }

        public bool Pause
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.Paused;
                return false;
            }
            set
            {
                if (m_activePlaylist != null) m_activePlaylist.Paused = value;
            }
        }

        public string LastSong
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.LastSong;
                return "Playlist not active";
            }
        }

        public string CurrentSong
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.CurrentSong;
                return "Playlist not active";
            }
        }

        public string NextSong
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.NextSong;
                return "Playlist not active";
            }
        }

        public Song Last
        {
            get
            {
                if (m_activePlaylist != null)
                {
                    if (m_activePlaylist.Last != null)
                        return m_activePlaylist.Last;
                }

                return null;
            }
        }

        public Song Active
        {
            get
            {
                if(m_activePlaylist != null)
                {
                    if (m_activePlaylist.Current != null)
                        return m_activePlaylist.Current;
                }

                return null;
            }
        }

        public Song Next
        {
            get
            {
                if (m_activePlaylist != null)
                {
                    if (m_activePlaylist.Next != null)
                        return m_activePlaylist.Next;
                }

                return null;
            }
        }

        public Song NextInQueue
        {
            get
            {
                if (m_activePlaylist != null)
                {
                    if (m_activePlaylist.Next == null && m_activePlaylist.Current != null)
                        return m_activePlaylist.Current;
                    else
                        return m_activePlaylist.Next;
                }
                return null;
            }
        }

        public Song LastInQueue
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.LastAdded;
                return null;
            }
        }

        public float Volume
        {
            get
            {
                if (m_activePlaylist != null) return m_activePlaylist.Volume;
                return 0.0f;
            }
            set
            {
                if (m_activePlaylist != null) m_activePlaylist.Volume = value;
            }
        }

        public ISocketMessageChannel FixedChannel
        {
            get { return m_fixedChannel; }
            set
            {
                m_fixedChannel = value;
                UpdatePlayerSettings();
            }
        }

        private Playlist m_activePlaylist = null;
        private Dictionary<string, Playlist> m_avaliablePlaylists = new Dictionary<string, Playlist>();

        private bool m_destroyed = false;
        private bool m_loadingPlaylists = false;
        private bool m_loadedPlaylists = false;

        private Task m_playbackThread;
        private CancellationTokenSource m_playbackCancellationSource = new CancellationTokenSource();

        private SocketGuild m_guild = null;
        private SocketVoiceChannel m_connectedChannel = null;
        private IAudioClient m_audioClient = null;

        private object m_threadMutex = new object();

        private ISocketMessageChannel m_fixedChannel = null;

        public MediaPlayer(SocketGuild guild)
        {
            m_avaliablePlaylists.Add(Playlist.DefaultPlaylist, new Playlist(guild));
            m_guild = guild;

            LoadPlayerSettings();
        }

        #region Connection

        public bool IsConnected()
        {
            if (m_connectedChannel != null && m_audioClient?.ConnectionState == ConnectionState.Connected)
            {
                return true;
            }

            return false;
        }

        public bool ConnectToVoice(ISocketMessageChannel requester, string channelName = "")
        {
            SocketVoiceChannel channel = m_guild.GetVoiceChannel(channelName);
            if (channel == null)
            {
                Logging.LogError(LogType.Bot, $"Channel name {channelName} not on server {m_guild.Name}.");
                return false;
            }

            return ConnectToVoice(requester, channel);
        }

        public bool ConnectToVoice(ISocketMessageChannel requester, SocketVoiceChannel channel)
        {
            if (m_playbackThread == null)
                m_playbackThread = Task.Factory.StartNew(PlaybackThread, m_playbackCancellationSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (m_loadedPlaylists == false && m_loadingPlaylists == false)
            {
                Utilities.Execute(async () => await LoadPlaylists(requester));
            }

            if (m_connectedChannel != null && m_audioClient?.ConnectionState == ConnectionState.Connected)
            {
                if (channel.Id == m_connectedChannel.Id)
                    return true;
            }

            m_audioClient = m_guild.ConnectToVoice(channel);
            if (m_audioClient == null)
            {
                Logging.LogError(LogType.Bot, $"Audio client is not connected on server {m_guild.Name}.");
                return false;
            }

            m_connectedChannel = channel;

            return true;
        }

        public bool ChangeChannel(ISocketMessageChannel requester, string channelName = "")
        {
            if (m_loadedPlaylists == false && m_loadingPlaylists == false)
            {
                Utilities.Execute(async () => await LoadPlaylists(requester));
            }

            if (channelName.Equals(m_connectedChannel.Name))
            {
                return ReconnectToVoice(requester);
            }
            else
            {
                Utilities.ExecuteAndWait(async () => await m_audioClient.StopAsync());

                if (m_audioClient.ConnectionState == ConnectionState.Disconnected && ConnectToVoice(requester, channelName))
                {
                    if (m_audioClient == null)
                    {
                        Logging.LogError(LogType.Bot, $"Audio client is not connected on server {m_guild.Name}.");
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        public bool ReconnectToVoice(ISocketMessageChannel requester)
        {
            if (m_loadedPlaylists == false && m_loadingPlaylists == false)
            {
                Utilities.Execute(async () => await LoadPlaylists(requester));
            }

            if (m_audioClient != null)
            {
                return false;
            }

            if (ConnectToVoice(requester, m_connectedChannel))
            {
                if (m_audioClient == null)
                {
                    Logging.LogError(LogType.Bot, $"Audio client is not connected on server {m_guild.Name}.");
                    return false;
                }

                return true;
            }

            return false;
        }

        public bool DisconnectFromVoice()
        {
            Utilities.ExecuteAndWait(async () => await m_audioClient.StopAsync());
            if (m_audioClient.ConnectionState == ConnectionState.Disconnected)
            {
                m_audioClient = null;
                return true;
            }

            Logging.LogError(LogType.Bot, $"Failed to disconnect. Bot may already be disconnected on server {m_guild.Name}.");
            return false;
        }

        #endregion

        #region Playlist

        public bool NewPlaylist(string name)
        {
            if (!m_loadedPlaylists)
            {
                return false;
            }

            if (!m_avaliablePlaylists.ContainsKey(name))
            {
                Playlist newPlaylist = new Playlist(name, m_guild);
                m_avaliablePlaylists.Add(name, newPlaylist);
                ChangePlaylist(name);
                return true;
            }

            return false;
        }

        public bool ChangePlaylist(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = Playlist.DefaultPlaylist;

            if (!m_loadedPlaylists)
            {
                return false;
            }

            if (m_avaliablePlaylists.ContainsKey(name))
            {
                m_activePlaylist?.Stop();
                m_activePlaylist = m_avaliablePlaylists[name];
                m_activePlaylist.Paused = false;
                return true;
            }

            return false;
        }

        public bool DeletePlaylist(string name)
        {
            if (name == Playlist.DefaultPlaylist)
            {
                return false;
            }

            if (!m_loadedPlaylists)
            {
                return false;
            }

            if (m_avaliablePlaylists.ContainsKey(name))
            {
                Playlist pl = m_avaliablePlaylists[name];
                if (pl == m_activePlaylist)
                {
                    m_activePlaylist.Stop();
                    m_activePlaylist = null;
                }

                string fileLoc = pl.PlaylistFile;
                if (File.Exists(fileLoc))
                    File.Delete(fileLoc);

                m_avaliablePlaylists.Remove(name);

                return true;
            }

            return false;
        }

        #endregion

        #region Controls

        //Add
        public async Task<bool> AddSong(ISocketMessageChannel requester, string location, Location addLocation = Location.Last, string playlist = "")
        {
            if(!m_loadedPlaylists)
            {
                var returnItem = requester.SendMessageAsync(":warning: Playlist is still loading please wait.");
                await returnItem;
                return false;
            }

            if(string.IsNullOrEmpty(location) || location.Length < 3)
            { 
                await requester.SendMessageAsync(":warning: Location is blank or invalid so cant play.");
                Logging.LogError(LogType.Bot, "Location is invalid for the song request '{0}'", location);
                return false;
            }

            if(m_activePlaylist == null)
            {
                Logging.LogError(LogType.Bot, ":warning: No playlist active so nothing to queue into.");
                await requester.SendMessageAsync("No playlist active so nothing to queue into.");
                return false;
            }

            if(!string.IsNullOrEmpty(playlist) && !m_avaliablePlaylists.ContainsKey(playlist))
            {
                Logging.LogError(LogType.Bot, $":warning: No playlist with name {playlist} so nothing to queue into.");
                await requester.SendMessageAsync($"No playlist with name {playlist} so nothing to queue into.");
                return false;
            }

            try
            {
                await requester.SendMessageAsync($":mag: | Searching for song (playlist can take a while).");
                IEnumerable<Song> resolvedSongs = await ResolveSong(location).ConfigureAwait(false);
                bool isPlaylist = resolvedSongs.Count() > 1;
                if(isPlaylist)
                {
                    await requester.SendMessageAsync($":minidisc: | Playlist with {resolvedSongs.Count()} songs to resolve");
                }

                foreach (var song in resolvedSongs)
                {
                    lock(m_threadMutex)
                    {
                        Playlist addToPlaylist = m_activePlaylist;
                        int queueLoc = -1;
                        if (string.IsNullOrEmpty(playlist))
                        {
                            queueLoc = m_activePlaylist.AddSong(song, addLocation);
                            if(queueLoc != -1) song.SetPlaylist(m_activePlaylist);
                        }
                        else
                        {
                            queueLoc = m_avaliablePlaylists[playlist].AddSong(song, addLocation);
                            if(queueLoc != -1) song.SetPlaylist(m_avaliablePlaylists[playlist]);
                        }

                        if (queueLoc == -1 && !isPlaylist)
                        {
                            requester.SendMessageAsync("Failed to add song to playlist may already be in playlist or is an invalid link");
                            return false;
                        }
                    }
                }

                if(isPlaylist)
                {
                    await requester.SendMessageAsync($"Added  {resolvedSongs.Count()} Songs to queue.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Exception occured in audio player while trying to resolve and queue song");
                await requester.SendMessageAsync("Failed to add song to playlist could not resolve request.");
                return false;
            }
        }

        //SetNext
        public async Task SetSong(ISocketMessageChannel requester, string name, Location setLocation = Location.Last, string playlist = "")
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlist is still loading please wait.");
                return;
            }

            try
            {
                lock (m_threadMutex)
                {
                    Playlist addToPlaylist = m_activePlaylist;
                    bool success = false;
                    if (!string.IsNullOrEmpty(playlist))
                        success = m_activePlaylist.SetSong(name, setLocation);
                    else
                        success = m_avaliablePlaylists[playlist].SetSong(name, setLocation);

                    if (!success)
                        requester.SendMessageAsync($"Failed set song {name} to play {setLocation}");
                    else
                        requester.SendMessageAsync($"Set song {name} to play {setLocation} in queue.");
                }
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Exception occured in audio player while trying to resolve and queue song");
                await requester.SendMessageAsync("Failed to add song to playlist could not resolve request.");
            }
        }

        //Requeue
        public async Task Requeue(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                m_activePlaylist.RequeueAll();
            }
        }

        //Skip
        public async Task Skip(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                m_activePlaylist.Skip();
            }
        }

        //Remove
        public async Task Remove(ISocketMessageChannel requester, string name)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                bool removed = m_activePlaylist.RemoveSong(name);
                if(removed)
                {
                    await requester.SendMessageAsync(":eject: | Removed Song Successfully");
                }
                else
                {
                    await requester.SendMessageAsync(":interrobang: | Failed to Remove Song");
                }
            }
        }

        //RemoveLast
        public async Task RemoveLast(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                if(m_activePlaylist.LastAdded != null)
                    await Remove(requester, m_activePlaylist.LastAdded.Name);
            }
        }

        //RemoveNext
        public async Task RemoveNext(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                await Remove(requester, m_activePlaylist.Next.Name);
            }
        }

        //Clear
        public async Task Clear(ISocketMessageChannel requester, bool all = false)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                if (all)
                    m_activePlaylist.Clear();
                else
                    m_activePlaylist.ClearPlaying();
            }
        }

        //Shuffle
        public async Task Shuffle(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                m_activePlaylist.Shuffle();
                await requester.SendMessageAsync("Playlist shuffled");
            }
        }

        //Stop
        public async Task Stop(ISocketMessageChannel requester)
        {
            if (!m_loadedPlaylists)
            {
                await requester.SendMessageAsync("Playlists still loading please wait...");
                return;
            }

            if (m_activePlaylist != null)
            {
                m_activePlaylist.Stop();
            }
        }

        #endregion

        #region Player Settings

        private void LoadPlayerSettings()
        {
            string fileLocation = Path.Combine(Utilities.DataFolder, m_guild.Id.ToString(),$"mediaplayer.xml");
            if (!File.Exists(fileLocation))
            {
                CreatePlayerSettings(fileLocation);
                return;
            }

            XDocument doc = XDocument.Load(fileLocation);
            XElement root = doc.Root;

            XElement fixedChannelElement = root.Element("FixedChannel");
            if (fixedChannelElement != null)
            {
                ulong channelId = 0;
                if(fixedChannelElement.TryGetAttribute("id", out channelId))
                {
                    SocketTextChannel channel = m_guild.GetTextChannel(channelId);
                    if(channel != null)
                    {
                        FixedChannel = channel;
                    }
                }
            }
        }

        private void CreatePlayerSettings(string file)
        {
            if(!Directory.Exists(Path.GetDirectoryName(file)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
            }

            XDocument doc = new XDocument();
            XElement root = new XElement("settings");
            XElement fixedChannelElement = new XElement("FixedChannel");
            root.Add(fixedChannelElement);

            doc.Add(root);
            doc.Save(file);
        }

        public void UpdatePlayerSettings()
        {
            string fileLocation = Path.Combine(Utilities.DataFolder, m_guild.Id.ToString(), $"mediaplayer.xml");
            if (!File.Exists(fileLocation))
            {
                CreatePlayerSettings(fileLocation);
                return;
            }

            XDocument doc = XDocument.Load(fileLocation);
            XElement root = doc.Root;

            if (FixedChannel != null)
            {
                XElement fixedChannelElement = root.Element("FixedChannel");
                if (fixedChannelElement.Attribute("id") != null)
                    fixedChannelElement.Attribute("id").Value = FixedChannel.Id.ToString();
                else
                    fixedChannelElement.Add(new XAttribute("id", FixedChannel.Id.ToString()));
            }
            else
            {
                XElement fixedChannelElement = root.Element("FixedChannel");
                fixedChannelElement.Remove();
            }

            doc.Save(fileLocation);
        }

        #endregion

        private void PlaybackThread()
        {
            while (!m_destroyed)
            {
                try
                {
                    if (m_activePlaylist != null)
                        Utilities.ExecuteAndWait(async () => await m_activePlaylist.Play(m_audioClient));
                    else
                        Utilities.ExecuteAndWait(async () => await Task.Delay(500).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    Logging.LogException(LogType.Bot, ex, $"Failed to play song {m_activePlaylist.CurrentSong} on Audio Play for server {m_guild.Name}.");
                }
            }
        }

        public void Dispose()
        {
            if (m_activePlaylist != null)
            {
                m_activePlaylist.Stop();
            }

            m_destroyed = true;
            bool success = Utilities.ExecuteAndWait(async (token) =>
            {
                while (m_playbackThread.IsCompleted || m_playbackThread.IsCanceled)
                {
                    await Task.Delay(500);
                    if (token.IsCancellationRequested)
                        break;
                }
            }, 10000);

            if (!success)
            {
                Logging.LogWarn(LogType.Bot, "Audio Playback thread failed to exit in requested timeout so will be aborted.");
                m_playbackCancellationSource.Cancel();
            }
            else
            {
                Logging.LogInfo(LogType.Bot, "Audio Playback thread successfully aborted");
            }
        }

        private async Task LoadPlaylists(ISocketMessageChannel requester)
        {
            m_loadingPlaylists = true;

            await requester.SendMessageAsync("Building Playlists please wait...");
            if (m_guild.Id != 1 || m_guild.Id == 0)
            {
                string dir = Path.Combine(Utilities.DataFolder, m_guild.Id.ToString(), "Playlists");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                foreach (var playlistXML in Directory.EnumerateFiles(dir, "*.xml"))
                {
                    Playlist pl = new Playlist(Path.GetFileNameWithoutExtension(playlistXML), m_guild);
                    if (pl.IsValid)
                        m_avaliablePlaylists.Add(pl.Name, pl);
                    else
                        await requester.SendMessageAsync($"Failed to add playlist {pl.Name}");
                }

                await requester.SendMessageAsync("Building playlists is complete");
                m_activePlaylist = m_avaliablePlaylists[Playlist.DefaultPlaylist];
                m_loadedPlaylists = true;
                m_loadingPlaylists = false;
            }
        }

        #region Resolvers

        public static async Task<IEnumerable<Song>> ResolveSong(string query)
        {
            List<Song> m_songs = new List<Song>();

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));
            try
            {
                if (IsRadioLink(query))
                {
                    query = await HandleStreamContainers(query).ConfigureAwait(false) ?? query;

                    m_songs.Add(new Song(new SongInfo
                    {
                        Uri = query,
                        Title = $"{query}",
                        Provider = "Radio Stream",
                        Query = query
                    }));
                    return m_songs;
                }
                else
                {
                    var links = await Utilities.FindYoutubeUrlByKeywords(query).ConfigureAwait(false);
                    foreach (var link in links)
                    {
                        if (string.IsNullOrWhiteSpace(link))
                            throw new OperationCanceledException("Not a valid youtube query.");
                        var allVideos = await Task.Factory.StartNew(async () => await YouTube.Default.GetAllVideosAsync(link).ConfigureAwait(false)).Unwrap().ConfigureAwait(false);
                        var videos = allVideos.Where(v => v.AdaptiveKind == AdaptiveKind.Audio);

                        YouTubeVideo video = null;
                        try
                        {
                            video = videos
                                .Where(v => v.AudioBitrate < 192)
                                .OrderByDescending(v => v.AudioBitrate)
                                .FirstOrDefault();
                        }
                        catch { }

                        if (video == null) // do something with this error
                        {
                            continue;
                            //throw new Exception("Could not load any video elements based on the query.");
                        }

                        var m = Regex.Match(query, @"\?t=((?<h>\d*)h)?((?<m>\d*)m)?((?<s>\d*)s?)?");
                        int gotoTime = 0;
                        if (m.Captures.Count > 0)
                        {
                            int hours;
                            int minutes;
                            int seconds;

                            int.TryParse(m.Groups["h"].ToString(), out hours);
                            int.TryParse(m.Groups["m"].ToString(), out minutes);
                            int.TryParse(m.Groups["s"].ToString(), out seconds);

                            gotoTime = hours * 60 * 60 + minutes * 60 + seconds;
                        }

                        m_songs.Add(new Song(new SongInfo
                        {
                            Title = video.Title.Substring(0, video.Title.Length - 10), // removing trailing "- You Tube"
                            Provider = "YouTube",
                            Uri = await video.GetUriAsync(),
                            Duration = await Utilities.GetVideoDuration(link),
                            Query = link,
                        }));

                        m_songs.Last().SkipTo = gotoTime;
                    }
                    return m_songs;
                }
            }
            catch (Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to resolve song");
            }
            return m_songs;
        }

        private static async Task<string> HandleStreamContainers(string query)
        {
            string file = null;
            try
            {
                file = await Utilities.GetResponseStringAsync(query).ConfigureAwait(false);
            }
            catch
            {
                return query;
            }
            if (query.Contains(".pls"))
            {
                //File1=http://armitunes.com:8000/
                //Regex.Match(query)
                try
                {
                    var m = Regex.Match(file, "File1=(?<url>.*?)\\n");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    Console.WriteLine($"Failed reading .pls:\n{file}");
                    return null;
                }
            }
            if (query.Contains(".m3u"))
            {
                try
                {
                    var m = Regex.Match(file, "(?<url>^[^#].*)", RegexOptions.Multiline);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    Console.WriteLine($"Failed reading .m3u:\n{file}");
                    return null;
                }

            }
            if (query.Contains(".asx"))
            {
                //<ref href="http://armitunes.com:8000"/>
                try
                {
                    var m = Regex.Match(file, "<ref href=\"(?<url>.*?)\"");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    Console.WriteLine($"Failed reading .asx:\n{file}");
                    return null;
                }
            }
            if (query.Contains(".xspf"))
            {
                /*
                <?xml version="1.0" encoding="UTF-8"?>
                    <playlist version="1" xmlns="http://xspf.org/ns/0/">
                        <trackList>
                            <track><location>file:///mp3s/song_1.mp3</location></track>
                */
                try
                {
                    var m = Regex.Match(file, "<location>(?<url>.*?)</location>");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    Console.WriteLine($"Failed reading .xspf:\n{file}");
                    return null;
                }
            }

            return query;
        }

        private static bool IsRadioLink(string query)
        {
            return (query.StartsWith("http") ||
            query.StartsWith("ww"))
            &&
            (query.Contains(".pls") ||
            query.Contains(".m3u") ||
            query.Contains(".asx") ||
            query.Contains(".xspf"));
        }

        #endregion
    }
}
