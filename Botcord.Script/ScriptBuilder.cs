using Botcord.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.IO;

namespace Botcord.Script
{
    public class ScriptBuilder : Singleton<ScriptBuilder>
    {
        ScriptCompiler m_compiler = new ScriptCompiler();

        public bool IsScriptBuilt(string scriptFile)
        {
            return m_compiler.IsCompiled(scriptFile);
        }

        public bool TryBuildScript<T>(string scriptFile, out IEnumerable<T> scriptObjects)
        {
            return TryBuildScript<T>(scriptFile, CompilerOptions.Default, out scriptObjects);
        }

        public bool TryBuildScript<T>(string scriptFile, CompilerOptions options, out IEnumerable<T> scriptObjects)
        {
            scriptObjects = null;
            AssemblyReference assemblyObject = null;

            ScanReferences(scriptFile, options);

            m_compiler.TryCompile(scriptFile, options, out assemblyObject);
            if (assemblyObject != null)
            {
                scriptObjects = CreateInstances<T>(assemblyObject);

                if (scriptObjects.Count() == 0) Logging.LogWarn(LogType.Bot, $"Script {scriptFile} failed to contain any instances of type {typeof(T).Name}.");
                else Logging.LogInfo(LogType.Bot, $"Found {scriptObjects.Count()} instance of type {typeof(T).Name} in script {scriptFile}");

                return true;
            }

            return false;
        }

        private IEnumerable<T> CreateInstances<T>(AssemblyReference asm)
        {
            List<T> instances = new List<T>();
            Type[] asmTypes = asm.Assembly.GetTypes();
            IEnumerable<Type> types = asmTypes.Where(t => IsOfType<T>(t));
            foreach(var type in types)
            {
                try
                {
                    T instance = (T)Activator.CreateInstance(type);
                    instances.Add(instance);
                }
                catch(Exception ex)
                {
                    Logging.LogException(LogType.Bot, ex, $"Failed to create instance of type {type.Name} or cast to type {typeof(T).Name}.");
                }
            }

            return instances;
        }

        private bool IsOfType<T>(Type type)
        {
            Type parentType = typeof(T);
            return parentType.GetTypeInfo().IsAssignableFrom(type);
        }

        private void ScanReferences(string script, CompilerOptions options)
        {
            List<string> additionalAssemblies = new List<string>();

            foreach (string line in File.ReadLines(script))
            {
                if (line.StartsWith("//ASM:"))
                {
                    string assembliesString = line.Replace("//ASM:", "");
                    string[] assemblies = assembliesString.Split(';');
                    additionalAssemblies.AddRange(assemblies);
                }
            }

            options.AddReferences(additionalAssemblies);
        }
    }
}
