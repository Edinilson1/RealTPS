using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Delegating decorator around the game's <see cref="PartUpgradeHandler"/>.
    ///
    /// Why it exists: RealTPS gates materials with stock <c>PARTUPGRADE</c> entries. When the "part upgrades"
    /// difficulty option is off in Career or Science, the stock handler answers "locked" for every upgrade,
    /// which would leave only the baseline material forever. For upgrade names starting with <c>RTPS_</c>, and
    /// only in that situation, this decorator answers "the tech node is researched" instead.
    ///
    /// Everything else — every other upgrade, every other game mode, every other member — is forwarded
    /// unchanged to the handler that was installed before RealTPS.
    /// </summary>
    public sealed class RtpsUpgradeHandler : PartUpgradeHandler
    {
        public const string Prefix = "RTPS_";

        private readonly PartUpgradeHandler inner;

        public RtpsUpgradeHandler(PartUpgradeHandler inner)
        {
            this.inner = inner ?? new PartUpgradeHandler();
        }

        /// <summary>The handler this decorator forwards to.</summary>
        public PartUpgradeHandler Inner
        {
            get { return inner; }
        }

        /// <summary>True when RealTPS answers for this upgrade name instead of the wrapped handler.</summary>
        public static bool Applies(string name)
        {
            if (name == null || !name.StartsWith(Prefix, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (!RtpsCatalogue.ResearchGatingWhenUpgradesDisabled)
            {
                return false;
            }
            Game game = HighLogic.CurrentGame;
            if (game == null || game.Parameters == null)
            {
                return false;
            }
            if (game.Mode != Game.Modes.CAREER && game.Mode != Game.Modes.SCIENCE_SANDBOX)
            {
                return false;
            }
            return !game.Parameters.CustomParams<GameParameters.AdvancedParams>().PartUpgradesInCareer;
        }

        private bool TechResearched(string name)
        {
            Upgrade upgrade = inner.GetUpgrade(name);
            if (upgrade == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(upgrade.techRequired))
            {
                return true;
            }
            return ResearchAndDevelopment.GetTechnologyState(upgrade.techRequired) == RDTech.State.Available;
        }

        public override bool IsUnlocked(string name)
        {
            return Applies(name) ? TechResearched(name) : inner.IsUnlocked(name);
        }

        // Overridden together with IsUnlocked: the wrapped handler's own IsEnabled calls its own IsUnlocked.
        public override bool IsEnabled(string name)
        {
            return Applies(name) ? TechResearched(name) : inner.IsEnabled(name);
        }

        public override void FillUpgrades()
        {
            inner.FillUpgrades();
        }

        public override void LinkUpgrades()
        {
            inner.LinkUpgrades();
        }

        public override bool AddUpgrade(Upgrade up)
        {
            return inner.AddUpgrade(up);
        }

        public override bool RemoveUpgrade(string name)
        {
            return inner.RemoveUpgrade(name);
        }

        public override void OnLoad(ConfigNode node)
        {
            inner.OnLoad(node);
        }

        public override void OnSave(ConfigNode node)
        {
            inner.OnSave(node);
        }

        public override bool CanHaveUpgrades()
        {
            return inner.CanHaveUpgrades();
        }

        public override bool IsAvailableToUnlock(string name)
        {
            return inner.IsAvailableToUnlock(name);
        }

        public override void SetUnlocked(string name, bool val)
        {
            inner.SetUnlocked(name, val);
        }

        public override void SetEnabled(string name, bool val)
        {
            inner.SetEnabled(name, val);
        }

        public override Upgrade GetUpgrade(string name)
        {
            return inner.GetUpgrade(name);
        }

        public override float GetUpgradeCost(string name)
        {
            return inner.GetUpgradeCost(name);
        }

        public override bool UgpradesAllowed()
        {
            return inner.UgpradesAllowed();
        }

        public override List<Upgrade> GetUpgradesForTech(string tech)
        {
            return inner.GetUpgradesForTech(tech);
        }

        public override IEnumerator GetEnumerator()
        {
            return inner.GetEnumerator();
        }
    }

    /// <summary>
    /// Keeps the decorator installed. Runs at the start of every scene: if the current handler is not the RealTPS
    /// decorator (first run, or another mod replaced the handler), it wraps whatever is current. It never
    /// unwraps; with the setting off the decorator simply forwards everything.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class RtpsUpgradeGate : MonoBehaviour
    {
        public void Awake()
        {
            Ensure();
        }

        public void Start()
        {
            Ensure();
        }

        public static void Ensure()
        {
            PartUpgradeHandler current = PartUpgradeManager.Handler;
            if (current is RtpsUpgradeHandler)
            {
                return;
            }
            PartUpgradeManager.Handler = new RtpsUpgradeHandler(current);
            RtpsLog.Info("upgrade handler decorated (wrapping " + current.GetType().FullName + ")");
        }
    }
}
