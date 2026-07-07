using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 程式員進位轉換 · Programmer number-base converter — parse a value in binary/octal/decimal/hex or any
/// custom base 2–36, and see it live in every base at once, plus bit info and a bitwise calculator.
/// Pure managed BigInteger maths (no overflow). All parsing is guarded so bad input just sets a status. Bilingual.
/// </summary>
public sealed partial class BaseConvertModule : Page
{
    private bool _suppress;

    public BaseConvertModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            BuildCombos();
            Render();
            _suppress = true;
            BaseCombo.SelectedIndex = 2;  // Decimal
            OpCombo.SelectedIndex = 0;    // AND
            ValueBox.Text = "255";
            OperandA.Text = "0xF0";
            OperandB.Text = "0x0F";
            _suppress = false;
            Recompute();
            RecomputeBitwise();
        };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        Recompute();
        RecomputeBitwise();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        _suppress = true;
        int baseSel = BaseCombo.SelectedIndex;
        BaseCombo.Items.Clear();
        BaseCombo.Items.Add(P("Binary (2)", "二進制 (2)"));
        BaseCombo.Items.Add(P("Octal (8)", "八進制 (8)"));
        BaseCombo.Items.Add(P("Decimal (10)", "十進制 (10)"));
        BaseCombo.Items.Add(P("Hex (16)", "十六進制 (16)"));
        BaseCombo.Items.Add(P("Custom…", "自訂…"));
        BaseCombo.SelectedIndex = baseSel < 0 ? 2 : baseSel;

        int opSel = OpCombo.SelectedIndex;
        OpCombo.Items.Clear();
        OpCombo.Items.Add("AND");
        OpCombo.Items.Add("OR");
        OpCombo.Items.Add("XOR");
        OpCombo.Items.Add("NAND");
        OpCombo.Items.Add("NOR");
        OpCombo.Items.Add(P("Left shift «", "左移 «"));
        OpCombo.Items.Add(P("Right shift »", "右移 »"));
        OpCombo.SelectedIndex = opSel < 0 ? 0 : opSel;
        _suppress = false;
    }

    private void Render()
    {
        Header.Title = "Base Converter · 進位轉換";
        HeaderBlurb.Text = P("Convert a number between binary, octal, decimal, hexadecimal or any base 2–36 — all at once — with bit info and a bitwise calculator. Big numbers welcome (no overflow).",
            "喺二進制、八進制、十進制、十六進制或者任何 2–36 進制之間互相轉換 — 一次過全部顯示 — 仲有位元資訊同埋位元運算計數機。大數都得（唔會溢位）。");

        InputTitle.Text = P("Input", "輸入");
        CustomBaseLabel.Text = P("Custom base (2–36)", "自訂進制（2–36）");
        OutputsTitle.Text = P("Outputs", "輸出");
        BinLabel.Text = P("Binary (grouped in nibbles)", "二進制（每四位一組）");
        OctLabel.Text = P("Octal", "八進制");
        DecLabel.Text = P("Decimal", "十進制");
        HexLabel.Text = P("Hexadecimal", "十六進制");

        BitInfoTitle.Text = P("Bit info", "位元資訊");
        Bit64Label.Text = P("64-bit two's-complement", "64 位元二補數");

        BitwiseTitle.Text = P("Bitwise calculator", "位元運算計數機");
        BitwiseBlurb.Text = P("Operands accept plain decimal or a 0x-prefixed hex literal.", "運算元可以用普通十進制或者 0x 開頭嘅十六進制。");
        ShiftLabel.Text = P("Shift by (bits)", "移位（位元）");
        BitwiseResultLabel.Text = P("Result", "結果");

        string copy = P("Copy", "複製");
        BinCopy.Content = copy; OctCopy.Content = copy; DecCopy.Content = copy; HexCopy.Content = copy; CustomCopy.Content = copy;

        UpdateCustomLabels();
    }

    private int CurrentInputBase()
    {
        return BaseCombo.SelectedIndex switch
        {
            0 => 2,
            1 => 8,
            2 => 10,
            3 => 16,
            _ => CustomBaseValue(),
        };
    }

    private int CustomBaseValue()
    {
        double v = CustomBaseBox.Value;
        if (double.IsNaN(v)) return 36;
        int b = (int)v;
        if (b < BaseConvertService.MinBase) b = BaseConvertService.MinBase;
        if (b > BaseConvertService.MaxBase) b = BaseConvertService.MaxBase;
        return b;
    }

    private void UpdateCustomLabels()
    {
        int cb = CurrentInputBase();
        // The "custom" output row always mirrors the currently-selected input base.
        CustomOutLabel.Text = P($"Base {cb}", $"{cb} 進制");
    }

    // ---- Converter events ----

    private void Base_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        CustomBasePanel.Visibility = BaseCombo.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCustomLabels();
        Recompute();
    }

    private void CustomBase_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        UpdateCustomLabels();
        Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Recompute()
    {
        int radix = CurrentInputBase();
        UpdateCustomLabels();

        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ClearOutputs();
            StatusText.Text = P("Enter a value to convert.", "輸入一個值嚟轉換。");
            return;
        }

        if (!BaseConvertService.TryParse(ValueBox.Text, radix, out BigInteger value))
        {
            ClearOutputs();
            StatusText.Text = P($"“{ValueBox.Text.Trim()}” is not a valid base-{radix} number.", $"「{ValueBox.Text.Trim()}」唔係有效嘅 {radix} 進制數字。");
            return;
        }

        StatusText.Text = P($"Parsed as base {radix}.", $"已當作 {radix} 進制解析。");
        BinOut.Text = BaseConvertService.ToGroupedBinary(value);
        OctOut.Text = BaseConvertService.ToBase(value, 8);
        DecOut.Text = value.ToString();
        HexOut.Text = BaseConvertService.ToHexPrefixed(value);
        CustomOut.Text = BaseConvertService.ToBase(value, radix);

        long bits = BaseConvertService.BitLength(value);
        BitLengthText.Text = P($"Bit length: {bits}", $"位元長度：{bits}");
        if (BaseConvertService.FitsIn64Bits(value))
        {
            Bit64Label.Visibility = Visibility.Visible;
            Bit64Out.Visibility = Visibility.Visible;
            Bit64Out.Text = BaseConvertService.To64BitBinary(value);
        }
        else
        {
            Bit64Label.Visibility = Visibility.Visible;
            Bit64Out.Visibility = Visibility.Collapsed;
            Bit64Label.Text = P("Value exceeds 64 bits.", "數值超過 64 位元。");
        }
        if (BaseConvertService.FitsIn64Bits(value))
            Bit64Label.Text = P("64-bit two's-complement", "64 位元二補數");
    }

    private void ClearOutputs()
    {
        BinOut.Text = OctOut.Text = DecOut.Text = HexOut.Text = CustomOut.Text = string.Empty;
        BitLengthText.Text = string.Empty;
        Bit64Out.Text = string.Empty;
        Bit64Out.Visibility = Visibility.Collapsed;
    }

    // ---- Copy buttons ----

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        pkg.SetText(text);
        Clipboard.SetContent(pkg);
    }

    private void Copy_Bin(object sender, RoutedEventArgs e) => CopyToClipboard(BinOut.Text);
    private void Copy_Oct(object sender, RoutedEventArgs e) => CopyToClipboard(OctOut.Text);
    private void Copy_Dec(object sender, RoutedEventArgs e) => CopyToClipboard(DecOut.Text);
    private void Copy_Hex(object sender, RoutedEventArgs e) => CopyToClipboard(HexOut.Text);
    private void Copy_Custom(object sender, RoutedEventArgs e) => CopyToClipboard(CustomOut.Text);

    // ---- Bitwise events ----

    private BaseConvertService.BitOp CurrentOp() => OpCombo.SelectedIndex switch
    {
        0 => BaseConvertService.BitOp.And,
        1 => BaseConvertService.BitOp.Or,
        2 => BaseConvertService.BitOp.Xor,
        3 => BaseConvertService.BitOp.Nand,
        4 => BaseConvertService.BitOp.Nor,
        5 => BaseConvertService.BitOp.LeftShift,
        6 => BaseConvertService.BitOp.RightShift,
        _ => BaseConvertService.BitOp.And,
    };

    private void Op_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        var op = CurrentOp();
        bool isShift = op is BaseConvertService.BitOp.LeftShift or BaseConvertService.BitOp.RightShift;
        ShiftPanel.Visibility = isShift ? Visibility.Visible : Visibility.Collapsed;
        OperandB.IsEnabled = !isShift;
        RecomputeBitwise();
    }

    private void Bitwise_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RecomputeBitwise();
    }

    private void Shift_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        RecomputeBitwise();
    }

    private void RecomputeBitwise()
    {
        var op = CurrentOp();
        bool isShift = op is BaseConvertService.BitOp.LeftShift or BaseConvertService.BitOp.RightShift;

        if (!BaseConvertService.TryParseOperand(OperandA.Text, out BigInteger a))
        {
            BitwiseResultText.Text = P("Operand A is not a valid number.", "運算元 A 唔係有效數字。");
            return;
        }

        BigInteger b = BigInteger.Zero;
        if (!isShift && !BaseConvertService.TryParseOperand(OperandB.Text, out b))
        {
            BitwiseResultText.Text = P("Operand B is not a valid number.", "運算元 B 唔係有效數字。");
            return;
        }

        int shift = 0;
        if (isShift)
        {
            double sv = ShiftBox.Value;
            shift = double.IsNaN(sv) ? 0 : (int)sv;
            if (shift < 0) shift = 0;
        }

        BigInteger result = BaseConvertService.Evaluate(op, a, b, shift);
        BitwiseResultText.Text = $"{result}  ·  {BaseConvertService.ToHexPrefixed(result)}";
    }
}
