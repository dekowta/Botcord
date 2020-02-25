using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Botcord.Script
{
    public class ScriptCache
    {
        private Dictionary<string, AssemblyReference> m_cachedAssemblies = new Dictionary<string, AssemblyReference>();

        public bool IsCached(string script)
        {
            return m_cachedAssemblies.ContainsKey(script);
        }

        public bool CacheAssembly(string script, AssemblyReference asm)
        {
            if (IsCached(script))
                return false;

            m_cachedAssemblies[script] = asm;

            return true;
        }

        public AssemblyReference GetCachedAssembly(string script)
        {
            if (IsCached(script))
                return m_cachedAssemblies[script];

            return null;
        }
    }
}
