using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace WinForge.Services;

/// <summary>
/// XPath 測試器 · XPath tester over an XML document — pure managed (System.Xml.Linq /
/// System.Xml.XPath.Extensions). Parses the XML with <see cref="XDocument"/> and evaluates the
/// expression with <see cref="System.Xml.XPath.Extensions.XPathEvaluate(XNode,string)"/>. Handles
/// node-sets as well as string / number / boolean results. Never throws — every failure comes back
/// as a bilingual error on <see cref="XPathResult"/>.
/// </summary>
public static class XPathTesterService
{
    /// <summary>One row shown in the results ListView.</summary>
    public sealed class XPathMatch
    {
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
        public string Outer { get; init; } = "";
    }

    /// <summary>Outcome of one evaluation. <see cref="Ok"/> is false when parsing/eval failed.</summary>
    public sealed class XPathResult
    {
        public bool Ok { get; init; }
        public string? ErrorEn { get; init; }
        public string? ErrorZh { get; init; }
        public string? Scalar { get; init; }        // non-null when the XPath returned a string/number/bool
        public List<XPathMatch> Matches { get; } = new();
        public int Count { get; init; }
    }

    /// <summary>
    /// Parse <paramref name="xml"/> and evaluate <paramref name="xpath"/> against it. Blank inputs
    /// return a benign empty result (Ok = true, no matches) so the UI can stay quiet.
    /// </summary>
    public static XPathResult Evaluate(string? xml, string? xpath)
    {
        xml ??= "";
        xpath ??= "";

        if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(xpath))
            return new XPathResult { Ok = true, Count = 0 };

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            return new XPathResult
            {
                Ok = false,
                ErrorEn = $"XML parse error: {ex.Message}",
                ErrorZh = $"XML 解析錯誤：{ex.Message}",
            };
        }
        catch (Exception ex)
        {
            return new XPathResult
            {
                Ok = false,
                ErrorEn = $"Could not read the XML: {ex.Message}",
                ErrorZh = $"讀唔到 XML：{ex.Message}",
            };
        }

        object evaluated;
        try
        {
            evaluated = doc.XPathEvaluate(xpath);
        }
        catch (XPathException ex)
        {
            return new XPathResult
            {
                Ok = false,
                ErrorEn = $"XPath error: {ex.Message}",
                ErrorZh = $"XPath 表達式錯誤：{ex.Message}",
            };
        }
        catch (Exception ex)
        {
            return new XPathResult
            {
                Ok = false,
                ErrorEn = $"Could not evaluate the XPath: {ex.Message}",
                ErrorZh = $"評估 XPath 失敗：{ex.Message}",
            };
        }

        // A node-set comes back as IEnumerable; scalars come back as string/double/bool.
        if (evaluated is IEnumerable<object> nodes)
        {
            var result = new XPathResult { Ok = true };
            int n = 0;
            try
            {
                foreach (var item in nodes)
                {
                    n++;
                    result.Matches.Add(Describe(item));
                }
            }
            catch (Exception ex)
            {
                return new XPathResult
                {
                    Ok = false,
                    ErrorEn = $"Could not read the matches: {ex.Message}",
                    ErrorZh = $"讀取匹配結果失敗：{ex.Message}",
                };
            }

            return new XPathResult { Ok = true, Count = n }.WithMatches(result.Matches);
        }

        // Scalar result (string() / count() / boolean() etc.)
        string scalar = evaluated switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString("0.############", CultureInfo.InvariantCulture),
            null => "",
            _ => evaluated.ToString() ?? "",
        };

        return new XPathResult { Ok = true, Scalar = scalar, Count = 1 };
    }

    private static XPathResult WithMatches(this XPathResult result, List<XPathMatch> matches)
    {
        result.Matches.AddRange(matches);
        return result;
    }

    private static XPathMatch Describe(object item)
    {
        switch (item)
        {
            case XElement el:
                return new XPathMatch
                {
                    Name = el.Name.LocalName,
                    Value = Trim(el.Value),
                    Outer = Trim(el.ToString(SaveOptions.DisableFormatting)),
                };
            case XAttribute at:
                return new XPathMatch
                {
                    Name = "@" + at.Name.LocalName,
                    Value = Trim(at.Value),
                    Outer = Trim($"{at.Name.LocalName}=\"{at.Value}\""),
                };
            case XText tx:
                return new XPathMatch { Name = "#text", Value = Trim(tx.Value), Outer = Trim(tx.Value) };
            case XComment cm:
                return new XPathMatch { Name = "#comment", Value = Trim(cm.Value), Outer = Trim(cm.ToString()) };
            case XProcessingInstruction pi:
                return new XPathMatch { Name = "#pi", Value = Trim(pi.Data), Outer = Trim(pi.ToString()) };
            case XObject xo:
                return new XPathMatch { Name = xo.NodeType.ToString(), Value = Trim(xo.ToString() ?? ""), Outer = Trim(xo.ToString() ?? "") };
            default:
                string s = item?.ToString() ?? "";
                return new XPathMatch { Name = "value", Value = Trim(s), Outer = Trim(s) };
        }
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        const int max = 4000;
        return s.Length > max ? s.Substring(0, max) + "…" : s;
    }
}
