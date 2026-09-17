using System.Reflection;

[assembly: AssemblyTitle("RealTPS runtime")]
[assembly: AssemblyProduct("RealTPS")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Edinilson. MIT License.")]
[assembly: AssemblyVersion("0.13.0.0")]
[assembly: AssemblyFileVersion("0.13.0.0")]
[assembly: AssemblyInformationalVersion("0.13.0")]

// Lets other plugins declare a dependency on RealTPS with KSPAssemblyDependency("RealTPSRuntime", 0, 13).
[assembly: KSPAssembly("RealTPSRuntime", 0, 13)]
