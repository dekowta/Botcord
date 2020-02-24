using Botcord.Audio.DiscordWrapper;
using Botcord.Core;
using Botcord.Core.Extensions;
using Discord.Audio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Botcord.Audio
{
    public class SongStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set => throw new NotImplementedException(); }

        private static float m_volume = 0.5f;
        public static float Volume
        {
            get { return m_volume; }
            set { m_volume = value; }
        }

        public const int SampleRate = 48000;

        private SongInfo m_songInfo = null;
        private Process m_ffmpeg = null;
        private CancellationToken m_cancellationToken;

        private readonly AudioStream m_stream;
        private readonly OpusEncoder m_encoder;
        private readonly byte[] m_buffer;
        private int m_partialFramePos;
        private ushort m_seq;
        private uint m_timestamp;



        public SongStream(AudioStream stream, SongInfo info, CancellationToken token)
        {
            m_cancellationToken = token;

            m_songInfo  = info;

            m_stream    = stream;
            m_encoder   = new OpusEncoder(128.KiB(), AudioApplication.Music, 30);
            m_buffer    = new byte[OpusConverter.FrameBytes];

        }

        #region Stream Unused items

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        #endregion

        public override async Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                buffer = AdjustVolume(buffer, m_volume);

                //Assume threadsafe
                while (count > 0)
                {
                    if (m_partialFramePos == 0 && count >= OpusConverter.FrameBytes)
                    {
                        //We have enough data and no partial frames. Pass the buffer directly to the encoder
                        int encFrameSize = m_encoder.EncodeFrame(buffer, offset, m_buffer, 0);
                        m_stream.WriteHeader(m_seq, m_timestamp, false);
                        await m_stream.WriteAsync(m_buffer, 0, encFrameSize, cancellationToken).ConfigureAwait(false);

                        offset += OpusConverter.FrameBytes;
                        count -= OpusConverter.FrameBytes;
                        m_seq++;
                        m_timestamp += OpusConverter.FrameSamplesPerChannel;
                    }
                    else if (m_partialFramePos + count >= OpusConverter.FrameBytes)
                    {
                        //We have enough data to complete a previous partial frame.
                        int partialSize = OpusConverter.FrameBytes - m_partialFramePos;
                        Buffer.BlockCopy(buffer, offset, m_buffer, m_partialFramePos, partialSize);
                        int encFrameSize = m_encoder.EncodeFrame(m_buffer, 0, m_buffer, 0);
                        m_stream.WriteHeader(m_seq, m_timestamp, false);
                        await m_stream.WriteAsync(m_buffer, 0, encFrameSize, cancellationToken).ConfigureAwait(false);

                        offset += partialSize;
                        count -= partialSize;
                        m_partialFramePos = 0;
                        m_seq++;
                        m_timestamp += OpusConverter.FrameSamplesPerChannel;
                    }
                    else
                    {
                        //Not enough data to build a complete frame, store this part for later
                        Buffer.BlockCopy(buffer, offset, m_buffer, m_partialFramePos, count);
                        m_partialFramePos += count;
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task StartStream(int startAt, float volume)
        {
                //-loglevel quiet
            m_ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                Arguments = $"-ss {startAt} -i \"{m_songInfo.Uri}\" -f s16le -ar 48000 -af \"volume = {volume}\" -ac 2 pipe:1 ",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = false,
            });

            var stream = m_ffmpeg.StandardOutput.BaseStream;
            await stream.CopyToAsync(this, 81920, m_cancellationToken);
        }

        public override async Task FlushAsync(CancellationToken cancelToken)
        {
            await m_stream.FlushAsync(cancelToken).ConfigureAwait(false);
        }
        //public override async Task ClearAsync(CancellationToken cancelToken)
        //{
        //    await m_stream.ClearAsync(cancelToken).ConfigureAwait(false);
        //}

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                m_encoder.Dispose();
        }

        private unsafe static byte[] AdjustVolume(byte[] audioSamples, float volume)
        {
            Contract.Requires(audioSamples != null);
            Contract.Requires(audioSamples.Length % 2 == 0);
            Contract.Requires(volume >= 0f && volume <= 1f);
            Contract.Assert(BitConverter.IsLittleEndian);

            if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;

            // 16-bit precision for the multiplication
            int volumeFixed = (int)Math.Round(volume * 65536d);

            int count = audioSamples.Length / 2;

            fixed (byte* srcBytes = audioSamples)
            {
                short* src = (short*)srcBytes;

                for (int i = count; i != 0; i--, src++)
                    *src = (short)(((*src) * volumeFixed) >> 16);
            }

            return audioSamples;
        }
    }
}
