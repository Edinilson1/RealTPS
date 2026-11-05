using System.Collections.Generic;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Generation;
using RealTPS.Core.Materials;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class LocalizationRulesTests
    {
        private const string Detail = "#LOC_RTPS_mat_test_ablator_detail";

        private static MaterialCatalogue Catalogue()
        {
            return MaterialCatalogue.Load(CfgParser.Parse(TestMaterials.AblatorCfg).GetNodes(MaterialParser.NodeName));
        }

        private static LocalizationRules.Table Table(params string[] files)
        {
            return LocalizationRules.Load(files.Select((text, i) => new KeyValuePair<string, string>("file" + i + ".cfg", text)));
        }

        private static string Language(string name, params string[] lines)
        {
            return "Localization\n{\n\t" + name + "\n\t{\n\t\t" + string.Join("\n\t\t", lines) + "\n\t}\n}\n";
        }

        private static List<string> Errors(LocalizationRules.Table table)
        {
            return table.Issues.Concat(LocalizationRules.Check(table, Catalogue())).Where(i => i.IsError).Select(i => i.ToString()).ToList();
        }

        [Fact]
        public void ANewLanguageIsJustAnotherFile()
        {
            LocalizationRules.Table table = Table(
                Language("en-us", "#LOC_RTPS_info = Area <<1>> m²", Detail + " = Limit 2500 K, bond 520 K, 50 mm, 0.2 W/(m·K), 25 kg/m², 5 MJ/kg, 50 kPa"),
                Language("pt-br", "#LOC_RTPS_info = Área <<1>> m²", Detail + " = Limite 2500 K, cola 520 K, 50 mm, 0,2 W/(m·K), 25 kg/m², 5 MJ/kg, 50 kPa"),
                Language("es-es", "#LOC_RTPS_info = Área <<1>> m²", Detail + " = Límite 2500 K"));
            Assert.Equal(new[] { "en-us", "es-es", "pt-br" }, table.Languages.Keys.ToArray());
            Assert.Empty(Errors(table));
        }

        [Fact]
        public void AStaleNumberInADetailLineIsAnError()
        {
            // The bond limit was 530 K in an older profile; the material now says 520.
            LocalizationRules.Table table = Table(Language("en-us", Detail + " = Limit 2500 K, bond 530 K"));
            List<string> errors = Errors(table);
            Assert.Single(errors);
            Assert.Contains("530", errors[0]);
        }

        [Fact]
        public void NumbersMatchAsRounded()
        {
            Assert.True(LocalizationRules.Matches("0.059", 0.0592));
            Assert.True(LocalizationRules.Matches("0,06", 0.0592));
            Assert.True(LocalizationRules.Matches("28.8", 28.8));
            Assert.True(LocalizationRules.Matches("1900", 1900.0));
            Assert.False(LocalizationRules.Matches("1640", 1644.0));
            Assert.False(LocalizationRules.Matches("0.07", 0.0592));
            Assert.False(LocalizationRules.Matches("520", 530.0));
        }

        [Fact]
        public void OnlyEnglishIsRequiredButATranslationMustKeepThePlaceholders()
        {
            LocalizationRules.Table table = Table(
                Language("en-us", "#LOC_RTPS_a = one <<1>> two <<2>>", "#LOC_RTPS_b = b", Detail + " = 2500 K"),
                Language("pt-br", "#LOC_RTPS_a = um <<1>>", "#LOC_RTPS_extra = x"));
            List<string> errors = Errors(table);
            Assert.Single(errors);
            Assert.Contains(errors, e => e.Contains("#LOC_RTPS_a") && e.Contains("placeholders"));
            // Untranslated tags (a plain one and a material's detail line) and a tag English lacks: warnings only.
            List<ValidationIssue> issues = LocalizationRules.Check(table, Catalogue());
            Assert.Contains(issues, i => !i.IsError && i.Message.Contains("#LOC_RTPS_b") && i.Message.Contains("not translated"));
            Assert.Contains(issues, i => !i.IsError && i.Message.Contains(Detail) && i.Message.Contains("not translated"));
            Assert.Contains(issues, i => !i.IsError && i.Message.Contains("#LOC_RTPS_extra"));
        }

        [Fact]
        public void EveryMaterialNeedsADetailLineAndEnglishMustExist()
        {
            Assert.Contains(Errors(Table(Language("en-us", "#LOC_RTPS_a = a"))), e => e.Contains("test-ablator") && e.Contains(Detail));
            Assert.Contains(Errors(Table(Language("pt-br", Detail + " = 2500 K"))), e => e.Contains("en-us"));
        }

        [Fact]
        public void DuplicateAndEmptyTagsAreErrors()
        {
            LocalizationRules.Table table = Table(
                Language("en-us", Detail + " = 2500 K", "#LOC_RTPS_a = a"),
                Language("en-us", "#LOC_RTPS_a = again", "#LOC_RTPS_b = "));
            Assert.Equal(2, table.Issues.Count(i => i.IsError));
        }
    }
}
