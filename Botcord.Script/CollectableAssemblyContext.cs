using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Botcord.Script
{
    public class AssemblyReference : WeakReference
    {
        public bool IsLoaded
        {
            get { return Assembly != null; }
        }

        public Assembly Assembly
        {
            get;
            private set;
        }

        private AssemblyLoadContext loadContext;

        public AssemblyReference(AssemblyLoadContext context) : base(context, true)
        {
            if(context.Assemblies.Count() != 1)
            {
                throw new ArgumentException("Only one assembly allowed in the context reference");
            }
            Assembly = context.Assemblies.First();
            loadContext = context;
        }

        public void Unload()
        {
            Assembly = null;
            loadContext.Unload();
            loadContext = null;
        }
    }

    public class CollectableAssemblyContext : AssemblyLoadContext
    {
        public CollectableAssemblyContext() 
            : base(isCollectible: true)
        {
        }

        public new AssemblyReference LoadFromStream(Stream stream)
        {
            base.LoadFromStream(stream);
            return new AssemblyReference(this);
        }
    }
}
