using Botcord.Core;
using Botcord.Core.Extensions;
using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Botcord.Audio
{
    public class SongInfo
    {
        public string Title { get; internal set; }
        public string Query { get; internal set; }
        public string Uri { get; internal set; }
        public TimeSpan Duration { get; internal set; }
        public string Provider { get; internal set; }
    }

    public class Song
    {
        public string Name
        {
            get { return m_info.Title; }
        }

        public string Source
        {
            get { return m_info.Query; }
        }

        public TimeSpan Duration
        {
            get { return m_info.Duration; }
        }

        public string DurationString
        {
            get { return m_info.Duration.ToString(); }
        }

        public Playlist Playlist
        {
            get;
            set;
        }

        public TimeSpan WaitInQueue
        {
            get { return m_playlist.GetSongQueueTime(this); }
        }

        public string WaitInQueueString
        {
            get { return WaitInQueue.ToString(); }
        }

        public int QueuePosition
        {
            get { return m_playlist.GetSongPosition(this); }
        }


        /*public string CurrentTime
        {
            get
            {
                var time = TimeSpan.FromSeconds(m_bytesSent / 3840 / 50);
                return $"{(int)time.TotalMinutes}:{time.Seconds}";
            }
        }

        public int CurrentSeconds
        {
            get
            {
                var time = TimeSpan.FromSeconds(m_bytesSent / 3840 / 50);
                return (int)time.TotalSeconds;
            }
        }*/

        public int SkipTo
        {
            get { return m_skipTo; }
            set { m_skipTo = value; }
        }

        public SongInfo Info
        {
            get { return m_info; }
        }        

        private SongInfo m_info = null;
        private Playlist m_playlist = null;
        private int m_skipTo = 0;
        private AudioOutStream m_outStream = null;

        public Song(SongInfo info)
        {
            m_info = info;
        }

        public void SetPlaylist(Playlist playlist)
        {
            m_playlist = playlist;
        }

        public async Task Play(IAudioClient voiceClient, CancellationToken cancelToken)
        {
            if (m_outStream == null)
            {
                m_outStream = voiceClient.CreateOpusStream(2000);
            }

            using (SongStream stream = new SongStream(m_outStream, m_info, cancelToken))
            {
                await stream.StartStream(m_skipTo, 1.0f);
                await stream.FlushAsync();
            }
        }

        public override string ToString()
        {
            return m_info.Title;
        }
    }
}
