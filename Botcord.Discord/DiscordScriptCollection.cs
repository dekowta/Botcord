using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Botcord.Discord
{
    public class DiscordScriptCollection : ICollection<DiscordScriptHost>, IDisposable
    {
        private SocketGuild m_attachedGuild;
        private List<DiscordScriptHost> m_scriptCollection;

        public SocketGuild Guild => m_attachedGuild;

        public int Count => m_scriptCollection.Count;

        public bool IsReadOnly => false;

        public DiscordScriptCollection()
        {
            m_attachedGuild     = null;
            m_scriptCollection  = new List<DiscordScriptHost>();
        }

        public DiscordScriptCollection(SocketGuild guild)
        {
            m_attachedGuild     = guild;
            m_scriptCollection  = new List<DiscordScriptHost>();
            
        }

        public void Add(DiscordScriptHost item)
        {
            if(!Contains(item))
            {
                item.AttachGuild(m_attachedGuild);
                m_scriptCollection.Add(item);
            }
        }

        public void Clear()
        {
            foreach(var script in m_scriptCollection)
            {
                script.Dispose();
            }

            m_scriptCollection.Clear();
        }

        public bool Contains(DiscordScriptHost item)
        {
            return m_scriptCollection.Contains(item);
        }

        public void CopyTo(DiscordScriptHost[] array, int arrayIndex)
        {
            IEnumerable<DiscordScriptHost> arrayRange = array.Skip(arrayIndex);
            m_scriptCollection.AddRange(arrayRange);
        }

        public IEnumerator<DiscordScriptHost> GetEnumerator()
        {
            return m_scriptCollection.GetEnumerator();
        }

        public bool Remove(DiscordScriptHost item)
        {
            if(Contains(item))
            {
                m_scriptCollection.Remove(item);
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_scriptCollection.GetEnumerator();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    for(int i = 0; i < m_scriptCollection.Count; i++)
                    {
                        DiscordScriptHost host = m_scriptCollection[i];
                        host.Dispose();
                        host = null;
                    }

                    m_scriptCollection.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DiscordScriptCollection() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
