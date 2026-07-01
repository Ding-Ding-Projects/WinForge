using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 短碼編碼器 · Short ID encoder — convert a non-negative integer to/from a compact base-N
/// code (Base62 / Base58 / Base36 / Crockford Base32), and mint NanoID-style random IDs via a
/// cryptographic RNG. Pure managed, never throws, bilingual. Mirrors the Awake module shell.
/// </summary>
public sealed partial class ShortIdModule : Page
{
    private bool _suppress;

    public ShortIdModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildAlphabetLists();
        Render();
        RunConvert();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildAlphabetLists()
    {
        _suppress = true;
        try
        {
            if (AlphabetCombo.Items.Count == 0)
            {
                AddItem(AlphabetCombo, "Base62", "Base62 · [0-9A-Za-z]");
                AddItem(AlphabetCombo, "Base58", "Base58 · Bitcoin");
                AddItem(AlphabetCombo, "Base36", "Base36 · [0-9a-z]");
                AddItem(AlphabetCombo, "Crockford32", "Crockford Base32");
                AlphabetCombo.SelectedIndex = 0;
            }
            if (RandomAlphabetCombo.Items.Count == 0)
            {
                AddItem(RandomAlphabetCombo, "UrlSafe", "URL-safe");
                AddItem(RandomAlphabetCombo, "Base62", "Base62");
                AddItem(RandomAlphabetCombo, "Base58", "Base58");
                RandomAlphabetCombo.SelectedIndex = 0;
            }
        }
        catch { /* never throw during UI build */ }
        finally { _suppress = false; }
    }

    private static void AddItem(ComboBox combo, string tag, string label)
    {
        combo.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
    }

    private static string TagOf(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Base62";

    private void Render()
    {
        try
        {
            Header.Title = "Short ID Encoder · 短碼編碼器";
            HeaderBlurb.Text = P(
                "Turn a big number into a short, URL-friendly code — or the reverse — and mint NanoID-style random IDs. All done locally with unbiased cryptographic randomness.",
                "將一個大數字變成又短又啱擺入網址嘅代碼（或者倒返轉），仲可以造 NanoID 式嘅隨機 ID。全部喺本機用無偏差嘅加密隨機數搞掂。");

            ConvertTitle.Text = P("Number ↔ short code", "數字 ↔ 短碼");
            AlphabetLabel.Text = P("Alphabet", "字母表");
            EncodeLabel.Text = P("Encode: number → code", "編碼：數字 → 代碼");
            DecodeLabel.Text = P("Decode: code → number", "解碼：代碼 → 數字");
            NumberBox.PlaceholderText = P("Enter a non-negative integer, e.g. 1234567890", "輸入一個非負整數，例如 1234567890");
            CodeIn.PlaceholderText = P("Paste a code to decode", "貼上要解碼嘅代碼");
            CopyCodeBtn.Content = P("Copy", "複製");
            CopyNumberBtn.Content = P("Copy", "複製");

            RandomTitle.Text = P("Random short IDs", "隨機短 ID");
            LengthLabel.Text = P("Length (4–32)", "長度（4–32）");
            CountLabel.Text = P("Count", "數量");
            RandomAlphabetLabel.Text = P("Alphabet", "字母表");
            GenerateBtn.Content = P("Generate", "產生");
            CopyRandomBtn.Content = P("Copy all", "全部複製");
            RandomOut.PlaceholderText = P("Generated IDs appear here, one per line.", "產生嘅 ID 會喺度出現，每行一個。");

            if (string.IsNullOrEmpty(ConvertStatus.Text)) ConvertStatus.Text = "";
            if (string.IsNullOrEmpty(RandomStatus.Text)) RandomStatus.Text = "";
        }
        catch { /* never throw */ }
    }

    private void Alphabet_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        RunConvert();
    }

    private void Encode_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RunEncode();
    }

    private void Decode_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RunDecode();
    }

    private void RunConvert()
    {
        RunEncode();
        RunDecode();
    }

    private void RunEncode()
    {
        try
        {
            string alpha = ShortIdService.Resolve(TagOf(AlphabetCombo));
            string raw = (NumberBox.Text ?? "").Trim();
            if (raw.Length == 0) { CodeOut.Text = ""; ConvertStatus.Text = ""; return; }

            if (!BigInteger.TryParse(raw, out var value))
            {
                CodeOut.Text = "";
                ConvertStatus.Text = P("That is not a whole number.", "嗰個唔係整數。");
                return;
            }

            var r = ShortIdService.Encode(value, alpha);
            if (r.Ok)
            {
                CodeOut.Text = r.Value;
                ConvertStatus.Text = P($"Encoded {r.Value.Length} chars.", $"已編碼 {r.Value.Length} 個字元。");
            }
            else
            {
                CodeOut.Text = "";
                ConvertStatus.Text = r.Error == "negative"
                    ? P("Only non-negative integers can be encoded.", "淨係可以編碼非負整數。")
                    : P("Could not encode that value.", "無法編碼呢個數值。");
            }
        }
        catch
        {
            CodeOut.Text = "";
            ConvertStatus.Text = P("Encoding failed.", "編碼失敗。");
        }
    }

    private void RunDecode()
    {
        try
        {
            string alpha = ShortIdService.Resolve(TagOf(AlphabetCombo));
            string code = (CodeIn.Text ?? "").Trim();
            if (code.Length == 0) { NumberOut.Text = ""; return; }

            var r = ShortIdService.Decode(code, alpha);
            if (r.Ok)
            {
                NumberOut.Text = r.Value;
                ConvertStatus.Text = P("Decoded successfully.", "已成功解碼。");
            }
            else
            {
                NumberOut.Text = "";
                ConvertStatus.Text = r.Error != null && r.Error.StartsWith("digit:")
                    ? P($"‘{r.Error.Substring(6)}’ is not a digit in this alphabet.", $"「{r.Error.Substring(6)}」唔屬於呢個字母表。")
                    : P("Could not decode that code.", "無法解碼呢個代碼。");
            }
        }
        catch
        {
            NumberOut.Text = "";
            ConvertStatus.Text = P("Decoding failed.", "解碼失敗。");
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int length = (int)(double.IsNaN(LengthBox.Value) ? 21 : LengthBox.Value);
            int count = (int)(double.IsNaN(CountBox.Value) ? 5 : CountBox.Value);
            string alpha = ShortIdService.Resolve(TagOf(RandomAlphabetCombo));

            var r = ShortIdService.GenerateMany(length, count, alpha);
            if (r.Ok)
            {
                RandomOut.Text = r.Value;
                RandomStatus.Text = P($"Generated {count} ID(s) of length {length}.", $"已產生 {count} 個長度 {length} 嘅 ID。");
            }
            else
            {
                RandomStatus.Text = P("Could not generate IDs.", "無法產生 ID。");
            }
        }
        catch
        {
            RandomStatus.Text = P("Generation failed.", "產生失敗。");
        }
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e) => Copy(CodeOut.Text, ConvertStatus);
    private void CopyNumber_Click(object sender, RoutedEventArgs e) => Copy(NumberOut.Text, ConvertStatus);
    private void CopyRandom_Click(object sender, RoutedEventArgs e) => Copy(RandomOut.Text, RandomStatus);

    private void Copy(string? text, TextBlock status)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                status.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            status.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            status.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
