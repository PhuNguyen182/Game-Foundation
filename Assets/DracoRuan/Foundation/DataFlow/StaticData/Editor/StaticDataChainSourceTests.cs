using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.Editor;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.StaticData.Editor.Tests
{
    /// <summary>
    /// Covers the parser and rewriter behind the Static Config Data Manager.
    /// </summary>
    /// <remarks>
    /// <para>This is the only part of the tool that writes to a file somebody else wrote, so the
    /// cases below are chosen around the ways a careless rewrite destroys work: dropping a
    /// commented-out binding, turning a <c>const</c> into a literal, following
    /// <c>StaticDataSourceBinding.Chain(</c> into an XML doc comment, or flipping a file's line
    /// endings so every line shows up in the diff.</para>
    ///
    /// <para>No file is touched here — the parser takes text and <c>Render</c> returns text, which
    /// is exactly why the risky logic was split from the window.</para>
    /// </remarks>
    public sealed class StaticDataChainSourceTests
    {
        private const string MultiLineChain =
            "namespace Sample\n" +
            "{\n" +
            "    public sealed class SampleController\n" +
            "    {\n" +
            "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
            "            StaticDataSourceBinding.Chain(\n" +
            "                StaticDataSourceBinding.RemoteConfig(\"gacha_rate\"),\n" +
            "                StaticDataSourceBinding.Resources(\"Configs/GachaRateData\"));\n" +
            "    }\n" +
            "}\n";

        [Test]
        public void Parse_ReadsAMultiLineChain()
        {
            StaticDataChainSource source = Parse(MultiLineChain);

            Assert.IsNotNull(source, StaticDataChainSource.LastParseError);
            Assert.AreEqual(2, source.Steps.Count);
            Assert.AreEqual(StaticDataSourceType.RemoteConfig, source.Steps[0].SourceType);
            Assert.AreEqual("gacha_rate", source.Steps[0].Key);
            Assert.AreEqual(StaticDataSourceType.Resources, source.Steps[1].SourceType);
            Assert.AreEqual("Configs/GachaRateData", source.Steps[1].Key);
        }

        [Test]
        public void Parse_ReadsASingleLineChain()
        {
            // The form used by the existing record tests: no newline between Chain( and the binding.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(StaticDataSourceBinding.Resources(\"levels\"));\n");

            Assert.IsNotNull(source, StaticDataChainSource.LastParseError);
            Assert.AreEqual(1, source.Steps.Count);
            Assert.AreEqual(StaticDataSourceType.Resources, source.Steps[0].SourceType);
            Assert.AreEqual("levels", source.Steps[0].Key);
        }

        [Test]
        public void Parse_KeepsACommentedOutBindingAsADisabledStep()
        {
            // LevelConfigController ships exactly this: a URL link commented out on purpose, with a
            // remarks paragraph explaining why. Dropping it would delete a design note.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                // StaticDataSourceBinding.Url(\"https://cdn/level.csv\"),\n" +
                "                StaticDataSourceBinding.Addressable(\"Configs/LevelConfigData\"),\n" +
                "                StaticDataSourceBinding.Resources(\"Configs/LevelConfigData\"));\n");

            Assert.IsNotNull(source, StaticDataChainSource.LastParseError);
            Assert.AreEqual(3, source.Steps.Count);

            Assert.IsFalse(source.Steps[0].IsEnabled);
            Assert.AreEqual(StaticDataSourceType.Url, source.Steps[0].SourceType);
            Assert.AreEqual("https://cdn/level.csv", source.Steps[0].Key);

            Assert.IsTrue(source.Steps[1].IsEnabled);
            Assert.IsTrue(source.Steps[2].IsEnabled);
        }

        [Test]
        public void Parse_LocksAStepWhoseKeyIsNotALiteral()
        {
            // StaticDataFallbackTests uses const identifiers. Rewriting them as literals would
            // silently dismantle the indirection, so the step is shown but never rewritten.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                StaticDataSourceBinding.RemoteConfig(RemoteKey),\n" +
                "                StaticDataSourceBinding.Resources(\"literal\"));\n");

            Assert.IsNotNull(source, StaticDataChainSource.LastParseError);
            Assert.IsFalse(source.Steps[0].IsKeyEditable);
            Assert.AreEqual("RemoteKey", source.Steps[0].RawArgument);
            Assert.IsTrue(source.Steps[1].IsKeyEditable);
        }

        [Test]
        public void Render_WritesALockedStepBackUnchanged()
        {
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                StaticDataSourceBinding.RemoteConfig(RemoteKey),\n" +
                "                StaticDataSourceBinding.Resources(\"literal\"));\n");

            string rendered = source.Render(source.Steps);

            StringAssert.Contains("StaticDataSourceBinding.RemoteConfig(RemoteKey)", rendered);
            StringAssert.DoesNotContain("\"RemoteKey\"", rendered);
        }

        [Test]
        public void Parse_IgnoresChainCallsInsideDocComments()
        {
            // The base classes document the expected Sources shape inside <code> blocks. Anchoring
            // on Chain( instead of on the Sources declaration would match the documentation and
            // rewrite it.
            StaticDataChainSource source = Parse(
                "    /// <code>\n" +
                "    ///     StaticDataSourceBinding.Chain(\n" +
                "    ///         StaticDataSourceBinding.RemoteConfig(\"doc_only\"));\n" +
                "    /// </code>\n" +
                "    public abstract class Documented\n" +
                "    {\n" +
                "    }\n");

            Assert.IsNull(source);
            StringAssert.Contains("Sources", StaticDataChainSource.LastParseError);
        }

        [Test]
        public void Parse_RejectsAFileDeclaringTwoChains()
        {
            string text = MultiLineChain + MultiLineChain;

            Assert.IsNull(Parse(text));
            StringAssert.Contains("more than one", StaticDataChainSource.LastParseError);
        }

        [Test]
        public void Render_RoundTripsAnUnchangedChainByteForByte()
        {
            // The strongest guarantee the tool can offer: opening a table and pressing Apply without
            // editing anything must leave git with nothing to report.
            StaticDataChainSource source = Parse(MultiLineChain);

            Assert.AreEqual(MultiLineChain, source.Render(source.Steps));
        }

        [Test]
        public void Render_KeepsASingleLineChainOnOneLine()
        {
            // Caught by running the parser over the project's own files: reflowing this to
            // multi-line made an unchanged Apply produce a diff, which defeats the round-trip
            // guarantee even though the code stayed correct.
            string text =
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(StaticDataSourceBinding.Resources(\"levels\"));\n";

            StaticDataChainSource source = Parse(text);

            Assert.AreEqual(text, source.Render(source.Steps));
        }

        [Test]
        public void Render_ExpandsASingleLineChainOnceASecondSourceIsAdded()
        {
            // Adding a link has to break the line, because two bindings on one line would run
            // well past the width the rest of the file is written to.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(StaticDataSourceBinding.Resources(\"levels\"));\n");

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.RemoteConfig, "levels"),
                StaticDataChainStep.NewEditable(StaticDataSourceType.Resources, "levels"),
            };

            string rendered = source.Render(steps);

            StringAssert.Contains("RemoteConfig(\"levels\"),", rendered);
            StringAssert.Contains("Resources(\"levels\"));", rendered);
        }

        [Test]
        public void Render_PutsTheClosingParenthesisOnItsOwnLineAfterADisabledStep()
        {
            // A disabled step last in the chain ends the argument list on a commented line.
            // Appending the closing parenthesis to it would comment that out too, leaving a
            // file that no longer compiles - the one thing a source rewriter must never do.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                // StaticDataSourceBinding.Url(\"https://cdn/x.csv\"),\n" +
                "                StaticDataSourceBinding.Addressable(\"addr\"),\n" +
                "                StaticDataSourceBinding.Resources(\"local\"));\n");

            // Drag the disabled step to the end, the arrangement that used to comment out the
            // closing parenthesis along with it.
            List<StaticDataChainStep> steps = new(source.Steps);
            StaticDataChainStep disabled = steps[0];
            steps.RemoveAt(0);
            steps.Add(disabled);

            string rendered = source.Render(steps);

            StringAssert.Contains("// StaticDataSourceBinding.Url", rendered);

            // The parenthesis that closes Chain( is on a line of its own, uncommented.
            StringAssert.Contains("x.csv\")" + LineBreak + "                );", rendered);

            // And the result is still something the parser can read back.
            StaticDataChainSource reparsed = StaticDataChainSource.TryParse("Assets/Sample.cs", rendered);
            Assert.IsNotNull(reparsed, StaticDataChainSource.LastParseError);
            Assert.AreEqual(3, reparsed.Steps.Count);
        }

        [Test]
        public void Render_OmitsTheCommaOnATrailingDisabledStep()
        {
            // Nothing follows it, so a comma there would separate an argument from nothing.
            StaticDataChainSource source = Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                // StaticDataSourceBinding.Url(\"https://cdn/x.csv\"),\n" +
                "                StaticDataSourceBinding.Addressable(\"addr\"),\n" +
                "                StaticDataSourceBinding.Resources(\"local\"));\n");

            // Drag the disabled step to the end, the arrangement that used to comment out the
            // closing parenthesis along with it.
            List<StaticDataChainStep> steps = new(source.Steps);
            StaticDataChainStep disabled = steps[0];
            steps.RemoveAt(0);
            steps.Add(disabled);

            string rendered = source.Render(steps);

            StringAssert.DoesNotContain("x.csv\"),", rendered);
        }

        [Test]
        public void Render_RoundTripsAChainThatHasADisabledStep()
        {
            string text =
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(\n" +
                "                // StaticDataSourceBinding.Url(\"https://cdn/level.csv\"),\n" +
                "                StaticDataSourceBinding.Addressable(\"Configs/LevelConfigData\"),\n" +
                "                StaticDataSourceBinding.Resources(\"Configs/LevelConfigData\"));\n";

            StaticDataChainSource source = Parse(text);

            Assert.AreEqual(text, source.Render(source.Steps));
        }

        [Test]
        public void Render_PreservesTextOutsideTheChain()
        {
            StaticDataChainSource source = Parse(MultiLineChain);

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.Url, "https://cdn/gacha.json"),
            };

            string rendered = source.Render(steps);

            StringAssert.StartsWith("namespace Sample\n{\n", rendered);
            StringAssert.EndsWith("    }\n}\n", rendered);
            StringAssert.Contains("StaticDataSourceBinding.Url(\"https://cdn/gacha.json\")", rendered);
            StringAssert.DoesNotContain("gacha_rate", rendered);
        }

        [Test]
        public void Render_KeepsCarriageReturnsWhenTheFileHasThem()
        {
            // Rewriting a CRLF file with LF endings turns a one-line change into a whole-file diff.
            string text = MultiLineChain.Replace("\n", "\r\n");
            StaticDataChainSource source = Parse(text);

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.Resources, "Configs/GachaRateData"),
            };

            string rendered = source.Render(steps);

            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(rendered, "(?<!\r)\n"),
                "The rewrite introduced a bare LF into a CRLF file.");
        }

        [Test]
        public void Render_IndentsFromTheDeclarationRatherThanAFixedWidth()
        {
            // A controller nested one level deeper still comes out aligned with its own braces.
            StaticDataChainSource source = Parse(
                "                protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "                    StaticDataSourceBinding.Chain(\n" +
                "                        StaticDataSourceBinding.Resources(\"deep\"));\n");

            string rendered = source.Render(source.Steps);

            StringAssert.Contains("\n                        StaticDataSourceBinding.Resources(\"deep\")", rendered);
        }

        [Test]
        public void Render_PutsACommaAfterEveryStepButTheLastEnabledOne()
        {
            StaticDataChainSource source = Parse(MultiLineChain);

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.RemoteConfig, "a"),
                StaticDataChainStep.NewEditable(StaticDataSourceType.Resources, "b"),
            };

            string rendered = source.Render(steps);

            StringAssert.Contains("StaticDataSourceBinding.RemoteConfig(\"a\"),", rendered);
            StringAssert.Contains("StaticDataSourceBinding.Resources(\"b\"));", rendered);
        }

        [Test]
        public void Render_EscapesAKeyContainingAQuote()
        {
            StaticDataChainSource source = Parse(MultiLineChain);

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.RemoteConfig, "say \"hi\""),
            };

            string rendered = source.Render(steps);

            StringAssert.Contains("RemoteConfig(\"say \\\"hi\\\"\")", rendered);
        }

        [Test]
        public void Parse_RejectsAnUnknownFactory()
        {
            Assert.IsNull(Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            StaticDataSourceBinding.Chain(StaticDataSourceBinding.Ftp(\"nope\"));\n"));

            StringAssert.Contains("Ftp", StaticDataChainSource.LastParseError);
        }

        [Test]
        public void Parse_RejectsAPropertyThatDoesNotUseChain()
        {
            Assert.IsNull(Parse(
                "        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =\n" +
                "            BuildSourcesFromSomewhereElse();\n"));

            StringAssert.Contains("Chain", StaticDataChainSource.LastParseError);
        }

        [Test]
        public void Parse_ReportsAFileWithNoSourcesProperty()
        {
            Assert.IsNull(Parse("public sealed class Nothing { }\n"));
            Assert.IsNotNull(StaticDataChainSource.LastParseError);
        }

        [Test]
        public void PreviewChainText_ShowsExactlyWhatWouldBeWritten()
        {
            StaticDataChainSource source = Parse(MultiLineChain);

            List<StaticDataChainStep> steps = new()
            {
                StaticDataChainStep.NewEditable(StaticDataSourceType.Addressable, "Configs/GachaRateData"),
            };

            string preview = source.PreviewChainText(steps);

            StringAssert.StartsWith("StaticDataSourceBinding.Chain(", preview);
            StringAssert.EndsWith(")", preview);
            StringAssert.Contains("Addressable(\"Configs/GachaRateData\")", preview);
            StringAssert.Contains(preview, source.Render(steps));
        }

        /// <summary>The line ending Render emits for text parsed from a string literal.</summary>
        private const string LineBreak = "\n";

        private static StaticDataChainSource Parse(string text) =>
            StaticDataChainSource.TryParse("Assets/Sample.cs", text);
    }
}
