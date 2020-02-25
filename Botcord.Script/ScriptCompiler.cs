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

        public bool TryCompile(string script, out AssemblyReference assembly)
        {
            return TryCompile(script, CompilerOptions.Default, out assembly);
        }

        public bool TryCompile(string script, CompilerOptions compilerOptions, out AssemblyReference assembly)
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
                AssemblyReference compiledAssembly = LoadAssembly(scriptName, compilation);
                assembly = compiledAssembly;
                if (compiledAssembly.IsLoaded)
                {
                    Logging.LogInfo(LogType.Bot, $"Compilation of script {script} successful.");
                    Logging.LogInfo(LogType.Bot, $"Attempting to load reference Assemblies.");
                    if (TryLoadingReferences(references))
                    {
                        Logging.LogInfo(LogType.Bot, $"Compilation of script {script} successful.");
                        m_cache.CacheAssembly(script, compiledAssembly);
                        return true;
                    }
                    else
                    {
                        Logging.LogError(LogType.Bot, $"Script {script} has a reference library that was not loaded. Unloading script assembly.");
                        compiledAssembly.Unload();
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to compiled script");
            }

            return false;
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

        private AssemblyReference LoadAssembly(string name, CSharpCompilation compilation)
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
                        CollectableAssemblyContext assemblyContext = new CollectableAssemblyContext();
                        AssemblyReference assembly = assemblyContext.LoadFromStream(codeStream);
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
        
        private bool TryLoadingReferences(IEnumerable<PortableExecutableReference> references)
        {
            try
            {
                foreach (var reference in references)
                {
                    string referencePath = Path.GetFullPath(reference.FilePath);
                    bool found = AppDomain.CurrentDomain.GetAssemblies().Any(a => !a.IsDynamic && a.Location == referencePath);
                    if (!found)
                    {
                        Logging.LogInfo(LogType.Bot, $"Loading Required Reference from {referencePath}.");
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(referencePath);
                    }
                }

                return true;
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to load references script will not be loaded");
                return false;
            }
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
