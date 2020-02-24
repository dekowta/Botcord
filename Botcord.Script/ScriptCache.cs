using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Botcord.Script
{
    public class ScriptCache
    {
        private Dictionary<string, Assembly> m_cachedAssemblies = new Dictionary<string, Assembly>();

        public bool IsCached(string script)
        {
            return m_cachedAssemblies.ContainsKey(script);
        }

        public bool CacheAssembly(string script, Assembly asm)
        {
            if (IsCached(script))
                return false;

            m_cachedAssemblies[script] = asm;

            return true;
        }

        public Assembly GetCachedAssembly(string script)
        {
            if (IsCached(script))
                return m_cachedAssemblies[script];

            return null;
        }
    }
}
