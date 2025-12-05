using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HackathonApp.ConsoleHelpers
{
    /// <summary>
    /// ConsoleTablePrinter - ASCII table renderer with optional colored borders and headers.
    /// Usage: ConsoleTablePrinter.PrintTable(rows, headers, selector);
    /// </summary>
    public static class ConsoleTablePrinter
    {
        /// <summary>
        /// Print a table to the console with defaults for colors.
        /// </summary>
        public static void PrintTable<T>(
            IEnumerable<T> rows,
            string[] headers,
            Func<T, object?[]> rowSelector,
            int maxRowsToShow = 20)
        {
            if (headers == null) headers = Array.Empty<string>();
            PrintTable(rows, headers, rowSelector, maxRowsToShow, ConsoleColor.Cyan, ConsoleColor.Yellow, null);
        }

        /// <summary>
        /// Print a table with explicit color choices.
        /// borderColor/headerColor/rowColor are optional (pass null to keep the console default for that part).
        /// </summary>
        public static void PrintTable<T>(
            IEnumerable<T> rows,
            string[] headers,
            Func<T, object?[]> rowSelector,
            int maxRowsToShow,
            ConsoleColor? borderColor,
            ConsoleColor? headerColor,
            ConsoleColor? rowColor)
        {
            if (headers == null) headers = Array.Empty<string>();
            if (rowSelector == null) throw new ArgumentNullException(nameof(rowSelector));
            if (maxRowsToShow <= 0) maxRowsToShow = 1;

            var list = rows?.ToList() ?? new List<T>();

            if (!list.Any())
            {
                WriteColoredLine("No results.", borderColor ?? Console.ForegroundColor);
                return;
            }

            // Prepare table rows (apply selector)
            var tableRows = list.Select(r => rowSelector(r).Select(o => o?.ToString() ?? string.Empty).ToArray()).ToList();

            // Compute number of columns: use headers length if provided, otherwise infer from first row
            int cols = Math.Max(headers.Length, tableRows.FirstOrDefault()?.Length ?? 0);
            if (cols == 0)
            {
                WriteColoredLine("No columns to display.", borderColor ?? Console.ForegroundColor);
                return;
            }

            // Ensure headers array is same length as cols (fill empty headers if needed)
            if (headers.Length < cols)
            {
                var extended = new string[cols];
                for (int i = 0; i < cols; i++)
                    extended[i] = i < headers.Length ? headers[i] ?? string.Empty : string.Empty;
                headers = extended;
            }

            var colWidths = new int[cols];

            for (int c = 0; c < cols; c++)
            {
                int headerLen = headers[c]?.Length ?? 0;
                colWidths[c] = headerLen;
            }

            for (int r = 0; r < tableRows.Count; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var cell = c < tableRows[r].Length ? tableRows[r][c] : string.Empty;
                    colWidths[c] = Math.Max(colWidths[c], (cell ?? string.Empty).Length);
                }
            }

            // Cap extremely long columns for readability
            const int maxColWidth = 60;
            for (int i = 0; i < colWidths.Length; i++)
            {
                if (colWidths[i] > maxColWidth) colWidths[i] = maxColWidth;
            }

            string Separator()
            {
                return "+" + string.Join("+", colWidths.Select(w => new string('-', w + 2))) + "+";
            }

            string FormatRow(params string[] cells)
            {
                var sb = new StringBuilder();
                sb.Append("|");
                for (int i = 0; i < colWidths.Length; i++)
                {
                    var raw = i < cells.Length ? (cells[i] ?? string.Empty) : string.Empty;
                    var text = TruncateSafe(raw, colWidths[i]);
                    sb.Append(' ');
                    sb.Append(text.PadRight(colWidths[i]));
                    sb.Append(" |");
                }
                return sb.ToString();
            }

            // Save original color
            var originalColor = Console.ForegroundColor;

            // Print header separator (colored)
            WriteColoredLine(Separator(), borderColor ?? originalColor);

            // Print header row (header color for content, but keep border colored)
            var headerLine = FormatRow(headers);
            PrintLineWithBorderAndContent(headerLine, headerColor, borderColor, originalColor);

            // Print separator
            WriteColoredLine(Separator(), borderColor ?? originalColor);

            // Print rows
            int rowsToShow = Math.Min(maxRowsToShow, tableRows.Count);
            for (int r = 0; r < rowsToShow; r++)
            {
                var rowLine = FormatRow(tableRows[r]);
                PrintLineWithBorderAndContent(rowLine, rowColor, borderColor, originalColor);
            }

            // Footer separator
            WriteColoredLine(Separator(), borderColor ?? originalColor);

            // Notice if truncated
            if (tableRows.Count > rowsToShow)
            {
                WriteColoredLine($"(Showing {rowsToShow} of {tableRows.Count} rows — increase maxRowsToShow to see more.)", borderColor ?? originalColor);
            }

            // Restore original color explicitly
            Console.ForegroundColor = originalColor;
        }

        static void PrintLineWithBorderAndContent(string fullLine, ConsoleColor? contentColor, ConsoleColor? borderColor, ConsoleColor originalColor)
        {
            // Split the fullLine into pipe-separated cells, then print with colored borders and content.
            var parts = SplitLineByPipes(fullLine);

            // Print leading pipe (border)
            if (borderColor.HasValue) Console.ForegroundColor = borderColor.Value;
            Console.Write("|");
            Console.ForegroundColor = contentColor ?? originalColor;

            for (int i = 0; i < parts.Length; i++)
            {
                var cell = parts[i];
                Console.Write(cell);
                if (i < parts.Length - 1)
                {
                    if (borderColor.HasValue) Console.ForegroundColor = borderColor.Value;
                    Console.Write(" |");
                    Console.ForegroundColor = contentColor ?? originalColor;
                }
            }
            Console.WriteLine();
        }

        static string[] SplitLineByPipes(string line)
        {
            // Robust simple splitter: remove outer '|' chars if present, then split on '|'
            if (string.IsNullOrEmpty(line)) return Array.Empty<string>();
            var trimmed = line;
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            var parts = trimmed.Split('|').Select(p => p.TrimStart()).ToArray();
            return parts;
        }

        static void WriteColoredLine(string text, ConsoleColor color)
        {
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = orig;
        }

        // single Truncate implementation used by this class (null-safe)
        static string TruncateSafe(string? value, int maxLength)
        {
            value ??= string.Empty;
            if (value.Length <= maxLength) return value;
            if (maxLength <= 3) return value.Substring(0, maxLength);
            return value.Substring(0, maxLength - 3) + "...";
        }
    }
}
