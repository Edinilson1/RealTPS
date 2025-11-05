using System;
using System.Collections.Generic;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using Xunit;

namespace RealTPS.Core.Tests
{

    public class CfgParserTests
    {
        [Fact]
        public void ParsesNodesValuesCommentsAndModuleManagerNames()
        {
            CfgNode root = CfgParser.Parse("// c\n@PART[HeatShield1]:NEEDS[RealTPSRuntime&B9PartSwitch]:FOR[RealTPS]\n{\n\t!MODULE[ModuleAblator] {}\n\t@cost -= 100 // note\n\tMODULE { name = X\n NESTED\n {\n  a = b = c\n }\n }\n}\n");
            CfgNode part = root.Nodes[0];
            Assert.Equal("@PART[HeatShield1]:NEEDS[RealTPSRuntime&B9PartSwitch]:FOR[RealTPS]", part.Name);
            Assert.Equal("!MODULE[ModuleAblator]", part.Nodes[0].Name);
            // ModuleManager operators stay part of the key: "@cost -= 100" is key "@cost -", value "100".
            Assert.Equal("100", part.GetValue("@cost -"));
            Assert.Equal("b = c", part.GetNode("MODULE").GetNode("NESTED").GetValue("a"));
        }

        [Theory]
        [InlineData("A {")]
        [InlineData("A { } }")]
        [InlineData("{ }")]
        [InlineData("A\nB { }")]
        public void MalformedTextThrows(string text)
        {
            Assert.Throws<CfgParser.ParseException>(() => CfgParser.Parse(text));
        }
    }
}
