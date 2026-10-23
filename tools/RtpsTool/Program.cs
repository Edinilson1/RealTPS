using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Generation;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;
using RealTPS.Core.Simulation;

namespace RtpsTool
{
    /// <summary>
    /// Developer tool. Commands:
    ///   validate  - parse and validate the material catalogue, the host definitions, the tech tree files, the
    ///               difficulty presets, the localization and docs/sources/MATERIAL_SOURCES.md
    ///   generate  - write the generated ModuleManager files (use --check to only compare)
    ///   simulate  - run the calibration mission matrix and print a Markdown table (--tilt N: face tilted N degrees;
    ///               --preset NAME: the settings of a shipped difficulty preset, cell variation included)
    ///   maps      - render the shield map of fixture states to PNG (--out DIR, default artifacts/maps)
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
            repo.ReportedIssues = repo.Issues.Count;
            if (command == "validate")
            {
                Provenance.Check(repo);
                foreach (ValidationIssue issue in repo.Issues.Skip(repo.ReportedIssues))
                {
                    Console.WriteLine(issue);
                }
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

                case "simulate":
                    if (failed)
                    {
                        return 1;
                    }
                {
                    ShieldSettings settings = null;
                    MissionMatrix.Print(repo, Console.Out, TiltArgument(args), settings);
                    return 0;
                }

                case "maps":
                {
                    if (failed)
                    {
                        return 1;
                    }
                    int index = Array.IndexOf(args, "--out");
                    string outDir = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(root, "artifacts", "maps");
                    return MapRender.Run(repo, outDir);
                }

                default:
                    Console.Error.WriteLine("Unknown command '" + command + "'. Use validate, generate, simulate or maps.");
                    return 2;
            }
        }

        /// <summary><c>--tilt N</c>: constant angle of the shield face to the direction of travel, degrees.</summary>
        private static double TiltArgument(string[] args)
        {
            int index = Array.IndexOf(args, "--tilt");
            double tilt;
            return index >= 0 && index + 1 < args.Length && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out tilt) ? tilt : 0.0;
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
        public int ReportedIssues;

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

    /// <summary>Calibration mission matrix. A model of the game, never reported as a flight test.</summary>
    internal static class MissionMatrix
    {
        private sealed class Vehicle
        {
            public string Name;
            public string Host;
            public double Mass;        // kg without shield consumable
            public double DragArea;    // m^2
            public double RadiativeArea;
            public double ShieldDryMass;
            public double ThermalMassModifier = 1.0;
            public double InternalMaxTemp = 3300.0;
            public double StockAblator;
        }

        private sealed class Entry
        {
            public string Name;
            public double Speed;
            public double Angle;
        }

        public static void Print(Repository repo, TextWriter o, double tiltDeg = 0.0, ShieldSettings settings = null)
        {
            EntryBody kerbin = EntryBody.Kerbin();
            var vehicles = new[]
            {
                // Radiative area = drag-cube total minus the face covered by the payload.
                new Vehicle { Name = "1.25 m capsule (1.0 t)", Host = "HeatShield1", Mass = 1000, DragArea = 1.3, RadiativeArea = 1.99, ShieldDryMass = 100, StockAblator = 200 },
                new Vehicle { Name = "2.5 m capsule (6 t)", Host = "HeatShield2", Mass = 6000, DragArea = 5.0, RadiativeArea = 7.8, ShieldDryMass = 500, StockAblator = 800 },
                new Vehicle { Name = "0.625 m probe (0.25 t)", Host = "HeatShield0", Mass = 250, DragArea = 0.4, RadiativeArea = 0.62, ShieldDryMass = 25, StockAblator = 50 },
                new Vehicle { Name = "KV-1 pod with service stack (1.6 t)", Host = "kv1Pod", Mass = 1600, DragArea = 2.45, RadiativeArea = 9.0, ShieldDryMass = 1150, StockAblator = 20 },
                new Vehicle { Name = "light probe on a 2.5 m shield (1.0 t)", Host = "HeatShield2", Mass = 1000, DragArea = 5.0, RadiativeArea = 7.8, ShieldDryMass = 500, StockAblator = 800 },
                new Vehicle { Name = "light payload on a 3.75 m shield (2.5 t)", Host = "HeatShield3", Mass = 2500, DragArea = 11.0, RadiativeArea = 17.0, ShieldDryMass = 1000, StockAblator = 1800 },
                new Vehicle { Name = "10 m inflatable (12 t)", Host = "InflatableHeatShield", Mass = 12000, DragArea = 85.0, RadiativeArea = 229.0, ShieldDryMass = 1500, ThermalMassModifier = 7.5, InternalMaxTemp = 3250 },
                new Vehicle { Name = "10 m inflatable (4 t)", Host = "InflatableHeatShield", Mass = 4000, DragArea = 85.0, RadiativeArea = 229.0, ShieldDryMass = 1500, ThermalMassModifier = 7.5, InternalMaxTemp = 3250 }
            };
            var entries = new[]
            {
                // Flight-path angles at the 70 km interface follow from the periapsis of the approach orbit.
                new Entry { Name = "Suborbital 1.5", Speed = 1500, Angle = 20.0 },
                new Entry { Name = "LKO shallow", Speed = 2150, Angle = 1.5 },
                new Entry { Name = "LKO steep", Speed = 2150, Angle = 5.0 },
                new Entry { Name = "Mun return Pe35", Speed = 3150, Angle = 12.5 },
                new Entry { Name = "Mun return Pe15", Speed = 3150, Angle = 16.0 },
                new Entry { Name = "Interplanetary 4.2", Speed = 4200, Angle = 16.5 },
                new Entry { Name = "High energy 5.5", Speed = 5500, Angle = 18.0 },
                new Entry { Name = "Extreme 7.0", Speed = 7000, Angle = 19.0 }
            };

            o.WriteLine("Calibration model output (numerical model of the stock thermal formulas; NOT in-game results).");
            o.WriteLine("flux = peak convective W/cm2 over the protected area; load = MJ/m2; used = kg consumed of kg loaded.");
            o.WriteLine("Cells: ok/LOST/FAIL, peak skin K, inventory used, b = peak bond K, x = face exposed, g = lowest cell integrity, i = peak internal K.");
            o.WriteLine("SOAK@Ns = the craft came through the entry and the shield failed N s later, while the heat soaked back to the bond.");
            o.WriteLine(string.Format(CultureInfo.InvariantCulture, "Face tilted {0:0.#} degrees from the direction of travel; each entry is followed by a 600 s soak.", tiltDeg));
            foreach (Vehicle vehicle in vehicles)
            {
                HostDefinition host = repo.Hosts.FirstOrDefault(h => h.PartName == vehicle.Host);
                if (host == null)
                {
                    continue;
                }
                o.WriteLine();
                o.WriteLine("### " + vehicle.Name + " on " + host.PartName);
                o.WriteLine();
                var names = new List<string>();
                if (vehicle.StockAblator > 0)
                {
                    names.Add("stock generic (reference)");
                }
                names.AddRange(host.Materials);
                var cells = new Dictionary<string, List<string>>();
                foreach (string n in names)
                {
                    cells[n] = new List<string>();
                }
                var environment = new List<string>();
                foreach (Entry entry in entries)
                {
                    var c = new EntryCase
                    {
                        Name = entry.Name,
                        Body = kerbin,
                        EntrySpeed = entry.Speed,
                        EntryAngleDeg = entry.Angle,
                        VehicleMass = vehicle.Mass,
                        DragArea = vehicle.DragArea,
                        ShieldArea = host.Area,
                        RadiativeArea = vehicle.RadiativeArea,
                        ShieldDryMass = vehicle.ShieldDryMass,
                        ThermalMassModifier = vehicle.ThermalMassModifier,
                        SubstrateMaxTemp = host.SubstrateMaxTemp,
                        InternalMaxTemp = vehicle.InternalMaxTemp,
                        Layout = host.Layout(),
                        ThicknessFactor = host.ThicknessFactor,
                        BackConductance = host.BackConductance,
                        SubstrateHeatCapacity = host.SubstrateHeatCapacity,
                        StructureLimitTemp = host.StructureLimitTemp,
                        PartRole = host.PartRole,
                        TiltDeg = tiltDeg
                    };
                    if (settings != null)
                    {
                        c.Settings = settings;
                    }
                    EntryOutcome reference = null;
                    if (vehicle.StockAblator > 0)
                    {
                        reference = EntrySimulator.RunStock(c, new EntrySimulator.StockAblator { Capacity = vehicle.StockAblator });
                        cells["stock generic (reference)"].Add(Cell(reference));
                    }
                    foreach (string id in host.Materials)
                    {
                        MaterialDefinition m;
                        if (repo.Catalogue.TryGet(id, out m))
                        {
                            EntryOutcome r = EntrySimulator.Run(c, m);
                            cells[id].Add(Cell(r));
                            if (reference == null)
                            {
                                reference = r;
                            }
                        }
                    }
                    environment.Add(string.Format(CultureInfo.InvariantCulture, "{0:0} W/cm2, {1:0} MJ/m2, {2:0} kPa, {3:0.0} g",
                        reference.PeakConvectiveFlux / 1.0e4, reference.HeatLoad / 1.0e6, reference.PeakDynamicPressure / 1000.0, reference.PeakDeceleration / 9.81));
                }
                o.WriteLine("| Material | " + string.Join(" | ", entries.Select(e => e.Name)) + " |");
                o.WriteLine("|---|" + string.Join("", entries.Select(e => "---|")));
                o.WriteLine("| *environment (first row's case)* | " + string.Join(" | ", environment) + " |");
                foreach (string n in names)
                {
                    o.WriteLine("| " + n + " | " + string.Join(" | ", cells[n]) + " |");
                }
            }
        }

        /// <summary>
        /// "ok 1704K 38% b480 i350" = survived, peak skin temperature, share of the inventory consumed, peak bond
        /// temperature, peak internal temperature. "x12%" = share of the face exposed at the end; "g0.4" = lowest
        /// cell integrity; "LOST" = protection lost but the part survived.
        /// </summary>
        private static string Cell(EntryOutcome r)
        {
            // Summing the cells can leave a rounding residue of about 1e-16 kg; it is not material.
            string used = r.InitialMass > 0.0 ? string.Format(CultureInfo.InvariantCulture, " {0:0}%", Math.Max(0.0, 100.0 * r.ConsumedMass / r.InitialMass)) : string.Empty;
            string temps = string.Format(CultureInfo.InvariantCulture, "{0:0}K", r.PeakSkinTemp);
            string shield = r.PeakBondTemp > 0.0 ? string.Format(CultureInfo.InvariantCulture, " b{0:0}", r.PeakBondTemp) : string.Empty;
            if (r.ExposedArea > 0.0005)
            {
                shield += string.Format(CultureInfo.InvariantCulture, " x{0:0}%", 100.0 * r.ExposedArea);
            }
            if (r.MinIntegrity < 0.995)
            {
                shield += string.Format(CultureInfo.InvariantCulture, " g{0:0.00}", r.MinIntegrity);
            }
            if (!r.Survived)
            {
                string why = r.FailureReason.StartsWith("burn") ? "burnthrough"
                    : r.FailureReason.StartsWith("structure") ? "structure"
                    : r.FailureReason.StartsWith("payload") ? "payload" : "overtemp";
                return string.Format(CultureInfo.InvariantCulture, "FAIL {0} @{1:0}s", why, r.Duration) + used + shield;
            }
            string prefix = r.FailureReason.Length > 0 ? "skip "
                : r.FailedInSoak ? string.Format(CultureInfo.InvariantCulture, "SOAK@{0:0}s ", r.FailureTime - r.EntryTime)
                : (r.Failure != RealTPS.Core.Shield.ShieldFailure.None ? "LOST " : "ok ");
            return prefix + temps + used + shield + string.Format(CultureInfo.InvariantCulture, " i{0:0}", r.PeakInternalTemp);
        }
    }
}
