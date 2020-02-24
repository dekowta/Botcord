using System;
using Botcord.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Botcord.Script
{
    public class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext() : base(isCollectible: true)
        { }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }


    //https://keestalkstech.com/2016/05/how-to-add-dynamic-compilation-to-your-projects/
    public class ScriptCompiler
    {
        private ScriptCache m_cache = new ScriptCache();

        public ScriptCompiler()
        {
        }

        public bool IsCompiled(string script)
        {
            return m_cache.IsCached(script);
        }

        public bool TryCompile(string script, out Assembly assembly)
        {
            return TryCompile(script, CompilerOptions.Default, out assembly);
        }

        public bool TryCompile(string script, CompilerOptions compilerOptions, out Assembly assembly)
        {
            assembly = null;

            try
            {
                Logging.Log(LogType.Bot, LogLevel.Info, $"Attempting to compile script {script}");

                if (m_cache.IsCached(script))
                {
                    Logging.Log(LogType.Bot, LogLevel.Info, $"Found cached script fetching assembly");
                    assembly = m_cache.GetCachedAssembly(script);
                    return true;
                }

                if (!File.Exists(script))
                {
                    Logging.Log(LogType.Bot, LogLevel.Error, $"Failed to find script {script} aborting compilation");
                    return false;
                }

                string scriptName = Path.GetFileNameWithoutExtension(script);

                bool success = false;
                IEnumerable<PortableExecutableReference> references = null;
                success = Utilities.TryCatch(() => references = compilerOptions.Assemblies.Select(asm => FindAndCreateReference(asm, compilerOptions)), "Failed to create meta data from assembly");
                if (!success) return false;

                SyntaxTree[] syntax = null;
                Utilities.TryCatch(() => syntax = new SyntaxTree[] { ParseSyntax(script) }, $"Failed to parse syntax of script {script}");
                if (!success) return false;

                CSharpCompilation compilation = CSharpCompilation.Create(scriptName, syntax, references, compilerOptions.Options);
                Assembly compiledAssembly = LoadAssembly(scriptName, compilation);
                assembly = compiledAssembly;
                if (compiledAssembly != null)
                {
                    Logging.LogInfo(LogType.Bot, $"Compilation of script {script} successful.");
                    m_cache.CacheAssembly(script, compiledAssembly);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to compiled script");
                return false;
            }
        }


        private SyntaxTree ParseSyntax(string file)
        {
            //string code = File.ReadAllText(file);
            using (var stream = File.OpenRead(file))
            {
                SourceText text = SourceText.From(stream, Encoding.UTF8);
                SyntaxTree syntax = CSharpSyntaxTree.ParseText(text, null, file);
                var root = (CompilationUnitSyntax)syntax.GetRoot();
                return syntax;
            }
        }

        private Assembly LoadAssembly(string name, CSharpCompilation compilation)
        {
            using (var codeStream = new MemoryStream())
            {
#if DEBUG
                using (var symbolStream = new MemoryStream())
                {
                    

                    var compilationResult = compilation.Emit(codeStream, symbolStream);

                    string pdbFile = Path.Combine(Utilities.AssemblyPath, $"{name}.pdb");
                    if (File.Exists(pdbFile)) File.Delete(pdbFile);
                    File.WriteAllBytes(pdbFile, symbolStream.ToArray());
#else
                {
                    var compilationResult = compilation.Emit(codeStream);
#endif
                    if (compilationResult.Success)
                    {
                        codeStream.Seek(0, SeekOrigin.Begin);
                        Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(codeStream);
                        return assembly;
                    }
                    else
                    {
                        Logging.LogError(LogType.Bot, "Failed to compile script");
                        foreach (var error in compilationResult.Diagnostics)
                        {
                            Logging.LogError(LogType.Bot, "{0}", error.ToString());
                        }
                    }
                }
            }

            return null;
        }
        
        private PortableExecutableReference FindAndCreateReference(string asm, CompilerOptions options)
        {
            if(File.Exists(asm))
            {
                Logging.LogInfo(LogType.Bot, $"Found reference {asm}");
                return MetadataReference.CreateFromFile(asm);
            }
            else
            {
                foreach(var referencePath in options.ReferencePaths)
                {
                    string asmFile = Path.Combine(referencePath, asm);
                    if(File.Exists(asmFile))
                    {
                        Logging.LogInfo(LogType.Bot, $"Found reference {asm}");
                        return MetadataReference.CreateFromFile(asmFile);
                    }
                }
            }

            Logging.LogError(LogType.Bot, $"Missing reference {asm}");
            return null;
        }
    }
}
