using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SS.UIComponent;

internal static class Program
{
    private static int s_Passed;
    private static int s_Failed;

    private static int Main()
    {
        TestHeadings();
        TestParagraphAndInlineStyles();
        TestLinksAndImages();
        TestCodeBlocks();
        TestLists();
        TestQuotes();
        TestRulesAndSetext();
        TestEscapesAndEntities();
        TestHardBreaks();
        TestAutolinks();
        TestNestedFormatting();
        TestTables();

        Console.WriteLine();
        Console.WriteLine("Passed: " + s_Passed + "  Failed: " + s_Failed);
        return s_Failed == 0 ? 0 : 1;
    }

    private static void TestHeadings()
    {
        var doc = MarkdownParser.ParseText("# Title\n\n### Third ###\n\nBody");
        AssertEqual(3, doc.Blocks.Count, "heading block count");
        AssertEqual(MarkdownBlockType.Heading, doc.Blocks[0].Type, "h1 type");
        AssertEqual(1, doc.Blocks[0].Level, "h1 level");
        AssertEqual("Title", doc.Blocks[0].PlainText, "h1 text");
        AssertEqual(3, doc.Blocks[1].Level, "h3 level");
        AssertEqual("Third", doc.Blocks[1].PlainText, "closing hashes stripped");
        AssertEqual(MarkdownBlockType.Paragraph, doc.Blocks[2].Type, "body type");
    }

    private static void TestParagraphAndInlineStyles()
    {
        var doc = MarkdownParser.ParseText("Hello **bold** and *italic* with `code` and ~~gone~~.");
        AssertEqual(1, doc.Blocks.Count, "inline block count");
        var spans = doc.Blocks[0].Inlines;
        AssertTrue(spans.Any(s => s.Text == "bold" && s.Style == MarkdownInlineStyle.Bold), "bold span");
        AssertTrue(spans.Any(s => s.Text == "italic" && s.Style == MarkdownInlineStyle.Italic), "italic span");
        AssertTrue(spans.Any(s => s.Text == "code" && s.Style == MarkdownInlineStyle.Code), "code span");
        AssertTrue(spans.Any(s => s.Text == "gone" && s.Style == MarkdownInlineStyle.Strikethrough), "strike span");
        AssertEqual("Hello bold and italic with code and gone.", doc.Blocks[0].PlainText, "inline plain text");
    }

    private static void TestLinksAndImages()
    {
        var doc = MarkdownParser.ParseText("See [the docs](https://example.com/a?b=1 \"title\") and ![alt text](image.png).");
        var spans = doc.Blocks[0].Inlines;
        var link = spans.FirstOrDefault(s => s.Text == "the docs");
        AssertTrue(link != null, "link span exists");
        AssertEqual("https://example.com/a?b=1", link.Url, "link destination");
        AssertTrue((link.Style & MarkdownInlineStyle.Link) != 0, "link style");

        var image = doc.Blocks.FirstOrDefault(b => b.Type == MarkdownBlockType.Image);
        AssertTrue(image != null, "image block exists");
        AssertEqual("alt text", image.ImageAlt, "image alt text exists");
        AssertEqual("image.png", image.ImageUrl, "image url");
    }

    private static void TestCodeBlocks()
    {
        string md = "```csharp\nvar x = **not bold**;\nDebug.Log(x);\n```\n\nAfter";
        var doc = MarkdownParser.ParseText(md);
        AssertEqual(2, doc.Blocks.Count, "code block count");
        AssertEqual(MarkdownBlockType.CodeBlock, doc.Blocks[0].Type, "code type");
        AssertEqual("csharp", doc.Blocks[0].Info, "code info");
        AssertEqual("var x = **not bold**;\nDebug.Log(x);", doc.Blocks[0].Code, "code preserved");
        AssertEqual("After", doc.Blocks[1].PlainText, "after code");
    }

    private static void TestLists()
    {
        string md = "- One\n- Two **bold**\n  - Nested\n\n1. First\n2. Second";
        var doc = MarkdownParser.ParseText(md);
        var lists = doc.Blocks.Where(b => b.Type == MarkdownBlockType.ListItem).ToList();
        AssertEqual(5, lists.Count, "list item count");
        AssertEqual(0, lists[0].ListDepth, "first depth");
        AssertEqual(0, lists[1].ListDepth, "second depth");
        AssertEqual(1, lists[2].ListDepth, "nested depth");
        AssertEqual("Nested", lists[2].PlainText, "nested text");
        AssertTrue(lists[3].IsOrderedList, "ordered flag");
        AssertEqual(1, lists[3].ListNumber, "ordered number");
    }

