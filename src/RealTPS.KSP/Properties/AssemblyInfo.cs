using System.Reflection;

[assembly: AssemblyTitle("RealTPS runtime")]
[assembly: AssemblyProduct("RealTPS")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Edinilson. MIT License.")]
[assembly: AssemblyVersion("0.10.0.0")]
[assembly: AssemblyFileVersion("0.10.0.0")]
[assembly: AssemblyInformationalVersion("0.10.0")]

// Lets other plugins declare a dependency on RealTPS with KSPAssemblyDependency("RealTPSRuntime", 0, 10).
[assembly: KSPAssembly("RealTPSRuntime", 0, 10)]
