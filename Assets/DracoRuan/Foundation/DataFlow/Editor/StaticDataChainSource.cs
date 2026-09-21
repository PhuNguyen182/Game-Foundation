using System;
using System.Collections.Generic;
using System.Text;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// One step of a fallback chain as it is written in a controller's source file.
    /// </summary>
    public sealed class StaticDataChainStep
    {
        public StaticDataChainStep(StaticDataSourceType sourceType, string key, bool isEnabled,
            bool isKeyEditable, string rawArgument)
        {
            this.SourceType = sourceType;
            this.Key = key;
            this.IsEnabled = isEnabled;
            this.IsKeyEditable = isKeyEditable;
            this.RawArgument = rawArgument;
        }

        public StaticDataSourceType SourceType { get; set; }

        /// <summary>The key as it will be written. Meaningless when <see cref="IsKeyEditable"/> is false.</summary>
        public string Key { get; set; }

        /// <summary>False when the step is commented out in the source. Written back as a comment.</summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// False when the argument is not a plain string literal — a <c>const</c> identifier, say.
        /// Such a step is shown but never rewritten, because turning <c>RemoteKey</c> into
        /// <c>"remote_key"</c> would silently dismantle indirection somebody built on purpose.
        /// </summary>
        public bool IsKeyEditable { get; }

        /// <summary>The argument exactly as it appears in the file, used to write a locked step back.</summary>
        public string RawArgument { get; }

        public static StaticDataChainStep NewEditable(StaticDataSourceType sourceType, string key,
            bool isEnabled = true) =>
            new(sourceType, key, isEnabled, isKeyEditable: true, rawArgument: null);
    }

    /// <summary>
    /// Reads and rewrites the <c>Sources</c> chain inside a controller's <c>.cs</c> file.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the source file and not reflection.</b> <c>Sources</c> is
    /// <c>protected abstract</c>, so reading it needs a constructed controller, which needs the
    /// container — which does not exist in edit mode. The file is the only place the chain can be
    /// read or changed without running the game.</para>
    ///
    /// <para><b>Why a hand-written scanner and not a regex.</b> The chains in this project already
    /// come in three shapes — multi-line, single-line, and multi-line with a commented-out step —
    /// and <c>StaticDataSourceBinding.Chain(</c> also appears inside XML doc comments on two base
    /// classes, where touching it would corrupt documentation. A line-oriented regex gets at least
    /// one of those wrong. This scanner tracks comment and string state explicitly, anchors on the
    /// <c>Sources</c> property rather than on <c>Chain</c>, and matches parentheses by counting, so
    /// every shape resolves to the same span.</para>
    ///
    /// <para><b>It refuses rather than guesses.</b> Every entry point returns null or false when the
    /// text is not exactly what it expects. The tool then shows that table read-only with the reason,
    /// which is always better than writing a file back in a shape nobody verified.</para>
    /// </remarks>
    public sealed class StaticDataChainSource
    {
        private const string SourcesAnchor = "IReadOnlyList<StaticDataSourceBinding> Sources";
        private const string ChainCall = "StaticDataSourceBinding.Chain";
        private const string BindingPrefix = "StaticDataSourceBinding.";

        private StaticDataChainSource(string filePath, string text, int chainStart, int chainEnd,
            string indent, string newLine, bool wasSingleLine, IReadOnlyList<StaticDataChainStep> steps)
        {
            this.FilePath = filePath;
            this.Text = text;
            this.ChainStart = chainStart;
            this.ChainEnd = chainEnd;
            this.Indent = indent;
            this.NewLine = newLine;
            this.WasSingleLine = wasSingleLine;
            this.Steps = steps;
        }

        public string FilePath { get; }

        /// <summary>The whole file, verbatim. Everything outside the chain span is preserved byte for byte.</summary>
        private string Text { get; }

        /// <summary>Index of the <c>S</c> in <c>StaticDataSourceBinding.Chain(</c>.</summary>
        private int ChainStart { get; }

        /// <summary>Index just past the closing <c>)</c> of the chain call.</summary>
        private int ChainEnd { get; }

        /// <summary>Indentation of the line holding the <c>Sources</c> declaration, read from the file.</summary>
        private string Indent { get; }

        /// <summary>
        /// True when the whole call was written on one line. Preserved so that opening a table and
        /// applying it unchanged does not reflow the file and show up as a diff.
        /// </summary>
        private bool WasSingleLine { get; }

        /// <summary>The file's own line ending, so a rewrite never mixes CRLF and LF.</summary>
        private string NewLine { get; }

        public IReadOnlyList<StaticDataChainStep> Steps { get; }

        /// <summary>Why the file could not be parsed, when <see cref="TryParse"/> returned null.</summary>
        public static string LastParseError { get; private set; }

        /// <summary>
        /// Parses the chain out of <paramref name="text"/>, or returns null and sets
        /// <see cref="LastParseError"/>.
        /// </summary>
        public static StaticDataChainSource TryParse(string filePath, string text)
        {
            LastParseError = null;

            if (string.IsNullOrEmpty(text))
            {
                LastParseError = "The script file is empty.";
                return null;
            }

            int anchor = IndexOfCode(text, SourcesAnchor, 0);
            if (anchor < 0)
            {
                LastParseError =
                    "No 'IReadOnlyList<StaticDataSourceBinding> Sources' declaration was found in this file.";
                return null;
            }

            // A second declaration means this file holds more than one controller, and there is no
            // way to tell from here which one the selected type owns.
            if (IndexOfCode(text, SourcesAnchor, anchor + SourcesAnchor.Length) >= 0)
            {
                LastParseError =
                    "This file declares more than one 'Sources' property, so the tool cannot tell " +
                    "which one belongs to this controller. Edit it by hand.";
                return null;
            }

            int chainStart = IndexOfCode(text, ChainCall, anchor);
            if (chainStart < 0)
            {
                LastParseError =
                    "The 'Sources' property does not use 'StaticDataSourceBinding.Chain(...)'. " +
                    "Only that form can be rewritten.";
                return null;
            }

            int openParen = IndexOfCode(text, "(", chainStart + ChainCall.Length);
            if (openParen < 0)
            {
                LastParseError = "The 'Chain' call is malformed.";
                return null;
            }

            int closeParen = FindMatchingParen(text, openParen);
            if (closeParen < 0)
            {
                LastParseError = "The 'Chain' call has no matching closing parenthesis.";
                return null;
            }

            string body = text.Substring(openParen + 1, closeParen - openParen - 1);
            List<StaticDataChainStep> steps = ParseSteps(body);
            if (steps == null)
                return null;

            return new StaticDataChainSource(
                filePath, text, chainStart, closeParen + 1,
                ReadIndent(text, anchor), DetectNewLine(text),
                IsSingleLine(body), steps);
        }

        /// <summary>True when the chain's argument list occupies a single line.</summary>
        private static bool IsSingleLine(string body) => !body.Contains(LineFeed);

        /// <summary>A bare line feed, built without an escape sequence.</summary>
        private static readonly string LineFeed = ((char)10).ToString();

        /// <summary>
        /// Builds the file's new contents with <paramref name="steps"/> in place of the current
        /// chain. Everything outside the chain call is untouched.
        /// </summary>
        public string Render(IReadOnlyList<StaticDataChainStep> steps)
        {
            StringBuilder builder = new();

            builder.Append(this.Text, 0, this.ChainStart);
            builder.Append(ChainCall).Append('(');

            // A chain that arrived on one line goes back on one line. A disabled step needs a line
            // of its own to be commented out, so the single-line form only survives while every
            // step is enabled.
            if (this.WasSingleLine && steps.Count == 1 && steps[0].IsEnabled)
            {
                StaticDataChainStep only = steps[0];

                builder.Append(BindingPrefix).Append(only.SourceType).Append('(')
                    .Append(only.IsKeyEditable ? Quote(only.Key) : only.RawArgument).Append(')');

                builder.Append(')');
                builder.Append(this.Text, this.ChainEnd, this.Text.Length - this.ChainEnd);
                return builder.ToString();
            }

            // One extra level in from the property's own indentation, matching how every chain in
            // the project is already written.
            string stepIndent = this.Indent + "        ";

            for (int index = 0; index < steps.Count; index++)
            {
                StaticDataChainStep step = steps[index];

                builder.Append(this.NewLine).Append(stepIndent);

                if (!step.IsEnabled)
                    builder.Append("// ");

                builder.Append(BindingPrefix).Append(step.SourceType).Append('(')
                    .Append(step.IsKeyEditable ? Quote(step.Key) : step.RawArgument).Append(')');

                // A disabled step keeps its trailing comma inside the comment so that enabling it
                // again is a matter of deleting "// ", and so the last enabled step still ends the
                // argument list cleanly.
                if (!step.IsEnabled || HasEnabledStepAfter(steps, index))
                    builder.Append(',');
            }

            builder.Append(')');
            builder.Append(this.Text, this.ChainEnd, this.Text.Length - this.ChainEnd);

            return builder.ToString();
        }

        /// <summary>The chain call as it currently stands, for a before/after diff in the dialog.</summary>
        public string GetCurrentChainText() => this.Text.Substring(this.ChainStart, this.ChainEnd - this.ChainStart);

        /// <summary>
        /// The chain call the given steps would produce, on its own — what the tool shows as a
        /// preview so nothing is written that was not seen first.
        /// </summary>
        public string PreviewChainText(IReadOnlyList<StaticDataChainStep> steps)
        {
            string rendered = this.Render(steps);

            // Render only ever changes the span between ChainStart and its closing paren, so the
            // preview is that same span read back out of the result.
            int end = FindMatchingParen(rendered, IndexOfCode(rendered, "(", this.ChainStart + ChainCall.Length));
            return end < 0 ? string.Empty : rendered.Substring(this.ChainStart, end + 1 - this.ChainStart);
        }

        private static bool HasEnabledStepAfter(IReadOnlyList<StaticDataChainStep> steps, int index)
        {
            for (int next = index + 1; next < steps.Count; next++)
            {
                if (steps[next].IsEnabled)
                    return true;
            }

            return false;
        }

        // -----------------------------------------------------------------
        // Step parsing
        // -----------------------------------------------------------------

        /// <summary>
        /// Reads the arguments of the <c>Chain(...)</c> call, keeping commented-out steps as disabled
        /// entries rather than dropping them.
        /// </summary>
        private static List<StaticDataChainStep> ParseSteps(string body)
        {
            List<StaticDataChainStep> steps = new();
            int index = 0;

            while (index < body.Length)
            {
                int found = body.IndexOf(BindingPrefix, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                bool isCommented = IsInsideLineComment(body, found);

                int nameStart = found + BindingPrefix.Length;
                int nameEnd = nameStart;
                while (nameEnd < body.Length && (char.IsLetterOrDigit(body[nameEnd]) || body[nameEnd] == '_'))
                    nameEnd++;

                string factoryName = body.Substring(nameStart, nameEnd - nameStart);

                if (nameEnd >= body.Length || body[nameEnd] != '(' ||
                    !TryMapFactory(factoryName, out StaticDataSourceType sourceType))
                {
                    LastParseError =
                        $"'{BindingPrefix}{factoryName}' is not one of the four source factories " +
                        "(RemoteConfig, Resources, Addressable, Url).";
                    return null;
                }

                int argEnd = FindMatchingParen(body, nameEnd);
                if (argEnd < 0)
                {
                    LastParseError = $"The '{factoryName}' call has no matching closing parenthesis.";
                    return null;
                }

                string rawArgument = body.Substring(nameEnd + 1, argEnd - nameEnd - 1).Trim();
                bool isLiteral = TryReadStringLiteral(rawArgument, out string key);

                steps.Add(new StaticDataChainStep(
                    sourceType, isLiteral ? key : rawArgument, !isCommented, isLiteral, rawArgument));

                index = argEnd + 1;
            }

            if (steps.Count != 0)
                return steps;

            LastParseError = "The 'Chain(...)' call has no source bindings in it.";
            return null;
        }

        private static bool TryMapFactory(string name, out StaticDataSourceType sourceType)
        {
            switch (name)
            {
                case "RemoteConfig":
                    sourceType = StaticDataSourceType.RemoteConfig;
                    return true;
                case "Resources":
                    sourceType = StaticDataSourceType.Resources;
                    return true;
                case "Addressable":
                    sourceType = StaticDataSourceType.Addressable;
                    return true;
                case "Url":
                    sourceType = StaticDataSourceType.Url;
                    return true;
                default:
                    sourceType = StaticDataSourceType.None;
                    return false;
            }
        }

        /// <summary>
        /// Reads a plain (non-verbatim, non-interpolated) string literal. Anything else — a const,
        /// an expression, an interpolation — is reported as not a literal and left alone.
        /// </summary>
        private static bool TryReadStringLiteral(string argument, out string value)
        {
            value = null;

            if (argument.Length < 2 || argument[0] != '"' || argument[argument.Length - 1] != '"')
                return false;

            StringBuilder builder = new();

            for (int index = 1; index < argument.Length - 1; index++)
            {
                char character = argument[index];

                if (character != '\\')
                {
                    // An unescaped quote before the end means this is not one single literal.
                    if (character == '"')
                        return false;

                    builder.Append(character);
                    continue;
                }

                index++;
                if (index >= argument.Length - 1)
                    return false;

                switch (argument[index])
                {
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case '\\': builder.Append('\\'); break;
                    case '"': builder.Append('"'); break;

                    // Unicode and the rest are legal C# but rare in a config key; rewriting one
                    // would mean re-encoding it correctly, so the step is locked instead.
                    default: return false;
                }
            }

            value = builder.ToString();
            return true;
        }

        private static string Quote(string value)
        {
            StringBuilder builder = new();
            builder.Append('"');

            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(character); break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        // -----------------------------------------------------------------
        // Text scanning
        // -----------------------------------------------------------------

        /// <summary>
        /// <see cref="string.IndexOf(string, int, StringComparison)"/> restricted to real code —
        /// matches inside comments, strings and char literals are skipped.
        /// </summary>
        /// <remarks>
        /// This is what keeps the tool away from the <c>Chain(</c> written in the XML documentation
        /// of <c>StaticDataController&lt;TData&gt;</c> and <c>StaticRecordDataController</c>.
        /// </remarks>
        private static int IndexOfCode(string text, string value, int startIndex)
        {
            foreach (int index in EnumerateCodeIndices(text, startIndex))
            {
                if (index + value.Length <= text.Length &&
                    string.CompareOrdinal(text, index, value, 0, value.Length) == 0)
                    return index;
            }

            return -1;
        }

        /// <summary>Walks the text and yields only the indices that sit in code.</summary>
        private static IEnumerable<int> EnumerateCodeIndices(string text, int startIndex)
        {
            for (int index = startIndex; index < text.Length; index++)
            {
                char character = text[index];

                if (character == '/' && index + 1 < text.Length)
                {
                    if (text[index + 1] == '/')
                    {
                        index = SkipLineComment(text, index);
                        continue;
                    }

                    if (text[index + 1] == '*')
                    {
                        index = SkipBlockComment(text, index);
                        continue;
                    }
                }

                if (character == '"')
                {
                    index = SkipStringLiteral(text, index);
                    continue;
                }

                if (character == '\'')
                {
                    index = SkipCharLiteral(text, index);
                    continue;
                }

                yield return index;
            }
        }

        /// <summary>Index of the last character of the line comment starting at <paramref name="start"/>.</summary>
        private static int SkipLineComment(string text, int start)
        {
            int index = start + 2;
            while (index < text.Length && text[index] != '\n')
                index++;

            return index;
        }

        private static int SkipBlockComment(string text, int start)
        {
            int index = start + 2;
            while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/'))
                index++;

            return index + 1 < text.Length ? index + 1 : text.Length;
        }

        /// <summary>
        /// Handles both <c>"…"</c> and <c>@"…"</c>, including the doubled-quote escape inside a
        /// verbatim string.
        /// </summary>
        private static int SkipStringLiteral(string text, int quoteIndex)
        {
            bool isVerbatim = quoteIndex > 0 && text[quoteIndex - 1] == '@';
            int index = quoteIndex + 1;

            while (index < text.Length)
            {
                char character = text[index];

                if (isVerbatim)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            index += 2;
                            continue;
                        }

                        return index;
                    }
                }
                else
                {
                    if (character == '\\')
                    {
                        index += 2;
                        continue;
                    }

                    if (character == '"')
                        return index;

                    // An unterminated literal would otherwise swallow the rest of the file.
                    if (character == '\n')
                        return index;
                }

                index++;
            }

            return text.Length;
        }

        private static int SkipCharLiteral(string text, int quoteIndex)
        {
            int index = quoteIndex + 1;

            while (index < text.Length)
            {
                if (text[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (text[index] == '\'' || text[index] == '\n')
                    return index;

                index++;
            }

            return text.Length;
        }

        /// <summary>Matches the parenthesis at <paramref name="openIndex"/>, ignoring comments and strings.</summary>
        private static int FindMatchingParen(string text, int openIndex)
        {
            if (openIndex < 0 || openIndex >= text.Length || text[openIndex] != '(')
                return -1;

            int depth = 0;

            foreach (int index in EnumerateCodeIndices(text, openIndex))
            {
                char character = text[index];

                if (character == '(')
                {
                    depth++;
                    continue;
                }

                if (character != ')')
                    continue;

                depth--;
                if (depth == 0)
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// True when the index sits after a <c>//</c> on its own line — the test for a
        /// commented-out binding inside the chain body.
        /// </summary>
        private static bool IsInsideLineComment(string body, int index)
        {
            int lineStart = body.LastIndexOf('\n', Math.Max(0, Math.Min(index, body.Length - 1)));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            for (int scan = lineStart; scan < index - 1; scan++)
            {
                if (body[scan] == '/' && body[scan + 1] == '/')
                    return true;
            }

            return false;
        }

        /// <summary>Whitespace at the start of the line containing <paramref name="index"/>.</summary>
        private static string ReadIndent(string text, int index)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, Math.Min(index, text.Length - 1)));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            int scan = lineStart;
            while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
                scan++;

            return text.Substring(lineStart, scan - lineStart);
        }

        /// <summary>
        /// The file's dominant line ending. Read rather than assumed, so rewriting a file on Windows
        /// does not leave one lonely LF in a CRLF file (or the reverse) for git to report.
        /// </summary>
        private static string DetectNewLine(string text)
        {
            int index = text.IndexOf('\n');
            if (index < 0)
                return Environment.NewLine;

            return index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
        }
    }
}