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

            ScanReferences(scriptFile, options);

            AssemblyReference assemblyObject;
            m_compiler.TryCompile(scriptFile, options, out assemblyObject);
            if (assemblyObject != null)
            {
                scriptObjects = assemblyObject.CreateInstances<T>();

                if (scriptObjects.Count() == 0) Logging.LogWarn(LogType.Bot, $"Script {scriptFile} failed to contain any instances of type {typeof(T).Name}.");
                else Logging.LogInfo(LogType.Bot, $"Found {scriptObjects.Count()} instance of type {typeof(T).Name} in script {scriptFile}");

                return true;
            }

            return false;
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