    private static void TestQuotes()
    {
        var doc = MarkdownParser.ParseText("> Quote **here**\n>\n> Second line\n\nAfter");
        AssertEqual(3, doc.Blocks.Count, "quote block count");
        AssertEqual(1, doc.Blocks[0].QuoteDepth, "quote depth");
        AssertEqual("Quote here", doc.Blocks[0].PlainText, "quote text");
        AssertEqual("Second line", doc.Blocks[1].PlainText, "quote second paragraph");
        AssertEqual(1, doc.Blocks[1].QuoteDepth, "second quote depth");
    }

    private static void TestRulesAndSetext()
    {
        var doc = MarkdownParser.ParseText("Title\n=====\n\n---\n\nSub\n---");
        AssertEqual(3, doc.Blocks.Count, "setext/rule count");
        AssertEqual(MarkdownBlockType.Heading, doc.Blocks[0].Type, "setext type");
        AssertEqual(1, doc.Blocks[0].Level, "setext level");
        AssertEqual(MarkdownBlockType.ThematicBreak, doc.Blocks[1].Type, "rule type");
        AssertEqual(2, doc.Blocks[2].Level, "setext h2");
    }

    private static void TestEscapesAndEntities()
    {
        var doc = MarkdownParser.ParseText(@"\*not italic\* &amp; &lt;tag&gt;");
        AssertEqual(@"*not italic* & <tag>", doc.Blocks[0].PlainText, "escapes and entities");
        AssertTrue(doc.Blocks[0].Inlines.All(s => (s.Style & MarkdownInlineStyle.Italic) == 0), "escaped italic");
    }

    private static void TestHardBreaks()
    {
        var doc = MarkdownParser.ParseText("line one  \nline two\\\nline three");
        AssertEqual("line one\nline two\nline three", doc.Blocks[0].PlainText, "hard breaks");
    }

    private static void TestAutolinks()
    {
        var doc = MarkdownParser.ParseText("Visit <https://example.com> or https://example.org/x.");
        var links = doc.Blocks[0].Inlines.Where(s => (s.Style & MarkdownInlineStyle.Link) != 0).ToList();
        AssertEqual(2, links.Count, "autolink count");
        AssertEqual("https://example.com", links[0].Url, "angle autolink");
        AssertEqual("https://example.org/x", links[1].Url, "bare autolink trailing dot");
    }

    private static void TestNestedFormatting()
    {
        var doc = MarkdownParser.ParseText("***both*** and **bold *and italic***");
        var spans = doc.Blocks[0].Inlines;
        AssertTrue(spans.Any(s => s.Text == "both" && s.Style == (MarkdownInlineStyle.Bold | MarkdownInlineStyle.Italic)), "triple style");
        AssertTrue(spans.Any(s => s.Text == "and italic" && s.Style == (MarkdownInlineStyle.Bold | MarkdownInlineStyle.Italic)), "nested style");
    }

    private static void TestTables()
    {
        var doc = MarkdownParser.ParseText("| Name | Score |\n| :--- | ---: |\n| **Alice** | 10 |\n| Bob | `20` |\n\nAfter");
        AssertEqual(2, doc.Blocks.Count, "table block count");
        AssertEqual(MarkdownBlockType.Table, doc.Blocks[0].Type, "table block type");
        AssertEqual(2, doc.Blocks[0].Table.ColumnCount, "table column count");
        AssertEqual(2, doc.Blocks[0].Table.Rows.Count, "table row count");
        AssertEqual("Name", doc.Blocks[0].Table.Header[0].PlainText, "table header");
        AssertEqual(MarkdownTableAlignment.Left, doc.Blocks[0].Table.Alignments[0], "table left alignment");
        AssertEqual(MarkdownTableAlignment.Right, doc.Blocks[0].Table.Alignments[1], "table right alignment");
        AssertTrue(doc.Blocks[0].Table.Rows[0][0].Inlines.Any(s => (s.Style & MarkdownInlineStyle.Bold) != 0), "table bold cell");
        AssertEqual("After", doc.Blocks[1].PlainText, "table trailing paragraph");

        var escaped = MarkdownParser.ParseText("| A | B |\n| --- | --- |\n| a \\| b | `x|y` |");
        AssertEqual(1, escaped.Blocks.Count, "escaped table block count");
        AssertEqual("a | b", escaped.Blocks[0].Table.Rows[0][0].PlainText, "escaped pipe");
        AssertEqual("x|y", escaped.Blocks[0].Table.Rows[0][1].PlainText, "pipe in code span");
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (condition)
        {
            s_Passed++;
        }
        else
        {
            s_Failed++;
            Console.WriteLine("FAIL: " + name);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            s_Passed++;
        }
        else
        {
            s_Failed++;
            Console.WriteLine("FAIL: " + name + "\n  expected: " + Format(expected) + "\n  actual:   " + Format(actual));
        }
    }

    private static string Format<T>(T value)
    {
        if (value is string s)
            return "\"" + s.Replace("\n", "\\n") + "\"";
        return value == null ? "<null>" : value.ToString();
    }
}
