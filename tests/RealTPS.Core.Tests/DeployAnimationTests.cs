using RealTPS.Core.Hosts;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class DeployAnimationTests
    {
        [Fact]
        public void TheConfiguredNameWins()
        {
            DeployAnimation.Match match;
            Assert.Equal(1, DeployAnimation.Resolve("InflatableHS", new[] { "Lights", "InflatableHS" }, out match));
            Assert.Equal(DeployAnimation.Match.Exact, match);
        }

        [Fact]
        public void ARenamedOnlyAnimationIsFollowed()
        {
            // ReStock renames the stock inflatable's animation to Inflate.
            DeployAnimation.Match match;
            Assert.Equal(0, DeployAnimation.Resolve("InflatableHS", new[] { "Inflate" }, out match));
            Assert.Equal(DeployAnimation.Match.OnlyCandidate, match);
        }

        [Fact]
        public void SeveralAnimationsWithoutTheNameAreNotGuessed()
        {
            DeployAnimation.Match match;
            Assert.Equal(-1, DeployAnimation.Resolve("InflatableHS", new[] { "Inflate", "Lights" }, out match));
            Assert.Equal(DeployAnimation.Match.Ambiguous, match);
        }

        [Fact]
        public void NoAnimationIsReported()
        {
            DeployAnimation.Match match;
            Assert.Equal(-1, DeployAnimation.Resolve("InflatableHS", new string[0], out match));
            Assert.Equal(DeployAnimation.Match.None, match);
            Assert.Equal(-1, DeployAnimation.Resolve("InflatableHS", null, out match));
            Assert.Equal(DeployAnimation.Match.None, match);
        }

        [Fact]
        public void TheNameIsCaseSensitive()
        {
            // Unity animation names are case-sensitive; a near miss on a multi-animation part is not a match.
            DeployAnimation.Match match;
            Assert.Equal(-1, DeployAnimation.Resolve("inflatablehs", new[] { "InflatableHS", "Lights" }, out match));
            Assert.Equal(DeployAnimation.Match.Ambiguous, match);
        }
    }
}
