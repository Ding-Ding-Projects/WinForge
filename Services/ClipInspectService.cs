using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace WinForge.Services;

/// <summary>
/// 剪貼簿格式檢查器 · Clipboard format inspector — reads the clipboard on demand and reports the
/// formats it exposes. Pure managed (Windows.ApplicationModel.DataTransfer.Clipboard); read-only,
/// never writes the clipboard, never throws (all access is wrapped and clipboard reads can throw).
/// </summary>
public static class ClipInspectService
{
    /// <summary>One row shown in the formats list.</summary>
    public sealed class FormatRow
    {
        public string Name { get; init; } = "";
    }

    /// <summary>Result of a single on-demand clipboard read.</summary>
    public sealed class InspectResult
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public List<FormatRow> Formats { get; init; } = new();

        public bool HasText { get; init; }
        public bool HasHtml { get; init; }
        public bool HasRtf { get; init; }
        public bool HasBitmap { get; init; }
        public bool HasStorageItems { get; init; }
        public bool HasWebLink { get; init; }
        public bool HasApplicationLink { get; init; }

        public string? TextPreview { get; init; }
        public int HtmlLength { get; init; }
        public int StorageItemCount { get; init; }
    }

    /// <summary>
    /// Read the clipboard once and describe what it holds. Fully guarded — returns an
    /// <see cref="InspectResult"/> with <c>Ok = false</c> and a message on any failure.
    /// </summary>
    public static async Task<InspectResult> ReadAsync()
    {
        DataPackageView view;
        try
        {
            view = Clipboard.GetContent();
        }
        catch (Exception ex)
        {
            return new InspectResult { Ok = false, Error = ex.Message };
        }

        var formats = new List<FormatRow>();
        try
        {
            foreach (var f in view.AvailableFormats)
                formats.Add(new FormatRow { Name = f });
        }
        catch { /* enumerating formats can throw; keep whatever we gathered */ }

        bool hasText = Contains(view, StandardDataFormats.Text);
        bool hasHtml = Contains(view, StandardDataFormats.Html);
        bool hasRtf = Contains(view, StandardDataFormats.Rtf);
        bool hasBitmap = Contains(view, StandardDataFormats.Bitmap);
        bool hasStorage = Contains(view, StandardDataFormats.StorageItems);
        bool hasWebLink = Contains(view, StandardDataFormats.WebLink);
        bool hasAppLink = Contains(view, StandardDataFormats.ApplicationLink);

        string? textPreview = null;
        if (hasText)
        {
            try
            {
                string text = await view.GetTextAsync();
                if (text is not null)
                {
                    text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                    textPreview = text.Length > 400 ? text.Substring(0, 400) + "…" : text;
                }
            }
            catch { /* text read can throw despite the flag */ }
        }

        int htmlLen = 0;
        if (hasHtml)
        {
            try
            {
                string html = await view.GetHtmlFormatAsync();
                htmlLen = html?.Length ?? 0;
            }
            catch { /* ignore */ }
        }

        int storageCount = 0;
        if (hasStorage)
        {
            try
            {
                var items = await view.GetStorageItemsAsync();
                storageCount = items?.Count ?? 0;
            }
            catch { /* ignore */ }
        }

        return new InspectResult
        {
            Ok = true,
            Formats = formats,
            HasText = hasText,
            HasHtml = hasHtml,
            HasRtf = hasRtf,
            HasBitmap = hasBitmap,
            HasStorageItems = hasStorage,
            HasWebLink = hasWebLink,
            HasApplicationLink = hasAppLink,
            TextPreview = textPreview,
            HtmlLength = htmlLen,
            StorageItemCount = storageCount,
        };
    }

    private static bool Contains(DataPackageView view, string format)
    {
        try { return view.Contains(format); }
        catch { return false; }
    }
}
