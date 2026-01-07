namespace RealTPS.Core.Generation
{
    /// <summary>
    /// Repository-relative paths of every generated file. The tool that writes them and the test that checks
    /// they are current both read the paths from here.
    /// </summary>
    public static class GeneratedFiles
    {
        public const string ModFolder = "GameData/RealTPS";

        /// <summary><c>RESOURCE_DEFINITION</c> of each consumable material.</summary>
        public const string Resources = ModFolder + "/Resources/MaterialResources.cfg";

        /// <summary><c>PARTUPGRADE</c> of each unlockable material, on its stock tech node.</summary>
        public const string Unlocks = ModFolder + "/TechTree/MaterialUnlocks.cfg";

        /// <summary>Enrolment patches of the stock host parts.</summary>
        public const string StockHosts = ModFolder + "/Patches/StockHosts.cfg";
    }
}
