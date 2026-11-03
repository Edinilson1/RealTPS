using System.Reflection;

[assembly: AssemblyTitle("RealTPS runtime")]
[assembly: AssemblyProduct("RealTPS")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Edinilson. MIT License.")]
[assembly: AssemblyVersion("0.17.1.0")]
[assembly: AssemblyFileVersion("0.17.1.0")]
[assembly: AssemblyInformationalVersion("0.17.1")]

// Lets other plugins declare a dependency on RealTPS with KSPAssemblyDependency("RealTPSRuntime", 0, 17).
[assembly: KSPAssembly("RealTPSRuntime", 0, 17)]
