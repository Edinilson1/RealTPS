using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Generation;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;

namespace RtpsTool
{
    /// <summary>
    /// Developer tool. Commands:
    ///   validate  - parse and validate the material catalogue, the host definitions, the tech tree files, the
    ///               difficulty presets, the localization and docs/sources/MATERIAL_SOURCES.md
    ///   generate  - write the generated ModuleManager files (use --check to only compare)
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "validate";
            string root = FindRoot();
            if (root == null)
            {
                Console.Error.WriteLine("Cannot find the repository root (GameData/RealTPS).");
                return 2;
            }

            Repository repo = Repository.Load(root);
            foreach (ValidationIssue issue in repo.Issues)
            {
                Console.WriteLine(issue);
            }
            bool failed = repo.Issues.Any(i => i.IsError);

            switch (command)
            {
                case "validate":
                    Console.WriteLine(repo.Catalogue.Materials.Count + " material profiles, " + repo.Hosts.Count + " hosts, " +
                        repo.Issues.Count(i => i.IsError) + " errors, " + repo.Issues.Count(i => !i.IsError) + " warnings.");
                    return failed ? 1 : 0;

                case "generate":
                    if (failed)
                    {
                        Console.Error.WriteLine("Not generating: fix the errors above first.");
                        return 1;
                    }
                    return Generate(repo, args.Contains("--check"));

                default:
                    Console.Error.WriteLine("Unknown command '" + command + "'. Use validate, generate, simulate or maps.");
                    return 2;
            }
        }

        private static int Generate(Repository repo, bool checkOnly)
        {
            var files = new Dictionary<string, string>
            {
                { GeneratedFiles.Resources, PatchGenerator.Resources(repo.Catalogue) },
                { GeneratedFiles.Unlocks, PatchGenerator.Upgrades(repo.Catalogue, repo.Hosts) },
                { GeneratedFiles.StockHosts, PatchGenerator.Hosts(repo.Catalogue, repo.Hosts) }
            };
            int stale = 0;
            foreach (KeyValuePair<string, string> file in files)
            {
                string path = Path.Combine(repo.Root, file.Key);
                string existing = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
                if (existing == file.Value)
                {
                    Console.WriteLine("up to date  " + file.Key);
                    continue;
                }
                stale++;
                if (checkOnly)
                {
                    Console.WriteLine("STALE       " + file.Key);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, file.Value);
                    Console.WriteLine("written     " + file.Key);
                }
            }
            return checkOnly && stale > 0 ? 1 : 0;
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "GameData", "RealTPS")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }

    /// <summary>Catalogue and hosts loaded from the working tree.</summary>
    internal sealed class Repository
    {
        public string Root;
        public MaterialCatalogue Catalogue;
        public List<HostDefinition> Hosts = new List<HostDefinition>();
        public List<ValidationIssue> Issues = new List<ValidationIssue>();

        public static Repository Load(string root)
        {
            var repo = new Repository { Root = root };

            var materialNodes = new List<CfgNode>();
            string materialDir = Path.Combine(root, "GameData", "RealTPS", "Materials");
            foreach (string file in Directory.GetFiles(materialDir, "*.cfg").OrderBy(f => f, StringComparer.Ordinal))
            {
                materialNodes.AddRange(CfgParser.Parse(File.ReadAllText(file)).GetNodes(MaterialParser.NodeName));
            }
            repo.Catalogue = MaterialCatalogue.Load(materialNodes);
            repo.Issues.AddRange(repo.Catalogue.Issues);

            string hostDir = Path.Combine(root, "data", "hosts");
            foreach (string file in Directory.GetFiles(hostDir, "*.cfg").OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (CfgNode node in CfgParser.Parse(File.ReadAllText(file)).GetNodes(HostDefinition.NodeName))
                {
                    HostDefinition host = HostDefinition.Parse(node, repo.Issues);
                    if (host != null)
                    {
                        Compatibility.ValidateHost(host, repo.Catalogue, repo.Issues);
                        repo.Hosts.Add(host);
                    }
                }
            }
            var parts = new HashSet<string>(StringComparer.Ordinal);
            foreach (HostDefinition host in repo.Hosts)
            {
                if (!parts.Add(host.PartName))
                {
                    repo.Issues.Add(new ValidationIssue(true, "host '" + host.PartName + "'", "is enrolled twice"));
                }
            }
            return repo;
        }
    }
}
