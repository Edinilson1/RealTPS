using System;
using System.Collections.Generic;

namespace RealTPS.Core.Hosts
{
    /// <summary>
    /// Picks the animation that opens a deployable shield. The host names one (<c>deployAnimation</c>), but a mod
    /// that remodels the part can rename it: ReStock calls the stock inflatable's animation <c>Inflate</c> instead
    /// of <c>InflatableHS</c>. When no animation carries the configured name and the part has exactly one, that one
    /// opens the shield. With none, or several and none named, the shield cannot follow its deployment.
    /// </summary>
    public static class DeployAnimation
    {
        public enum Match
        {
            /// <summary>The part has no animation module.</summary>
            None,

            /// <summary>An animation carries the configured name.</summary>
            Exact,

            /// <summary>No animation carries the configured name; the part's only one is used.</summary>
            OnlyCandidate,

            /// <summary>Several animations and none carries the configured name.</summary>
            Ambiguous
        }

        /// <summary>Index into <paramref name="names"/> of the animation to follow, or -1.</summary>
        public static int Resolve(string configured, IList<string> names, out Match match)
        {
            int count = names != null ? names.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(names[i], configured, StringComparison.Ordinal))
                {
                    match = Match.Exact;
                    return i;
                }
            }
            if (count == 1)
            {
                match = Match.OnlyCandidate;
                return 0;
            }
            match = count == 0 ? Match.None : Match.Ambiguous;
            return -1;
        }
    }
}
