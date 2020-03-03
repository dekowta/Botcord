using Botcord.Core;
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

        public bool AreInstancesAlive
        {
            get { return createdInstances.Any(instance => instance.IsAlive); }
        }

        public Assembly Assembly
        {
            get;
            private set;
        }

        private List<WeakReference> createdInstances;
        private AssemblyLoadContext loadContext;

        public AssemblyReference(AssemblyLoadContext context) : base(context, true)
        {
            if(context.Assemblies.Count() != 1)
            {
                throw new ArgumentException("Only one assembly allowed in the context reference");
            }
            Assembly = context.Assemblies.First();
            loadContext = context;
            createdInstances = new List<WeakReference>();
        }

        public IEnumerable<T> CreateInstances<T>()
        {
            List<T> instances = new List<T>();
            Type[] asmTypes = Assembly.GetTypes();
            IEnumerable<Type> types = asmTypes.Where(t => IsOfType<T>(t));
            foreach (var type in types)
            {
                try
                {
                    T instance = (T)Activator.CreateInstance(type);
                    if (instance != null)
                    {
                        WeakReference instanceRef = new WeakReference(instance);
                        createdInstances.Add(instanceRef);
                        instances.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    Logging.LogException(LogType.Script, ex, $"Failed to create instance of type {type.Name} or cast to type {typeof(T).Name}.");
                }
            }

            return instances;
        }

        public void Unload()
        {
            if(AreInstancesAlive)
            {
                throw new InvalidOperationException("One or more instance are still active");
            }

            Assembly = null;
            loadContext.Unload();
            loadContext = null;
        }

        private bool IsOfType<T>(Type type)
        {
            Type parentType = typeof(T);
            return parentType.GetTypeInfo().IsAssignableFrom(type);
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
