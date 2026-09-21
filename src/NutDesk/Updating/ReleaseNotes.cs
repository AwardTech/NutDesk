using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NutDesk.Updating
{
    /// <summary>What a block of release notes is, once its Markdown has been read.</summary>
    public enum ReleaseNoteKind
    {
        /// <summary>A "#" heading, or a line underlined with "===" or "---".</summary>
        Heading,

        /// <summary>An item of a bulleted, numbered or task list.</summary>
        Bullet,

        /// <summary>Running text, or one row of a table.</summary>
        Paragraph,

        /// <summary>The content of a fenced code block, line breaks kept.</summary>
        Code,
    }

    /// <summary>
    /// One block of release notes, as plain text. Not changed any more once <see cref="ReleaseNotes.Parse"/> has
    /// returned it.
    /// </summary>
    public sealed class ReleaseNoteBlock
    {
        internal ReleaseNoteBlock(ReleaseNoteKind kind, int level, string marker, string text)
        {
            Kind = kind;
            Level = level;
            Marker = marker;
            Text = text ?? string.Empty;
        }

        public ReleaseNoteKind Kind { get; }

        /// <summary>A heading's level, 1 to 6; a list item's nesting depth, from 0; otherwise 0.</summary>
        public int Level { get; }

        /// <summary>A list item's bullet, number ("3.") or task box; null for anything else.</summary>
        public string Marker { get; }

        /// <summary>The text, with the Markdown markup taken out.</summary>
        public string Text { get; private set; }

        /// <summary>A line that carries on the block: Markdown wraps a paragraph or a list item over several lines.</summary>
        internal void Append(string more)
        {
            if (!string.IsNullOrEmpty(more))
            {
                Text = Text.Length == 0 ? more : Text + " " + more;
            }
        }

        public override string ToString()
        {
            return Kind + (Marker == null ? string.Empty : " " + Marker) + ": " + Text;
        }
    }

    /// <summary>
    /// Turns the Markdown of a GitHub release into readable text for the update window.
    /// </summary>
    /// <remarks>
    /// This is a light clean-up, not a Markdown renderer. It recognises what release notes are made of - headings,
    /// lists, paragraphs, fenced code, the odd table - and drops the markup a reader would otherwise have to look
    /// past: emphasis markers, link targets, code quotes, HTML comments and the few HTML tags people type. Anything
    /// it does not recognise is shown as it was written, which is still readable. It never throws: the notes come
    /// from the network, and a formatting problem must not cost the user the update window.
    /// </remarks>
    public static class ReleaseNotes
    {
        private const string Bullet = "•";
        private const string NestedBullet = "◦";
        private const string OpenTask = "☐";
        private const string DoneTask = "☑";

        // Deeper lists are drawn at this depth: the window is narrow, and six levels of indent would leave no room.
        private const int MaxListDepth = 3;

        // Stand-ins for escaped characters and code spans while the emphasis markers are removed, so that neither
        // loses characters that only look like markup. Private-use characters do not occur in real notes.
        private const char EscapeMark = '';
        private const char CodeMark = '';

        private static readonly Regex HtmlComment = new Regex(@"<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex AtxHeading = new Regex(@"^(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$");
        private static readonly Regex SetextUnderline = new Regex(@"^(?:=+|-+)$");
        private static readonly Regex ThematicBreak = new Regex(@"^(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$");
        private static readonly Regex BulletItem = new Regex(@"^[-*+][ \t]+(.*)$");
        private static readonly Regex NumberedItem = new Regex(@"^(\d{1,9})[.)][ \t]+(.*)$");
        private static readonly Regex TaskBox = new Regex(@"^\[([ xX])\][ \t]+");
        private static readonly Regex TableSeparator = new Regex(@"^\|?[ \t]*:?-+:?[ \t]*(?:\|[ \t]*:?-+:?[ \t]*)*\|?$");

        private static readonly Regex Escape = new Regex(@"\\([\\`*_{}\[\]()#+\-.!|<>~])");
        private static readonly Regex CodeSpan = new Regex(@"(`+)(.+?)\1");
        private static readonly Regex Image = new Regex(@"!\[([^\]]*)\]\((?:[^()]|\([^)]*\))*\)");
        private static readonly Regex InlineLink = new Regex(@"\[([^\]]+)\]\((?:[^()]|\([^)]*\))*\)");
        private static readonly Regex ReferenceLink = new Regex(@"\[([^\]]+)\]\[[^\]]*\]");
        private static readonly Regex AutoLink = new Regex(@"<((?:https?|mailto):[^<>\s]+)>", RegexOptions.IgnoreCase);
        private static readonly Regex LineBreakTag = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase);
        private static readonly Regex FormattingTag = new Regex(
            @"</?(?:p|b|i|u|s|em|strong|kbd|sub|sup|code|del|ins|span|div|details|summary)\b[^<>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex StrongStars = new Regex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*");
        private static readonly Regex StrongUnderscores = new Regex(@"(?<!\w)__(?=\S)(.+?)(?<=\S)__(?!\w)");
        private static readonly Regex EmphasisStar = new Regex(@"(?<![*\w])\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?![*\w])");
        // Only between non-word characters, so that NUT_AutoReconnect or file_name_here keep their underscores.
        private static readonly Regex EmphasisUnderscore = new Regex(@"(?<!\w)_(?=[^\s_])(.+?)(?<=[^\s_])_(?!\w)");
        private static readonly Regex Strikethrough = new Regex(@"~~(?=\S)(.+?)(?<=\S)~~");
        private static readonly Regex Whitespace = new Regex(@"\s+");

        /// <summary>
        /// Reads release notes into blocks. Line ends of any kind are accepted; lines that Markdown joins into one
        /// paragraph or one list item are joined with a space. Empty for null or blank notes.
        /// </summary>
        public static IList<ReleaseNoteBlock> Parse(string markdown)
        {
            var blocks = new List<ReleaseNoteBlock>();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return blocks;
            }

            try
            {
                ParseInto(markdown, blocks);
            }
            catch (Exception)
            {
                // Last resort, see the remarks: the notes as they are, one paragraph per line.
                blocks.Clear();
                foreach (var line in Normalize(markdown).Split('\n'))
                {
                    blocks.Add(new ReleaseNoteBlock(ReleaseNoteKind.Paragraph, 0, null, line.Trim()));
                }
            }

            blocks.RemoveAll(block => string.IsNullOrWhiteSpace(block.Text));
            return blocks;
        }

        /// <summary>
        /// The notes as plain text, one block per line: list items keep their bullet or number, nested ones are
        /// indented, and a blank line goes before each heading.
        /// </summary>
        public static string ToPlainText(string markdown)
        {
            var text = new StringBuilder();
            foreach (var block in Parse(markdown))
            {
                if (block.Kind == ReleaseNoteKind.Heading && text.Length > 0)
                {
                    text.AppendLine();
                }

                if (block.Kind == ReleaseNoteKind.Bullet)
                {
                    text.Append(' ', block.Level * 2).Append(block.Marker).Append(' ');
                }

                text.AppendLine(block.Text);
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// Removes the inline markup from one line or table cell: emphasis markers, link targets (the link text
        /// stays), images (their description stays), code quotes, the common HTML formatting tags and entities.
        /// Runs of white space become one space.
        /// </summary>
        public static string CleanInline(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var escaped = new List<string>();
            var code = new List<string>();

            // Escapes first: "\*" is a star, never the start of emphasis. Then code spans, whose content is shown
            // exactly as written, underscores and stars included.
            text = Escape.Replace(text, m => Hold(escaped, EscapeMark, m.Groups[1].Value));
            text = CodeSpan.Replace(text, m => Hold(code, CodeMark, m.Groups[2].Value.Trim()));

            text = Image.Replace(text, "$1");
            text = InlineLink.Replace(text, "$1");
            text = ReferenceLink.Replace(text, "$1");
            text = AutoLink.Replace(text, "$1");
            text = LineBreakTag.Replace(text, " ");
            text = FormattingTag.Replace(text, string.Empty);

            text = StrongStars.Replace(text, "$1");
            text = StrongUnderscores.Replace(text, "$1");
            text = EmphasisStar.Replace(text, "$1");
            text = EmphasisUnderscore.Replace(text, "$1");
            text = Strikethrough.Replace(text, "$1");

            // Entities only now, so that a "&lt;b&gt;" written on purpose is shown rather than taken for a tag.
            text = WebUtility.HtmlDecode(text);

            text = Release(text, code, CodeMark);
            text = Release(text, escaped, EscapeMark);

            return Whitespace.Replace(text, " ").Trim();
        }

        private static void ParseInto(string markdown, List<ReleaseNoteBlock> blocks)
        {
            var lines = HtmlComment.Replace(Normalize(markdown), string.Empty).Split('\n');

            // The block the next line carries on, unless that line starts a block of its own.
            ReleaseNoteBlock open = null;
            // The last list item, which an indented paragraph after a blank line still belongs to.
            ReleaseNoteBlock lastItem = null;
            // The indents of the list items the current one may be nested in.
            var listIndents = new List<int>();

            string fence = null;
            var fenced = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Replace("\t", "    ").TrimEnd();
                var trimmed = line.Trim();

                if (fence != null)
                {
                    if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                    {
                        AddCode(blocks, fenced);
                        fence = null;
                    }
                    else
                    {
                        fenced.Add(line);
                    }

                    continue;
                }

                if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
                {
                    fence = trimmed.Substring(0, 3);
                    fenced.Clear();
                    open = null;
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    open = null;
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;

                // A quote reads the same without its markers.
                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    trimmed = trimmed.TrimStart('>', ' ');
                    if (trimmed.Length == 0)
                    {
                        open = null;
                        continue;
                    }
                }

                // "Title" over a row of "=" or "-" is a heading. A row of dashes anywhere else is only a rule.
                if (SetextUnderline.IsMatch(trimmed) && open != null && open.Kind == ReleaseNoteKind.Paragraph &&
                    blocks.Count > 0 && ReferenceEquals(open, blocks[blocks.Count - 1]))
                {
                    blocks[blocks.Count - 1] = new ReleaseNoteBlock(ReleaseNoteKind.Heading, trimmed[0] == '=' ? 1 : 2, null, open.Text);
                    open = null;
                    lastItem = null;
                    listIndents.Clear();
                    continue;
                }

                if (ThematicBreak.IsMatch(trimmed))
                {
                    open = null;
                    continue;
                }

                var heading = indent < 4 ? AtxHeading.Match(trimmed) : Match.Empty;
                if (heading.Success)
                {
                    blocks.Add(new ReleaseNoteBlock(ReleaseNoteKind.Heading, heading.Groups[1].Length, null,
                                                    CleanInline(heading.Groups[2].Value)));
                    open = null;
                    lastItem = null;
                    listIndents.Clear();
                    continue;
                }

                var bullet = BulletItem.Match(trimmed);
                var numbered = bullet.Success ? Match.Empty : NumberedItem.Match(trimmed);
                if (bullet.Success || numbered.Success)
                {
                    // Nested under every earlier item that is indented less than this one.
                    while (listIndents.Count > 0 && listIndents[listIndents.Count - 1] >= indent)
                    {
                        listIndents.RemoveAt(listIndents.Count - 1);
                    }

                    var depth = Math.Min(listIndents.Count, MaxListDepth);
                    listIndents.Add(indent);

                    var content = bullet.Success ? bullet.Groups[1].Value : numbered.Groups[2].Value;
                    string marker;
                    var task = TaskBox.Match(content);
                    if (task.Success)
                    {
                        marker = task.Groups[1].Value == " " ? OpenTask : DoneTask;
                        content = content.Substring(task.Length);
                    }
                    else if (bullet.Success)
                    {
                        marker = depth % 2 == 0 ? Bullet : NestedBullet;
                    }
                    else
                    {
                        marker = numbered.Groups[1].Value + ".";
                    }

                    var item = new ReleaseNoteBlock(ReleaseNoteKind.Bullet, depth, marker, CleanInline(content));
                    blocks.Add(item);
                    open = item;
                    lastItem = item;
                    continue;
                }

                // A table row stands on its own line with its cells spaced apart; the row of dashes under the
                // header goes.
                if (trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    if (!TableSeparator.IsMatch(trimmed))
                    {
                        var cells = trimmed.Trim('|').Split('|').Select(cell => CleanInline(cell));
                        blocks.Add(new ReleaseNoteBlock(ReleaseNoteKind.Paragraph, 0, null, string.Join("   ", cells)));
                    }

                    open = null;
                    continue;
                }

                if (open != null)
                {
                    open.Append(CleanInline(trimmed));
                    continue;
                }

                // An indented paragraph after a blank line is the last list item going on.
                if (indent >= 2 && lastItem != null)
                {
                    lastItem.Append(CleanInline(trimmed));
                    open = lastItem;
                    continue;
                }

                var paragraph = new ReleaseNoteBlock(ReleaseNoteKind.Paragraph, 0, null, CleanInline(trimmed));
                blocks.Add(paragraph);
                open = paragraph;
                lastItem = null;
                listIndents.Clear();
            }

            // A fence that is never closed runs to the end of the notes, as it does on GitHub.
            if (fence != null)
            {
                AddCode(blocks, fenced);
            }
        }

        private static void AddCode(List<ReleaseNoteBlock> blocks, List<string> lines)
        {
            // The common indent goes, so a block copied from an indented list does not start half-way across.
            var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
            var text = string.Join("\n", lines.Select(l => l.Length >= indent ? l.Substring(indent) : l.TrimStart()));
            blocks.Add(new ReleaseNoteBlock(ReleaseNoteKind.Code, 0, null, text.Trim('\n')));
            lines.Clear();
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string Hold(List<string> store, char mark, string value)
        {
            store.Add(value);
            return mark + (store.Count - 1).ToString(CultureInfo.InvariantCulture) + mark;
        }

        private static string Release(string text, List<string> store, char mark)
        {
            if (store.Count == 0)
            {
                return text;
            }

            var pattern = Regex.Escape(mark.ToString()) + @"(\d+)" + Regex.Escape(mark.ToString());
            return Regex.Replace(text, pattern, m =>
            {
                int index;
                return int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index < store.Count
                    ? store[index]
                    : string.Empty;
            });
        }
    }
}
