using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AniVault.Utilities;

/// <summary>
/// A deliberately small Markdown → <see cref="FlowDocument"/> renderer for the bundled rating
/// guide. Supports ATX headings (<c>#</c>…<c>###</c>), <c>**bold**</c>, <c>- </c> bullets,
/// <c>|</c> tables, <c>---</c> rules and blank-line-separated paragraphs. Not a general parser.
/// Colours come from the current theme via resource references, so it follows light/dark.
/// </summary>
public static class MarkdownFlow
{
    public static FlowDocument Build(string markdown)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, sans-serif"),
            FontSize = 13,
            PagePadding = new Thickness(0),
            LineHeight = 20,
        };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Brush.TextPrimary");

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        var tableRows = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
            AddInlines(p.Inlines, string.Join(" ", paragraph));
            doc.Blocks.Add(p);
            paragraph.Clear();
        }

        void FlushTable()
        {
            if (tableRows.Count == 0)
            {
                return;
            }

            var rows = tableRows
                .Select(r => r.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray())
                .Where(cells => !cells.All(c => c.Length == 0 || c.All(ch => ch is '-' or ':' or ' ')))
                .ToList();

            if (rows.Count > 0)
            {
                var columns = rows.Max(r => r.Length);
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
                for (var i = 0; i < columns; i++)
                {
                    table.Columns.Add(new TableColumn());
                }

                var group = new TableRowGroup();
                for (var r = 0; r < rows.Count; r++)
                {
                    var row = new TableRow();
                    foreach (var cell in rows[r])
                    {
                        var para = new Paragraph { Margin = new Thickness(4, 2, 4, 2) };
                        AddInlines(para.Inlines, cell);
                        if (r == 0)
                        {
                            para.FontWeight = FontWeights.SemiBold;
                        }

                        row.Cells.Add(new TableCell(para) { BorderThickness = new Thickness(0, 0, 0, r == 0 ? 1 : 0) });
                    }

                    group.Rows.Add(row);
                }

                table.RowGroups.Add(group);
                doc.Blocks.Add(table);
            }

            tableRows.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (line.StartsWith('|'))
            {
                FlushParagraph();
                tableRows.Add(line);
                continue;
            }

            FlushTable();

            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (line is "---" or "***" or "___")
            {
                FlushParagraph();
                doc.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Separator { Margin = new Thickness(0, 4, 0, 12) }));
                continue;
            }

            if (line.StartsWith('#'))
            {
                FlushParagraph();
                var level = line.TakeWhile(c => c == '#').Count();
                var text = line[level..].Trim();
                var heading = new Paragraph
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = level <= 1 ? 18 : level == 2 ? 15.5 : 13.5,
                    Margin = new Thickness(0, level <= 2 ? 14 : 10, 0, 6),
                };
                AddInlines(heading.Inlines, text);
                doc.Blocks.Add(heading);
                continue;
            }

            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                FlushParagraph();
                var item = new Paragraph { Margin = new Thickness(14, 0, 0, 4), TextIndent = -10 };
                item.Inlines.Add(new Run("• "));
                AddInlines(item.Inlines, line[2..].Trim());
                doc.Blocks.Add(item);
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph();
        FlushTable();
        return doc;
    }

    private static void AddInlines(InlineCollection target, string text)
    {
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            var run = new Run(parts[i]);
            if (i % 2 == 1)
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            target.Add(run);
        }
    }
}
