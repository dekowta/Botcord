using Botcord.Core;
using Discord.Audio;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using Botcord.Core.Extensions;
using Discord;

namespace Botcord.Audio
{
    public class Playlist
    {
        public static string DefaultPlaylist
        {
            get { return "Default"; }
        }

        public string GuildName
        {
            get { return m_guild.Name; }
        }

        public string Name
        {
            get
            {
                return m_name;
            }
        }

        public string PlaylistFile
        {
            get { return m_playlistFile; }
        }

        public bool Paused
        {
            get;
            set;
        }

        public bool Repeat
        {
            get { return m_repeat; }
            set { m_repeat = value; UpdatePlaylist(); }
        }

        public int Songs
        {
            get { return m_playlistCollection.Count; }
        }

        public int ActiveSongs
        {
            get { return m_activePlaylist.Count; } 
        }
        
        public bool ShuffleOnLoad
        {
            get { return m_suffleOnLoad; }
            set
            {
                m_suffleOnLoad = value;
                UpdatePlaylist();
            }
        }

        public float Volume
        {
            get { return m_volume; }
            set
            {
                m_volume = Utilities.Clamp(0.0f, 2.0f, value);
                SongStream.Volume = m_volume;
                UpdatePlaylist();
            }
        }

        public bool IsValid
        {
            get { return m_isValid; }
        }

        public IReadOnlyCollection<Song> Queue
        {
            get { return m_activePlaylist; }
        }

        public string LastSong { get { return Next != null ? Next.Name : "No song currently playing"; } }
        public Song Last { get { return m_lastSong; } }
        public string CurrentSong { get { return Next != null ? Next.Name : "No song currently playing"; } }
        public Song Current { get { return m_currentSong; } }
        public string NextSong { get { return Next != null ? Next.Name : "No song currently playing"; } }
        public Song Next
        {
            get
            {
                lock (m_mutex)
                {
                    if (m_activePlaylist.Count >= 1)
                        return m_activePlaylist.First();
                    else
                        return null;
                }
            }
        }

        public Song LastAdded
        {
            get
            {
                if(m_activePlaylist.Count >= 1 )
                {
                    return m_activePlaylist.Last.Value;
                }
                else if(m_currentSong != null)
                {
                    return m_currentSong;
                }
                return null;
            }
        }

        public TimeSpan QueueTime
        {
            get { return m_currentQueueTime; }
        }

        private SocketGuild m_guild = null;

        private string m_name           = string.Empty;
        private string m_playlistFile   = string.Empty;

        private bool m_repeat = true;
        private bool m_suffleOnLoad = false;
        private float m_volume = 0.5f;
        
        private bool m_saveable = true;
        private bool m_isValid = false;

        private object m_mutex = new object();
        private CancellationTokenSource m_songCancellationSource = new CancellationTokenSource();

        private List<Song> m_playlistCollection = new List<Song>();
        private LinkedList<Song> m_activePlaylist = new LinkedList<Song>();

        private Song m_currentSong;
        private Song m_lastSong;

        private TimeSpan m_currentQueueTime = TimeSpan.Zero;

        private const string c_playlistFolder = "Playlists";

        public Playlist(SocketGuild guild)
        {
            m_guild = guild;
            m_saveable = false;
            m_repeat = false;
            m_name = DefaultPlaylist;
        }

        public Playlist(string Name, SocketGuild guild)
        {
            m_guild = guild;
            m_name = Name;
            LoadPlaylist(m_name);
        }

