using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Xml;
using Botcord.Core.Extensions;
using System.Web;
using System.IO.Compression;

namespace Botcord.Core
{
    public static class Utilities
    {
        public static string AssemblyPath
        {
            get
            {
                Assembly thisAsm = typeof(Utilities).GetTypeInfo().Assembly;
                return Path.GetDirectoryName(thisAsm.Location);
            }
        }

        public static string AssemblyLocation<T>()
        {
            try
            {
                return typeof(T).GetTypeInfo().Assembly.Location;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string TempFolder
        {
            get { return EnsurePath(Path.Combine(Utilities.AssemblyPath, "Temp")); }
        }

        public static string DataFolder
        {
            get { return EnsurePath(Path.Combine(Utilities.AssemblyPath, "Data")); }
        }

        public static string LogFolder
        {
            get { return EnsurePath(Path.Combine(Utilities.AssemblyPath, "Logs")); }
        }

        public static string DataShortFolder
        {
            get { return "Data"; }
        }

        #region Random
        private static int randQueueMax = 20;
        private static Queue<int> m_lastRandoms = new Queue<int>(randQueueMax);
        public static int Rand(int min, int max)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            int val = rand.Next(min, max);
            if (randQueueMax < Math.Abs(max - min))
            {
                int escape = 0;
                while (m_lastRandoms.Contains(val) && ++escape != 20)
                {
                    val = rand.Next(min, max);
                }
            }

            if (m_lastRandoms.Count >= randQueueMax)
            {
                m_lastRandoms.Dequeue();
            }
            m_lastRandoms.Enqueue(val);
            return val;
        }

        public static float Clamp(float min, float max, float value)
        {
            if (value > max) return max;
            if (value < min) return min;
            return value;
        }

        public static int Clamp(int min, int max, int value)
        {
            if (value > max) return max;
            if (value < min) return min;
            return value;
        }
        #endregion

        public static string TempFilePath(string fileType)
        {
            string fileName = Path.ChangeExtension(Path.GetRandomFileName(), fileType);
            if(!Directory.Exists(TempFolder))
            {
                Directory.CreateDirectory(TempFolder);
            }

            return Path.Combine(TempFolder, fileName);
        }

        public static string EnsurePath(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
            }
            catch
            {
                return Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        #region Async Helpers

        /// <summary>
        /// Execute's an async Task<T> method which has a void return value synchronously
        /// </summary>
        /// <param name="task">Task<T> method to execute</param>
        public static void ExecuteAndWait(Func<Task> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            synch.Post(async _ =>
            {
                try
                {
                    await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();

            SynchronizationContext.SetSynchronizationContext(oldContext);
        }

        /// <summary>
        /// Execute's an async Task<T> method which has a T return type synchronously
        /// </summary>
        /// <typeparam name="T">Return Type</typeparam>
        /// <param name="task">Task<T> method to execute</param>
        /// <returns></returns>
        public static T ExecuteAndWait<T>(Func<Task<T>> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            T ret = default(T);
            synch.Post(async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            return ret;
        }

        public static void Execute(Func<Task> asyncAction)
        {
            asyncAction();
        }
        public static void Execute<T>(Func<T> asyncAction)
        {
            asyncAction();
        }

        public static void Execute(Func<CancellationToken, Task> asyncAction, int timeout)
        {
            Execute(async () =>
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                var task = asyncAction(tokenSource.Token);
                if (await Task.WhenAny(task, Task.Delay(timeout, tokenSource.Token)) == task)
                {
                    await task;
                }
                else
                {
                    tokenSource.Cancel();
                }
            });
        }

        public static bool ExecuteAndWait(Func<CancellationToken, Task> asyncAction, int millisecondsTimeout)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            var task = asyncAction(tokenSource.Token);
            if (Task.WhenAny(task, Task.Delay(millisecondsTimeout, tokenSource.Token)).GetAwaiter().GetResult() == task)
            {
                task.ConfigureAwait(false).GetAwaiter().GetResult();
                return true;
            }
            else
            {
                tokenSource.Cancel();
                return false;
            }
        }

        #endregion

        #region Web Helpers

        [DebuggerDisplay("{items[0].id.playlistId}")]
        public class YoutubePlaylistSearch
        {
            public YtPlaylistItem[] items { get; set; }
        }
        public class YtPlaylistItem
        {
            public YtPlaylistId id { get; set; }
        }
        public class YtPlaylistId
        {
            public string kind { get; set; }
            public string playlistId { get; set; }
        }
        [DebuggerDisplay("{items[0].id.videoId}")]
        public class YoutubeVideoSearch
        {
            public YtVideoItem[] items { get; set; }
        }
        public class YtVideoItem
        {
            public YtVideoId id { get; set; }
        }
        public class YtVideoId
        {
            public string kind { get; set; }
            public string videoId { get; set; }
        }
        public class PlaylistItemsSearch
        {
            public string nextPageToken { get; set; }
            public PlaylistItem[] items { get; set; }
        }
        public class PlaylistItem
        {
            public YtVideoId contentDetails { get; set; }
        }

        public enum RequestHttpMethod
        {
            Get,
            Post
        }

        public static string GoogleApiKey = "AIzaSyDnQDgweHmwsDu5WL0V9vojWu4vHCyyvcY";

        private static HttpClient _httpClient = new HttpClient();
        public static async Task TryDownloadFileAsync(string url, IProgress<double> progress, CancellationToken token)
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(string.Format("The request returned with HTTP status code {0}", response.StatusCode));
            }

            var total = response.Content.Headers.ContentLength.HasValue ? response.Content.Headers.ContentLength.Value : -1L;
            var canReportProgress = total != -1 && progress != null;

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var totalRead = 0L;
                var buffer = new byte[4096];
                var isMoreToRead = true;

                do
                {
                    token.ThrowIfCancellationRequested();

                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        var data = new byte[read];
                        buffer.ToList().CopyTo(0, data, 0, read);

                        // TODO: put here the code to write the file to disk

                        totalRead += read;

                        if (canReportProgress)
                        {
                            progress.Report((totalRead * 1d) / (total * 1d) * 100);
                        }
                    }
                } while (isMoreToRead);


            }
        }

