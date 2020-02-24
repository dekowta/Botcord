using Botcord.Core;
using Botcord.Core.DiscordExtensions;
using Botcord.Core.Extensions;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Botcord.Audio
{
    public static class SoundHelper
    {
        public static bool IsClipMP3(string file)
        {
            try
            {
                using (var MP3Reader = new Mp3FileReader(file))
                {
                    if(MP3Reader.Mp3WaveFormat.Encoding != WaveFormatEncoding.MpegLayer3)
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static TimeSpan GetClipTime(string file)
        {
            try
            {
                using (var MP3Reader = new Mp3FileReader(file))
                {
                    return MP3Reader.TotalTime;
                }
            }
            catch
            {
                return TimeSpan.MaxValue;
            }
        }
    }

    public class SoundPlayer : IDisposable
    {
        private SocketGuild m_guild = null;
        private SocketVoiceChannel m_connectedChannel = null;
        private IAudioClient m_audioClient = null;
        private AudioOutStream m_outStream = null;

        private bool m_destroyed = false;
        private object m_mutex = new object();
        private object m_playbackMutex = new object();
        private Task m_playbackThread;

        private CancellationTokenSource m_clipCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource m_taskCancellationSource = new CancellationTokenSource();
        private Queue<string> m_playbackQueue = new Queue<string>();
        private string m_current = string.Empty;

        private bool m_exitThread = false;

        public SoundPlayer(SocketGuild guild)
        {
            m_guild = guild;
        }

        #region Connection

        public bool IsConnected()
        {
            if (m_connectedChannel != null && (m_audioClient?.ConnectionState == ConnectionState.Connected || m_audioClient?.ConnectionState == ConnectionState.Connecting))
            {
                return true;
            }

            return false;
        }

        public SocketVoiceChannel ConnectedChannel()
        {
            return m_connectedChannel;
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
            if(channel == null)
            {
                Logging.LogError(LogType.Bot, $"Sent Channel was null something went wrong {m_guild.Name}.");
                return false;
            }

            if (m_playbackThread == null)
                m_playbackThread = Task.Factory.StartNew(PlaybackThread, m_taskCancellationSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (m_connectedChannel != null && m_audioClient?.ConnectionState == ConnectionState.Connected)
            {
                if (channel.Id == m_connectedChannel.Id)
                {
                    return true;
                }
                else
                {
                    Stop();
                }
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
            if (m_audioClient != null)
            {
                Utilities.ExecuteAndWait(async () => await m_audioClient.StopAsync());
            }

            if (m_audioClient == null || m_audioClient.ConnectionState == ConnectionState.Disconnected)
            {
                m_outStream     = null;
                m_audioClient   = null;
                return true;
            }

            Logging.LogError(LogType.Bot, $"Failed to disconnect. Bot may already be disconnected on server {m_guild.Name}.");
            return false;
        }

        #endregion

        public bool IsQueueEmpty()
        {
            return m_playbackQueue.Count == 0 && string.IsNullOrEmpty(m_current);
        }

        public bool IsInQueue(string file)
        {
            if (m_playbackQueue.Count == 0 && string.IsNullOrEmpty(m_current))
                return false;

            if (m_current == file)
                return true;

            return m_playbackQueue.FirstOrDefault(f => f == file) != null;
        }

        public bool EnqueueClip(string file, int count = 1)
        {
            if (count > 100)
                count = 100;

            if (File.Exists(file))
            {
                for (int i = 0; i < count; i++)
                {
                    lock (m_mutex)
                    {
                        m_playbackQueue.Enqueue(file);
                    }
                }

                return true;
            }
            else
            {
                Logging.LogError(LogType.Script, $"Cant Find file to play {file}.");
            }

            return false;
        }

        public void Stop()
        {
            m_clipCancellationSource.Cancel();
            m_clipCancellationSource = new CancellationTokenSource();

            lock (m_playbackMutex)
            {
                if (m_outStream != null)
                {
                    m_outStream.Clear();
                    m_outStream = null;
                }
            }

            lock (m_mutex)
            {
                m_playbackQueue.Clear();
            }
        }

        public void Dispose()
        {
            Stop();

            m_taskCancellationSource.Cancel();
            m_destroyed = true;
            bool success = Utilities.ExecuteAndWait(async (token) =>
            {
                while (!m_exitThread)
                {
                    await Task.Delay(500);
                    if (token.IsCancellationRequested)
                        break;
                }
            }, 30.Second());

            if (!success)
            {
                Logging.LogWarn(LogType.Bot, "Audio Playback thread failed to exit in requested timeout so will be aborted.");
                m_playbackThread.Wait(10.Second());
                m_exitThread = false;
            }
            else
            {
                Logging.LogInfo(LogType.Bot, "Audio Playback thread successfully aborted");
                m_exitThread = false;
            }

            m_playbackThread = null;
        }

        private async Task PlaybackThread()
        {
            Thread.CurrentThread.Name = $"Clip playback thread";

            while (!m_destroyed)
            {
                string currentSoundClip = "";
                try
                {
                    if (m_playbackQueue.Count == 0)
                    {
                        lock (m_playbackMutex)
                        {
                            if (m_outStream != null)
                                m_outStream.Flush();
                        }
                        m_current = string.Empty;
                        await Task.Delay(100).ConfigureAwait(false);
                        continue;
                    }

                    if (!IsConnected() && m_connectedChannel != null)
                        ReconnectToVoice(null);

                    lock (m_mutex)
                    {
                        currentSoundClip = m_playbackQueue.Dequeue();
                        m_current = currentSoundClip;
                    }

                    if (File.Exists(currentSoundClip))
                    {
                        await BufferClip(currentSoundClip);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Logging.LogException(LogType.Bot, ex, $"Failed to play clip {currentSoundClip} on Audio Play for server {m_guild.Name}.");
                    //try reconnecting in this exception case
                    DisconnectFromVoice();
                    ReconnectToVoice(null);
                }
                catch (Exception ex)
                {
                    Logging.LogException(LogType.Bot, ex, $"Failed to play clip {currentSoundClip} on Audio Play for server {m_guild.Name}.");
                }
                finally
                {
                    m_clipCancellationSource = new CancellationTokenSource();
                }
            }

            m_exitThread = true;
        }

        private Task BufferClip(string clip)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    var OutFormat = new WaveFormat(48000, 16, 2); // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
                    using (var MP3Reader = new Mp3FileReader(clip)) // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
                    using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat)) // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
                    {
                        resampler.ResamplerQuality = 60; // Set the quality of the resampler to 60, the highest quality
                        int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
                        byte[] buffer = new byte[blockSize];
                        int byteCount;

                        if (m_outStream == null)
                        {
                            m_outStream = m_audioClient.CreatePCMStream(AudioApplication.Mixed, bufferMillis: 10);
                        }

                        int writes = 0;
                        while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0) // Read audio into our buffer, and keep a loop open while data is present
                        {
                            if (m_clipCancellationSource.Token.IsCancellationRequested)
                            {
                                m_clipCancellationSource = new CancellationTokenSource();
                                return;
                            }

                            if (byteCount < blockSize)
                            {
                                // Incomplete Frame
                                for (int i = byteCount; i < blockSize; i++)
                                    buffer[i] = 0;
                            }

                            lock (m_playbackMutex)
                            {
                                if (m_outStream != null)
                                    m_outStream.Write(buffer, 0, blockSize); // Send the buffer to Discord
                            }

                            writes++;
                        }
                    }
                }
                catch
                {
                    //this is ok for the moment as currently it may assert if the channel changes
                    m_outStream = null;
                }
                //} while (dequeued);
            }, m_clipCancellationSource.Token);
        }
    }
}
