using System.Linq;
using NUnit.Framework;
using SS.UIComponent;

namespace SS.UIComponent.Tests
{
    public sealed class MarkdownParserTests
    {
        [Test]
        public void Parses_headings_and_closing_hashes()
        {
            MarkdownDocument document = MarkdownParser.ParseText("# One\n\n### Three ###");
            Assert.AreEqual(2, document.Blocks.Count);
            Assert.AreEqual(MarkdownBlockType.Heading, document.Blocks[0].Type);
            Assert.AreEqual(1, document.Blocks[0].Level);
            Assert.AreEqual("One", document.Blocks[0].PlainText);
            Assert.AreEqual(3, document.Blocks[1].Level);
            Assert.AreEqual("Three", document.Blocks[1].PlainText);
        }

        [Test]
        public void Parses_inline_styles_links_and_code()
        {
            MarkdownDocument document = MarkdownParser.ParseText(
                "A **bold** *italic* ~~gone~~ `code` [link](https://example.com).");
            var spans = document.Blocks[0].Inlines;

            Assert.IsTrue(spans.Any(s => s.Text == "bold" && (s.Style & MarkdownInlineStyle.Bold) != 0));
            Assert.IsTrue(spans.Any(s => s.Text == "italic" && (s.Style & MarkdownInlineStyle.Italic) != 0));
            Assert.IsTrue(spans.Any(s => s.Text == "gone" && (s.Style & MarkdownInlineStyle.Strikethrough) != 0));
            Assert.IsTrue(spans.Any(s => s.Text == "code" && (s.Style & MarkdownInlineStyle.Code) != 0));
            Assert.IsTrue(spans.Any(s => s.Text == "link" && s.Url == "https://example.com"));
        }

        [Test]
        public void Parses_fenced_code_verbatim()
        {
            MarkdownDocument document = MarkdownParser.ParseText(
                "```csharp\nvar x = **not bold**;\nif (a < b) { }\n```");
            Assert.AreEqual(1, document.Blocks.Count);
            Assert.AreEqual(MarkdownBlockType.CodeBlock, document.Blocks[0].Type);
            Assert.AreEqual("csharp", document.Blocks[0].Info);
            StringAssert.Contains("a < b", document.Blocks[0].Code);
            StringAssert.Contains("**not bold**", document.Blocks[0].Code);
        }

        [Test]
        public void Parses_nested_lists()
        {
            MarkdownDocument document = MarkdownParser.ParseText("- One\n- Two\n  - Nested\n\n1. First\n2. Second");
            var items = document.Blocks.Where(b => b.Type == MarkdownBlockType.ListItem).ToList();

            Assert.AreEqual(5, items.Count);
            Assert.AreEqual(0, items[0].ListDepth);
            Assert.AreEqual(1, items[2].ListDepth);
            Assert.IsTrue(items[3].IsOrderedList);
            Assert.AreEqual(1, items[3].ListNumber);
        }

        [Test]
        public void Parses_quotes_and_rules()
        {
            MarkdownDocument document = MarkdownParser.ParseText("> Quote **here**\n\n---\n\nAfter");
            Assert.AreEqual(3, document.Blocks.Count);
            Assert.AreEqual(1, document.Blocks[0].QuoteDepth);
            Assert.AreEqual(MarkdownBlockType.ThematicBreak, document.Blocks[1].Type);
            Assert.AreEqual("After", document.Blocks[2].PlainText);
        }

        [Test]
        public void Resolves_escapes_entities_and_autolinks()
        {
            MarkdownDocument document = MarkdownParser.ParseText(
                @"\*literal\* &amp; &lt;tag&gt; <https://unity.com>");
            Assert.AreEqual("*literal* & <tag> https://unity.com", document.Blocks[0].PlainText);
            Assert.IsTrue(document.Blocks[0].Inlines.Any(s => s.Url == "https://unity.com"));
        }

        [Test]
        public void Handles_hard_breaks()
        {
            MarkdownDocument document = MarkdownParser.ParseText("one  \ntwo\\\nthree");
            Assert.AreEqual("one\ntwo\nthree", document.Blocks[0].PlainText);
        }
    }
}