        public static async Task<bool> TryDownloadFileAsync(string url, string file, int maxFileSize, CancellationToken token)
        {
            if(maxFileSize > 50.MiB())
            {
                return false;
            }

            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(string.Format("The request returned with HTTP status code {0}", response.StatusCode));
            }

            var total = response.Content.Headers.ContentLength.HasValue ? response.Content.Headers.ContentLength.Value : -1L;
            if(total != -1 && total > maxFileSize)
            {
                return false;
            }

            bool failed = false;
            using (var outputStream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var totalRead = 0L;
                var buffer = new byte[4096];
                var isMoreToRead = true;

                do
                {
                    token.ThrowIfCancellationRequested();

                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        if(read + totalRead > maxFileSize)
                        {
                            failed = true;
                            break;
                        }

                        outputStream.Write(buffer, 0, read);

                        totalRead += read;
                    }
                } while (isMoreToRead);
            }

            if(failed)
            {
                File.Delete(file);
                return false;
            }
            else
            {
                return true;
            }
        }

        public static async Task<string> GetResponseStringAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers = null,
            RequestHttpMethod method = RequestHttpMethod.Get)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url));
            var cl = new HttpClient();
            cl.DefaultRequestHeaders.Clear();
            switch (method)
            {
                case RequestHttpMethod.Get:
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            cl.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    return await cl.GetStringAsync(url).ConfigureAwait(false);
                case RequestHttpMethod.Post:
                    FormUrlEncodedContent formContent = null;
                    if (headers != null)
                    {
                        formContent = new FormUrlEncodedContent(headers);
                    }
                    var message = await cl.PostAsync(url, formContent).ConfigureAwait(false);
                    return await message.Content.ReadAsStringAsync().ConfigureAwait(false);
                default:
                    throw new NotImplementedException("That type of request is unsupported.");
            }
        }

        public static async Task<IEnumerable<string>> FindYoutubeUrlByKeywords(string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords), "Query not specified.");
            if (keywords.Length > 150)
                throw new ArgumentException("Query is too long.");

            var match = new Regex(@"(?:list=)(?<id>[\da-zA-Z\-_]*)").Match(keywords);
            if (match.Success)
            {
                List<string> youtubeVideos = new List<string>();
                var videos = await GetVideoIDs(match.Groups["id"].Value, 200);
                foreach (string id in videos)
                {
                    youtubeVideos.Add($"https://www.youtube.com/watch?v={id}");
                }

                return youtubeVideos;
            }

            //maybe it is already a youtube url, in which case we will just extract the id and prepend it with youtube.com?v=
            match = new Regex(@"(?:youtu\\.be\\/|v=)(?<id>[\da-zA-Z\-_]*)").Match(keywords);
            if (match.Success)
            {
                return new List<string> { $"https://www.youtube.com/watch?v={match.Groups["id"].Value}" };
            }

            if (string.IsNullOrWhiteSpace(GoogleApiKey))
                throw new Exception("Google API Key is missing.");

            var response = await GetResponseStringAsync(
                                    $"https://www.googleapis.com/youtube/v3/search?" +
                                    $"part=snippet&maxResults=1" +
                                    $"&q={Uri.EscapeDataString(keywords)}" +
                                    $"&key={GoogleApiKey}").ConfigureAwait(false);
            JObject obj = JObject.Parse(response);

            var data = JsonConvert.DeserializeObject<YoutubeVideoSearch>(response);

            if (data.items.Length > 0)
            {
                var toReturn = "http://www.youtube.com/watch?v=" + data.items[0].id.videoId.ToString();
                return new List<string> { toReturn };
            }
            else
                return null;
        }

        public static async Task<IEnumerable<string>> GetVideoIDs(string playlist, int number = 50)
        {
            if (string.IsNullOrWhiteSpace(GoogleApiKey))
            {
                throw new ArgumentNullException(nameof(playlist));
            }
            if (number < 1)
                throw new ArgumentOutOfRangeException();

            string nextPageToken = null;

            List<string> toReturn = new List<string>();

            do
            {
                var toGet = number > 50 ? 50 : number;
                number -= toGet;
                var link =
                    $"https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails" +
                    $"&maxResults={toGet}" +
                    $"&playlistId={playlist}" +
                    $"&key={GoogleApiKey}";
                if (!string.IsNullOrWhiteSpace(nextPageToken))
                    link += $"&pageToken={nextPageToken}";
                var response = await GetResponseStringAsync(link).ConfigureAwait(false);
                var data = await Task.Run(() => JsonConvert.DeserializeObject<PlaylistItemsSearch>(response)).ConfigureAwait(false);
                nextPageToken = data.nextPageToken;
                toReturn.AddRange(data.items.Select(i => i.contentDetails.videoId));
            } while (number > 0 && !string.IsNullOrWhiteSpace(nextPageToken));

            return toReturn;
        }

        public static async Task<TimeSpan> GetVideoDuration(string videoUrl)
        {
            var match = new Regex(@"(?:youtu\\.be\\/|v=)(?<id>[\da-zA-Z\-_]*)").Match(videoUrl);
            if (match.Success)
            {
                var response = await GetResponseStringAsync(
                                      $"https://www.googleapis.com/youtube/v3/videos?" +
                                      $"part=contentDetails" +
                                      $"&id={match.Groups["id"].Value}" +
                                      $"&key={GoogleApiKey}").ConfigureAwait(false);

                dynamic d = JObject.Parse(response);
                if (d.items != null)
                {
                    string duration = d.items[0].contentDetails.duration;
                    TimeSpan youTubeDuration = XmlConvert.ToTimeSpan(duration);
                    return youTubeDuration;
                }
            }

            return TimeSpan.Zero;
        }

        public static string GetYoutubeThumbnailUrl(string videoUrl)
        {
            var match = new Regex(@"(?:youtu\\.be\\/|v=)(?<id>[\da-zA-Z\-_]*)").Match(videoUrl);
            if (match.Success)
            {
                return $"https://img.youtube.com/vi/{match.Groups["id"].Value}/0.jpg";
            }

            return string.Empty;
        }

        public static async Task<bool> CheckUriAsync(string Uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Uri);
            try
            {
                HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    response.Dispose();
                    return true;
                }
                else
                {
                    response.Dispose();
                    return false;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }

        public static string SanitiseString(string stringToSanatise)
        {
            return HttpUtility.HtmlDecode(stringToSanatise);
        }
        #endregion

        #region String Helpers

        public static List<string> Split(string text, char delimiter)
        {
            return text.Split(delimiter).ToList();
        }

        public static List<string> SplitAndWrapString(string text, string split = " ", int wrapLimit = 1950)
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

        #endregion

        #region Try Catch Helper

        public static bool TryCatch(Action func, string message)
        {
            try
            {
                func();
                return true;
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, message);
                return false;
            }
        }

        #endregion
    }


    public class ExclusiveSynchronizationContext : SynchronizationContext
    {
        private bool done;
        public Exception InnerException { get; set; }
        readonly AutoResetEvent workItemsWaiting = new AutoResetEvent(false);
        readonly Queue<Tuple<SendOrPostCallback, object>> items = new Queue<Tuple<SendOrPostCallback, object>>();

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException("We cannot send to our same thread");
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            lock (items)
            {
                items.Enqueue(Tuple.Create(d, state));
            }
            workItemsWaiting.Set();
        }

        public void EndMessageLoop()
        {
            Post(_ => done = true, null);
        }

        public void BeginMessageLoop()
        {
            while (!done)
            {
                Tuple<SendOrPostCallback, object> task = null;
                lock (items)
                {
                    if (items.Count > 0)
                    {
                        task = items.Dequeue();
                    }
                }
                if (task != null)
                {
                    task.Item1(task.Item2);
                    if (InnerException != null) // the method threw an exeption
                    {
                        throw new AggregateException("AsyncHelpers.Run method threw an exception.", InnerException);
                    }
                }
                else
                {
                    workItemsWaiting.WaitOne();
                }
            }
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }
    }
}
