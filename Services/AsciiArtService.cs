using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// ASCII 橫幅產生器 · ASCII banner generator. Pure managed, never-throw. Embeds a fixed 5-row
/// block font (A–Z, 0–9, space and a few punctuation marks) as string[5] glyphs and composes a
/// banner row-by-row. Two styles: "Block" (solid '#') and "Outline" (thin frame). No redirect.
/// </summary>
public static class AsciiArtService
{
    /// <summary>Height (rows) of every glyph in the embedded font.</summary>
    public const int Height = 5;

    public enum Style { Block, Outline }

    // Each glyph is exactly 5 rows. '#' marks filled cells; spaces are blank. Widths vary per glyph.
    private static readonly Dictionary<char, string[]> Font = new()
    {
        [' '] = new[] { "   ", "   ", "   ", "   ", "   " },
        ['A'] = new[] { " ### ", "#   #", "#####", "#   #", "#   #" },
        ['B'] = new[] { "#### ", "#   #", "#### ", "#   #", "#### " },
        ['C'] = new[] { " ####", "#    ", "#    ", "#    ", " ####" },
        ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#### " },
        ['E'] = new[] { "#####", "#    ", "#### ", "#    ", "#####" },
        ['F'] = new[] { "#####", "#    ", "#### ", "#    ", "#    " },
        ['G'] = new[] { " ####", "#    ", "#  ##", "#   #", " ####" },
        ['H'] = new[] { "#   #", "#   #", "#####", "#   #", "#   #" },
        ['I'] = new[] { "###", " # ", " # ", " # ", "###" },
        ['J'] = new[] { "  ###", "   # ", "   # ", "#  # ", " ##  " },
        ['K'] = new[] { "#   #", "#  # ", "###  ", "#  # ", "#   #" },
        ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#####" },
        ['M'] = new[] { "#   #", "## ##", "# # #", "#   #", "#   #" },
        ['N'] = new[] { "#   #", "##  #", "# # #", "#  ##", "#   #" },
        ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", " ### " },
        ['P'] = new[] { "#### ", "#   #", "#### ", "#    ", "#    " },
        ['Q'] = new[] { " ### ", "#   #", "# # #", "#  # ", " ## #" },
        ['R'] = new[] { "#### ", "#   #", "#### ", "#  # ", "#   #" },
        ['S'] = new[] { " ####", "#    ", " ### ", "    #", "#### " },
        ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  " },
        ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", " ### " },
        ['V'] = new[] { "#   #", "#   #", "#   #", " # # ", "  #  " },
        ['W'] = new[] { "#   #", "#   #", "# # #", "## ##", "#   #" },
        ['X'] = new[] { "#   #", " # # ", "  #  ", " # # ", "#   #" },
        ['Y'] = new[] { "#   #", " # # ", "  #  ", "  #  ", "  #  " },
        ['Z'] = new[] { "#####", "   # ", "  #  ", " #   ", "#####" },
        ['0'] = new[] { " ### ", "#  ##", "# # #", "##  #", " ### " },
        ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", " ### " },
        ['2'] = new[] { " ### ", "#   #", "  ## ", " #   ", "#####" },
        ['3'] = new[] { "#### ", "    #", " ### ", "    #", "#### " },
        ['4'] = new[] { "#  # ", "#  # ", "#####", "   # ", "   # " },
        ['5'] = new[] { "#####", "#    ", "#### ", "    #", "#### " },
        ['6'] = new[] { " ### ", "#    ", "#### ", "#   #", " ### " },
        ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   " },
        ['8'] = new[] { " ### ", "#   #", " ### ", "#   #", " ### " },
        ['9'] = new[] { " ### ", "#   #", " ####", "    #", " ### " },
        ['.'] = new[] { "  ", "  ", "  ", "  ", "##" },
        [','] = new[] { "  ", "  ", "  ", "##", " #" },
        ['!'] = new[] { "#", "#", "#", " ", "#" },
        ['?'] = new[] { "### ", "   #", " ## ", "    ", " #  " },
        ['-'] = new[] { "    ", "    ", "####", "    ", "    " },
        ['+'] = new[] { "   ", " # ", "###", " # ", "   " },
        ['='] = new[] { "    ", "####", "    ", "####", "    " },
        ['*'] = new[] { "     ", "# # #", " ### ", "# # #", "     " },
        ['/'] = new[] { "    #", "   # ", "  #  ", " #   ", "#    " },
        [':'] = new[] { "  ", "##", "  ", "##", "  " },
        ['\''] = new[] { "#", "#", " ", " ", " " },
        ['('] = new[] { " #", "# ", "# ", "# ", " #" },
        [')'] = new[] { "# ", " #", " #", " #", "# " },
        ['@'] = new[] { " ### ", "#   #", "# ###", "#    ", " ####" },
        ['#'] = new[] { " # # ", "#####", " # # ", "#####", " # # " },
    };

    /// <summary>1-space gap columns rendered between glyphs.</summary>
    private const int Gap = 1;

    /// <summary>
    /// Render <paramref name="input"/> as a 5-row ASCII banner. Uppercases input; unknown chars
    /// become blank space. Returns "" for null/empty/whitespace-only input. Never throws.
    /// </summary>
    public static string Render(string? input, Style style)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string text = input.ToUpperInvariant();
            var rows = new StringBuilder[Height];
            for (int r = 0; r < Height; r++) rows[r] = new StringBuilder();

            bool first = true;
            foreach (char raw in text)
            {
                char c = raw;
                if (!Font.TryGetValue(c, out var glyph))
                {
                    // Unknown → blank glyph sized like a space.
                    glyph = Font[' '];
                }

                if (!first)
                    for (int r = 0; r < Height; r++) rows[r].Append(new string(' ', Gap));
                first = false;

                for (int r = 0; r < Height; r++) rows[r].Append(glyph[r]);
            }

            var sb = new StringBuilder();
            for (int r = 0; r < Height; r++)
            {
                string line = rows[r].ToString().TrimEnd();
                sb.Append(line);
                if (r < Height - 1) sb.Append('\n');
            }

            string block = sb.ToString();
            return style == Style.Outline ? ToOutline(block) : block;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Convert a solid '#' banner into a thin outline: a filled cell keeps a glyph only when it
    /// borders an empty cell (or the banner edge); interior filled cells become spaces.
    /// </summary>
    private static string ToOutline(string block)
    {
        try
        {
            string[] lines = block.Split('\n');
            int rows = lines.Length;
            int cols = 0;
            foreach (var l in lines) cols = Math.Max(cols, l.Length);

            char[][] grid = new char[rows][];
            for (int r = 0; r < rows; r++)
            {
                grid[r] = new char[cols];
                for (int c = 0; c < cols; c++)
                    grid[r][c] = c < lines[r].Length ? lines[r][c] : ' ';
            }

            bool Filled(int r, int c) =>
                r >= 0 && r < rows && c >= 0 && c < cols && grid[r][c] == '#';

            var sb = new StringBuilder();
            for (int r = 0; r < rows; r++)
            {
                var line = new StringBuilder();
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r][c] != '#') { line.Append(' '); continue; }

                    bool edge = !Filled(r - 1, c) || !Filled(r + 1, c)
                             || !Filled(r, c - 1) || !Filled(r, c + 1);
                    line.Append(edge ? 'o' : ' ');
                }
                sb.Append(line.ToString().TrimEnd());
                if (r < rows - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
        catch
        {
            return block;
        }
    }
}
