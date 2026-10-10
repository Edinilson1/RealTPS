using System;
using System.Collections.Generic;
using System.Text;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using KSP.Localization;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// One-time check at the main menu: dependencies present, catalogue valid, every enrolled host consistent.
    /// Problems are logged and shown once; RealTPS never repairs a configuration silently.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class RtpsStartup : MonoBehaviour
    {
        public void Start()
        {
            var problems = new List<string>();
            try
            {
                Check(problems);
            }
            catch (Exception e)
            {
                RtpsLog.Error("startup check failed: " + e);
                return;
            }

            if (problems.Count == 0)
            {
                return;
            }

            var text = new StringBuilder();
            int shown = Math.Min(problems.Count, 8);
            for (int i = 0; i < shown; i++)
            {
                text.Append("- ").Append(problems[i]).Append('\n');
            }
            if (problems.Count > shown)
            {
                text.Append(Localizer.Format("#LOC_RTPS_startup_more", (problems.Count - shown).ToString())).Append('\n');
            }
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "RtpsStartupProblems",
                Localizer.Format("#LOC_RTPS_startup_title"), text.ToString(), Localizer.Format("#LOC_RTPS_startup_ok"), false, HighLogic.UISkin);
        }

        private static void Check(List<string> problems)
        {
            if (!AssemblyLoaded("B9PartSwitch"))
            {
                string message = Localizer.Format("#LOC_RTPS_startup_no_b9");
                problems.Add(message);
                RtpsLog.Error(message);
            }
            if (!AssemblyLoaded("ModuleManager"))
            {
                string message = Localizer.Format("#LOC_RTPS_startup_no_mm");
                problems.Add(message);
                RtpsLog.Error(message);
            }

            MaterialCatalogue catalogue = RtpsCatalogue.Current;
            int errors = 0;
            for (int i = 0; i < catalogue.Issues.Count; i++)
            {
                if (catalogue.Issues[i].IsError)
                {
                    errors++;
                }
            }
            if (errors > 0)
            {
                problems.Add(Localizer.Format("#LOC_RTPS_startup_catalogue", errors.ToString()));
            }

            if (PartLoader.LoadedPartsList == null)
            {
                return;
            }
            int hosts = 0;
            for (int i = 0; i < PartLoader.LoadedPartsList.Count; i++)
            {
                AvailablePart available = PartLoader.LoadedPartsList[i];
                Part prefab = available != null ? available.partPrefab : null;
                if (prefab == null || prefab.Modules == null)
                {
                    continue;
                }
                for (int m = 0; m < prefab.Modules.Count; m++)
                {
                    var module = prefab.Modules[m] as ModuleRtpsShield;
                    if (module == null)
                    {
                        continue;
                    }
                    hosts++;
                    string problem = CheckHost(available, prefab, module, catalogue);
                    if (problem != null)
                    {
                        problems.Add(problem);
                        RtpsLog.Error(problem);
                    }
                }
            }
            RtpsLog.Info("startup check: " + hosts + " enrolled host part(s), " + problems.Count + " problem(s)");
        }

        private static string CheckHost(AvailablePart available, Part prefab, ModuleRtpsShield module, MaterialCatalogue catalogue)
        {
            string name = available.name;
            if (!(module.tpsArea > 0.0))
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "tpsArea must be positive");
            }
            for (int i = 0; i < prefab.Modules.Count; i++)
            {
                if (prefab.Modules[i] is ModuleAblator)
                {
                    return Localizer.Format("#LOC_RTPS_startup_host", name, "stock ModuleAblator is still present; a RealTPS host must remove it");
                }
            }
            MaterialDefinition baseline;
            if (!catalogue.TryGet(module.materialId, out baseline))
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "unknown baseline material '" + module.materialId + "'");
            }
            var capabilities = new List<string>();
            if (!string.IsNullOrEmpty(module.hostCapabilities))
            {
                string[] parts = module.hostCapabilities.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Trim().Length > 0)
                    {
                        capabilities.Add(parts[i].Trim());
                    }
                }
            }
            string reason = Compatibility.Check(baseline, module.hostClass, capabilities);
            if (reason != null)
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, reason);
            }
            if (RealTPS.Core.Shield.ShieldLayout.Parse(module.sectors.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                + module.rings.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                + module.centerPercent.ToString("R", System.Globalization.CultureInfo.InvariantCulture)) == null)
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "sectors / rings / centerPercent do not form a valid grid");
            }
            if (module.partRole != RealTPS.Core.Shield.ShieldFailurePolicy.RoleDedicated
                && module.partRole != RealTPS.Core.Shield.ShieldFailurePolicy.RoleMultifunction)
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "partRole must be 'dedicated' or 'multifunction'");
            }
            double[] normal, tangent;
            if (!RealTPS.Core.Shield.ShieldGeometry.TryParseVector(module.protectedFace, out normal)
                || !RealTPS.Core.Shield.ShieldGeometry.TryParseVector(module.sectorZero, out tangent)
                || !RealTPS.Core.Shield.ShieldGeometry.IsUsableFrame(normal, tangent))
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "protectedFace / sectorZero are not a usable face frame");
            }
            if (AssemblyLoaded("B9PartSwitch") && B9SwitcherProxy.Find(prefab, module.switcherModuleId) == null)
            {
                return Localizer.Format("#LOC_RTPS_startup_host", name, "no ModuleB9PartSwitch with moduleID '" + module.switcherModuleId + "'");
            }
            return null;
        }

        private static bool AssemblyLoaded(string name)
        {
            for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
            {
                AssemblyLoader.LoadedAssembly loaded = AssemblyLoader.loadedAssemblies[i];
                if (loaded != null && loaded.assembly != null && string.Equals(loaded.assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
