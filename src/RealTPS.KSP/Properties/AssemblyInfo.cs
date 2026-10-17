using System.Reflection;

[assembly: AssemblyTitle("RealTPS runtime")]
[assembly: AssemblyProduct("RealTPS")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Edinilson. MIT License.")]
[assembly: AssemblyVersion("0.15.1.0")]
[assembly: AssemblyFileVersion("0.15.1.0")]
[assembly: AssemblyInformationalVersion("0.15.1")]

// Lets other plugins declare a dependency on RealTPS with KSPAssemblyDependency("RealTPSRuntime", 0, 15).
[assembly: KSPAssembly("RealTPSRuntime", 0, 15)]
