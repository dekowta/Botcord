using Botcord.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;



namespace Botcord.Script
{
    public class CompilerOptions
    {

        public static CompilerOptions Default { get; } = new CompilerOptions();

        public IEnumerable<string> Assemblies
        {
            get { return m_assemblies; }
        }

        public IEnumerable<string> ReferencePaths
        {
            get { return m_assemblyFolders; }
        }

        public CSharpCompilationOptions Options
        {
            get { return m_options; }
        }

        private List<string> m_assemblies = new List<string>();
        private List<string> m_assemblyFolders = new List<string>();

        private CSharpCompilationOptions m_options = null;

        public CompilerOptions()
        {
#if DEBUG
            m_options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, true, optimizationLevel: OptimizationLevel.Debug, generalDiagnosticOption: ReportDiagnostic.Error);
#else
            m_options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, true, optimizationLevel: OptimizationLevel.Release, generalDiagnosticOption: ReportDiagnostic.Error);
#endif
            Assembly asm = typeof(object).GetTypeInfo().Assembly;
            var coreDir = Path.GetDirectoryName(asm.Location);

            AddReference(Path.Combine(coreDir, "mscorlib.dll"));
            AddReference(typeof(object).GetTypeInfo().Assembly.Location);
            AddReference(Path.Combine(coreDir, "netstandard.dll"));
            AddReference(Path.Combine(coreDir, "System.Linq.dll"));
            AddReference(Path.Combine(coreDir, "System.Runtime.dll"));
            AddReference(Path.Combine(coreDir, "System.Collections.dll"));
            AddReference(Path.Combine(coreDir, "System.Threading.Tasks.dll"));

            AddReferenceSearchFolder(coreDir);
            AddReferenceSearchFolder(Utilities.AssemblyPath);
        }

        public void AddReferenceSearchFolder(string path)
        {
            path = path.Trim();
            if(!m_assemblyFolders.Contains(path))
            {
                m_assemblyFolders.Add(path);
            }
        }

        public void AddReference(string assemblyLocation)
        {
            if (string.IsNullOrWhiteSpace(assemblyLocation))
                return;

            assemblyLocation = assemblyLocation.Trim();
            if(!assemblyLocation.EndsWith(".dll"))
            {
                assemblyLocation = assemblyLocation + ".dll";
            }

            if (!m_assemblies.Contains(assemblyLocation))
            {
                m_assemblies.Add(assemblyLocation);
            }
        }

        public void AddReferences(IEnumerable<string> assemblyLocations)
        {
            foreach (string reference in assemblyLocations)
            {
                AddReference(reference);
            }
        }

        public bool HasReference(string assemblyLocation)
        {
            return m_assemblies.Contains(assemblyLocation);
        }
    }
}
