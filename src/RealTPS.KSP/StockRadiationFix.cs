using System;
using System.Collections;
using System.Reflection;

namespace RealTPS
{
    /// <summary>
    /// Stock KSP 1.12 radiates too much under time warp between 1x and 100x: when a thermal step runs more than one
    /// integration pass, <c>FlightIntegrator.UpdateRadiation</c> changes its incoming-flux terms on every pass. A
    /// shield then runs cooler than at 1x (twice the radiation at 4x physical warp was measured in flight). The
    /// engine owns radiation, so RealTPS does not patch it; KSPCommunityFixes corrects it with its
    /// <c>FIUpdateRadiation</c> patch. This finds out, without a reference to that mod, whether the patch is applied.
    /// </summary>
    internal static class StockRadiationFix
    {
        internal enum Status
        {
            /// <summary>KSPCommunityFixes applied FIUpdateRadiation.</summary>
            Applied,

            /// <summary>KSPCommunityFixes is installed but FIUpdateRadiation is not applied (disabled or failed).</summary>
            NotApplied,

            /// <summary>KSPCommunityFixes is not installed.</summary>
            NotInstalled
        }

        private const string AssemblyName = "KSPCommunityFixes";
        private const string PatchName = "FIUpdateRadiation";
        private const string SettingsNode = "KSP_COMMUNITY_FIXES";

        private static bool known;
        private static Status status;
        private static string detail;

        /// <summary>Whether the stock radiation is corrected. Checked once, after the patches were applied.</summary>
        public static Status Current
        {
            get
            {
                if (!known)
                {
                    status = Check(out detail);
                    known = true;
                }
                return status;
            }
        }

        /// <summary>One line for the log: the status and how it was found out.</summary>
        public static string Describe()
        {
            Status current = Current;
            switch (current)
            {
                case Status.Applied:
                    return "stock radiation under time warp: corrected by KSPCommunityFixes " + PatchName + " (" + detail + ")";
                case Status.NotApplied:
                    return "stock radiation under time warp: KSPCommunityFixes is installed but " + PatchName + " is not applied (" + detail
                        + "); between 1x and 100x warp the game over-counts radiation and shields run cooler than at 1x";
                default:
                    return "stock radiation under time warp: KSPCommunityFixes is not installed; between 1x and 100x warp the game "
                        + "over-counts radiation and shields run cooler than at 1x";
            }
        }

        private static Status Check(out string how)
        {
            Assembly kspcf = null;
            for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
            {
                AssemblyLoader.LoadedAssembly loaded = AssemblyLoader.loadedAssemblies[i];
                if (loaded != null && loaded.assembly != null && string.Equals(loaded.assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    kspcf = loaded.assembly;
                    break;
                }
            }
            if (kspcf == null)
            {
                how = "assembly not loaded";
                return Status.NotInstalled;
            }

            // KSPCommunityFixes.patchInstances (Dictionary<Type, BasePatch>) gets an entry only for a patch that applied.
            try
            {
                Type main = kspcf.GetType(AssemblyName + "." + AssemblyName, false);
                FieldInfo field = main != null ? main.GetField("patchInstances", BindingFlags.Public | BindingFlags.Static) : null;
                var patches = field != null ? field.GetValue(null) as IDictionary : null;
                if (patches != null)
                {
                    foreach (object key in patches.Keys)
                    {
                        var type = key as Type;
                        if (type != null && type.Name == PatchName)
                        {
                            how = "patch applied";
                            return Status.Applied;
                        }
                    }
                    how = "patch not among the applied ones";
                    return Status.NotApplied;
                }
            }
            catch (Exception e)
            {
                RtpsLog.Warn("could not read the applied KSPCommunityFixes patches: " + e.Message);
            }

            // Fallback for a KSPCommunityFixes that no longer exposes its applied patches: the setting that enables it.
            ConfigNode[] settings = GameDatabase.Instance != null ? GameDatabase.Instance.GetConfigNodes(SettingsNode) : null;
            bool enabled;
            if (settings != null && settings.Length == 1 && bool.TryParse(settings[0].GetValue(PatchName), out enabled) && enabled)
            {
                how = "enabled in its settings; applied patches not readable";
                return Status.Applied;
            }
            how = "not enabled in its settings";
            return Status.NotApplied;
        }
    }
}