        public async Task Play(IAudioClient client)
        {
            if (Paused)
            {
                return;
            }

            if (client != null && client.ConnectionState == ConnectionState.Connected && m_activePlaylist.Count != 0)
            {
                lock (m_mutex)
                {
                    m_currentSong = m_activePlaylist.First.Value;
                    m_activePlaylist.RemoveFirst();
                    if(m_lastSong != null)
                        m_currentQueueTime = m_currentQueueTime.Subtract(m_lastSong.Duration);

                    Logging.LogInfo(LogType.Bot, $"Changing song to {m_currentSong.Name} on server {GuildName}");
                }

                if (!await Utilities.CheckUriAsync(m_currentSong.Info.Uri))
                {
                    Logging.LogInfo(LogType.Bot, $"Changing song to {m_currentSong.Name} has an dead link re-resolving song on server {GuildName}");
                    m_currentSong = (await MediaPlayer.ResolveSong(m_currentSong.Info.Query)).FirstOrDefault();
                    if (m_currentSong != null)
                    {
                        m_currentSong.SetPlaylist(this);
                        UpdateSong(m_currentSong);
                    }
                    else
                    {
                        Logging.LogInfo(LogType.Bot, $"Song {m_currentSong.Name} failed to re-resolving dead link on server {GuildName}");
                        RemoveSong(m_currentSong.Name);
                        m_currentSong = m_lastSong;
                        return;
                    }
                }

                try
                {
                    if (HasSong(m_currentSong.Info))
                    {
                        await m_currentSong.Play(client, m_songCancellationSource.Token);
                        RepeatSong();

                        m_lastSong = m_currentSong;
                        m_currentSong = null;
                    }
                }
                catch (Exception ex)
                {
                    Logging.LogException(LogType.Bot, ex, $"Exception occured in playlist {Name} for server {GuildName}.");
                }
                finally
                {
                    if (!m_songCancellationSource.Token.IsCancellationRequested)
                    {
                        m_songCancellationSource.Cancel();
                    }

                    m_songCancellationSource = new CancellationTokenSource();
                }
            }
            else if(m_activePlaylist.Count == 0 && m_currentSong == null && m_currentQueueTime != new TimeSpan(0))
            {
                m_currentQueueTime = new TimeSpan(0);
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        public int AddSong(Song newSong, Location location)
        {
            if (string.IsNullOrEmpty(newSong.Info.Uri))
                return -1;

            if (!HasSong(newSong.Info))
            {
                lock (m_mutex)
                {
                    newSong.SetPlaylist(this);
                    m_playlistCollection.Add(newSong);
                    if (location == Location.Last)
                        m_activePlaylist.AddLast(newSong);
                    else
                        m_activePlaylist.AddFirst(newSong);
                }

                m_currentQueueTime = m_currentQueueTime.Add(newSong.Duration);

                UpdatePlaylist();

                Logging.LogInfo(LogType.Bot, $"New Song {newSong.Name} added to playlist {Name} on server {GuildName}.");

                return m_activePlaylist.Count;
            }
            else
            {

                lock (m_mutex)
                {
                    newSong.SetPlaylist(this);
                    m_playlistCollection.Add(newSong);
                    if (location == Location.Last)
                        m_activePlaylist.AddLast(newSong);
                    else
                        m_activePlaylist.AddFirst(newSong);
                }

                m_currentQueueTime = m_currentQueueTime.Add(newSong.Duration);

                Logging.LogInfo(LogType.Bot, $"New Song {newSong.Name} added to playlist {Name} on server {GuildName}.");

                return m_activePlaylist.Count;
            }

            //Logging.LogInfo(LogType.Bot, $"Failed to add Song {newSong.Name} added to playlist {Name} on server {GuildName} as it already exists.");
            //return -1;
        }

        public bool SetSong(string songName, Location location)
        {
            if (HasSong(songName))
            {
                lock (m_mutex)
                {
                    Song foundSong = m_activePlaylist.FirstOrDefault(s => s.Name == songName);
                    m_activePlaylist.Remove(foundSong);
                    if (location == Location.Last)
                        m_activePlaylist.AddLast(foundSong);
                    else
                        m_activePlaylist.AddFirst(foundSong);

                    m_currentQueueTime = m_currentQueueTime.Add(foundSong.Duration);

                    Logging.LogInfo(LogType.Bot, $"Set song {foundSong.Name} to play next on server {GuildName}");
                }

                return true;
            }

            return false;
        }

        public bool Skip()
        {
            Logging.LogInfo(LogType.Bot, $"Skipping song {m_currentSong.Name} on server {GuildName}");
            m_songCancellationSource.Cancel();
            m_songCancellationSource = new CancellationTokenSource();
            return true;
        }

        public bool RemoveSong(string songName)
        {
            if (HasSong(songName))
            {
                Song foundSong = m_playlistCollection.FirstOrDefault(s => s.Name == songName);
                m_playlistCollection.Remove(foundSong);
                lock(m_mutex)
                {
                    foundSong = m_activePlaylist.FirstOrDefault(s => s.Name == songName);
                    m_activePlaylist.Remove(foundSong);

                    m_currentQueueTime = m_currentQueueTime.Subtract(foundSong.Duration);
                }
                Logging.LogInfo(LogType.Bot, $"Removed song {foundSong.Name} from server {GuildName}.");
                return true;
            }

            return false;
        }

        public bool Stop()
        {
            Logging.LogInfo(LogType.Bot, $"Stopping playlist {Name} on server {m_guild}");
            m_songCancellationSource.Cancel();
            m_songCancellationSource = new CancellationTokenSource();
            Paused = true;
            if (!m_saveable)
            {
                lock (m_mutex)
                {
                    m_activePlaylist.Clear();
                }
                m_playlistCollection.Clear();
            }

            return true;
        }

        public bool ClearPlaying()
        {
            lock (m_mutex)
            {
                Logging.LogInfo(LogType.Bot, $"Clearing the active playlist {Name} on server {GuildName}");
                m_activePlaylist.Clear();

                m_currentQueueTime = TimeSpan.Zero;
            }

            return true;
        }

        public bool Clear()
        {
            lock (m_mutex)
            {
                Logging.LogInfo(LogType.Bot, $"Clearing entire playlist {Name} on server {GuildName}");
                m_playlistCollection.Clear();
            }

            m_currentQueueTime = TimeSpan.Zero;

            UpdatePlaylist();

            return true;
        }

        public void RequeueAll()
        {
            ClearPlaying();

            lock (m_mutex)
            {
                Skip();
                foreach (var song in m_playlistCollection)
                {
                    m_activePlaylist.AddLast(song);

                    m_currentQueueTime.Add(song.Duration);
                }

                if (m_suffleOnLoad)
                    Shuffle();
            }

            Logging.LogInfo(LogType.Bot, $"Requeueing playlist {Name} on server {GuildName}.");
        }

        public void Shuffle()
        {
            lock (m_mutex)
            {
                Logging.LogInfo(LogType.Bot, $"Shuffling playlist {Name} on server {GuildName}.");
                m_activePlaylist.Shuffle();
            }
        }

        public bool HasSong(SongInfo songinfo)
        {
            lock (m_mutex)
            {
                return m_playlistCollection.FirstOrDefault(s => s.Info.Title == songinfo.Title) != null;
            }
        }

        public bool HasSong(string name)
        {
            lock (m_mutex)
            {
                return m_playlistCollection.FirstOrDefault(s => s.Info.Title == name) != null;
            }
        }

        public Song GetSong(string name)
        {
            lock (m_mutex)
            {
                return m_playlistCollection.FirstOrDefault(s => s.Info.Title == name);
            }
        }

        public string GetQueueInfo()
        {
            StringBuilder sb = new StringBuilder(EmbedBuilder.MaxEmbedLength);
            if (m_currentSong != null)
                sb.AppendLine($"Playing: {m_currentSong.Name} - {m_currentSong.Source}");
            if (m_lastSong != null)
                sb.AppendLine($"Last Song: {m_lastSong.Name} - {m_lastSong.Source}");
            if (m_activePlaylist.Count >= 1)
                sb.AppendLine($"Next Song: {m_activePlaylist.First.Value.Name} - {m_activePlaylist.First.Value.Source}");
            sb.AppendLine();

            int id = 1;
            foreach (var song in m_activePlaylist)
            {
                sb.AppendLine($"Song {id++}: {song.Name} - {song.Source}");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public TimeSpan GetSongQueueTime(Song song)
        {
            TimeSpan time = TimeSpan.Zero;
            if(m_currentSong != null)
            {
                time += m_currentSong.Duration;
            }
            
            foreach(var queuedSong in m_activePlaylist)
            {
                if (queuedSong == song) break;
                time += queuedSong.Duration;
            }

            return time;
        }

        public int GetSongPosition(Song song)
        {
            if (song == m_currentSong)
                return 0;
            else
                return m_activePlaylist.IndexOf(song) + 1;
        }

        public bool IsPlaylistEmpty()
        {
            return m_activePlaylist.Count == 0 && m_currentSong == null;
        }

        private void RepeatSong()
        {
            if (!Repeat)
                return;

            lock (m_mutex)
            {
                if (m_currentSong != null && HasSong(m_currentSong.Info))
                {
                    m_activePlaylist.AddLast(m_currentSong);
                }
            }
        }

        private void UpdateSong(Song song)
        {
            lock (m_mutex)
            {
                int index = m_playlistCollection.FindIndex(s => s.Name == song.Name);
                if (index != -1)
                {
                    m_playlistCollection[index] = song;
                }
            }

            UpdatePlaylist();
        }

        #region XML Management

        private void LoadPlaylist(string name)
        {
            if (m_guild == null || string.IsNullOrWhiteSpace(name))
            {
                Logging.LogError(LogType.Bot, $"Server or playlist name is empty ignoring playlist.");
                m_isValid = false;
                return;
            }

            string fileLocation = Path.Combine(Utilities.DataFolder, m_guild.Id.ToString(), c_playlistFolder, $"{name}.xml");
            if (!File.Exists(fileLocation))
            {
                CreateNew(name, fileLocation);
                return;
            }

            XDocument doc = XDocument.Load(fileLocation);
            XElement root = doc.Root;
            XElement info = root.Element("Info");
            string xmlName = string.Empty;
            if (info.TryGetAttribute("Name", out xmlName))
            {
                if (xmlName != name)
                {
                    Logging.LogError(LogType.Bot, $"Xml document {fileLocation} has info Name {xmlName} but Name {name} was used for loading playlist in server {m_guild.Name}.");
                    m_isValid = false;
                    return;
                }

                m_name = xmlName;

                bool repeat = false;
                if (info.TryGetAttribute("Repeat", out repeat))
                {
                    m_repeat = repeat;
                }
                bool shuffle = false;
                if (info.TryGetAttribute("Shuffle", out shuffle))
                {
                    m_suffleOnLoad = shuffle;
                }
                float volume = 1.0f;
                if (info.TryGetAttribute("Volume", out volume))
                {
                    m_volume = Utilities.Clamp(0.0f, 1.0f, volume);
                    SongStream.Volume = m_volume;
                }

                XElement songs = root.Element("Songs");
                if (songs.HasElements)
                {
                    foreach (var song in songs.Elements("Song"))
                    {
                        SongInfo songInfo = GetSongInfoFromXml(song);
                        if (!string.IsNullOrEmpty(songInfo.Title))
                        {
                            Song addSong = new Song(songInfo);
                            addSong.SetPlaylist(this);
                            m_playlistCollection.Add(addSong);
                            m_activePlaylist.AddLast(addSong);
                        }
                    }
                }

                if (m_suffleOnLoad)
                {
                    Shuffle();
                }

                m_playlistFile = fileLocation;

                UpdatePlaylist();
                m_isValid = true;
                return;
            }
        }

        private void CreateNew(string name, string fileLocation)
        {
            XDocument doc = new XDocument();
            XElement root = new XElement("Playlist");
            XElement info = new XElement("Info");
            XElement songs = new XElement("Songs");
            info.Add(new XAttribute("Name", name));
            info.Add(new XAttribute("Repeat", m_repeat));
            info.Add(new XAttribute("Shuffle", m_suffleOnLoad));
            root.Add(info);
            root.Add(songs);
            doc.Add(root);
            using (var stream = new FileStream(fileLocation, FileMode.CreateNew))
            {
                doc.Save(stream);
            }

            m_name = name;

            m_playlistFile = fileLocation;
        }

        private void UpdatePlaylist()
        {
            if (m_saveable == false)
                return;

            string fileLocation = Path.Combine(Utilities.DataFolder, m_guild.Id.ToString(), c_playlistFolder, $"{Name}.xml");
            if (!File.Exists(fileLocation))
            {
                CreateNew(Name, fileLocation);
            }

            XDocument doc = XDocument.Load(fileLocation);
            XElement root = doc.Root;
            XElement info = root.Element("Info");
            if (info.Attribute("Repeat") != null)
                info.Attribute("Repeat").Value = Repeat.ToString();
            else
                info.Add(new XAttribute("Repeat", Repeat));

            if (info.Attribute("Shuffle") != null)
                info.Attribute("Shuffle").Value = ShuffleOnLoad.ToString();
            else
                info.Add(new XAttribute("Shuffle", ShuffleOnLoad));

            if (info.Attribute("Volume") != null)
                info.Attribute("Volume").Value = Volume.ToString();
            else
                info.Add(new XAttribute("Volume", Volume));

            string xmlName = string.Empty;
            if (info.TryGetAttribute("Name", out xmlName))
            {
                if (xmlName != Name)
                {
                    Logging.LogError(LogType.Bot, $"Xml document {fileLocation} has info Name {xmlName} but Name {Name} was used for loading" +
                        " playlist in server {m_owner.Name} cant update playlist.");
                    m_isValid = false;
                    return;
                }

                XElement songs = root.Element("Songs");
                songs.RemoveAll();
                lock (m_playlistCollection)
                {
                    foreach (var song in m_playlistCollection)
                    {
                        XElement songElement = SetSongInfoToXml(song.Info);
                        songs.Add(songElement);
                    }
                }

                using (var stream = new FileStream(fileLocation, FileMode.CreateNew))
                {
                    doc.Save(stream);
                }
            }
        }

        private SongInfo GetSongInfoFromXml(XElement element)
        {
            SongInfo info;
            string title = string.Empty;
            if (!element.TryGetAttribute("Title", out title))
            {
                info = new SongInfo()
                {
                    Title = string.Empty
                };
                return info;
            }

            string uri = string.Empty;
            if (!element.TryGetAttribute("Uri", out uri))
            {
                info = new SongInfo()
                {
                    Title = string.Empty
                };
                return info;
            }

            string query = string.Empty;
            if (!element.TryGetAttribute("Query", out query))
            {
                info = new SongInfo()
                {
                    Title = string.Empty
                };
                return info;
            }

            string provider = string.Empty;
            if (!element.TryGetAttribute("Provider", out provider))
            {
                info = new SongInfo()
                {
                    Title = string.Empty
                };
                return info;
            }


            info = new SongInfo()
            {
                Title = title,
                Uri = uri,
                Query = query,
                Provider = provider,
            };

            return info;
        }

        private XElement SetSongInfoToXml(SongInfo song)
        {
            if (string.IsNullOrEmpty(song.Title))
                return null;

            XElement newElement = new XElement("Song");
            XAttribute title = new XAttribute("Title", song.Title);
            XAttribute uri = new XAttribute("Uri", song.Uri);
            XAttribute query = new XAttribute("Query", song.Query);
            XAttribute provider = new XAttribute("Provider", song.Provider);

            newElement.Add(title);
            newElement.Add(uri);
            newElement.Add(query);
            newElement.Add(provider);

            return newElement;
        }

        #endregion
    }
}
