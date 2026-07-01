using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 特殊符號調色盤 · Special-symbols palette. Pure-managed curated symbol sets, grouped by
/// category. No I/O, no shelling out — every symbol is embedded here so the module never throws.
/// </summary>
public static class SymbolsService
{
    /// <summary>One clickable glyph: the character plus a short bilingual name for search.</summary>
    public sealed class SymbolItem
    {
        public string Symbol { get; init; } = "";
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        public override string ToString() => Symbol;
    }

    /// <summary>A named group of symbols (bilingual display name via <see cref="Display"/>).</summary>
    public sealed class Category
    {
        public string En { get; init; } = "";
        public string Zh { get; init; } = "";
        public IReadOnlyList<SymbolItem> Items { get; init; } = Array.Empty<SymbolItem>();
        public string Display => Loc.I.Pick(En, Zh);
    }

    // --- Raw curated data: symbol + English name + 粵語名 -------------------------------------

    private static SymbolItem S(string sym, string en, string zh, string cat) =>
        new() { Symbol = sym, Name = $"{en} · {zh}", Category = cat };

    private static readonly List<Category> _cats = BuildCategories();

    /// <summary>All categories, in display order. Never null, never empty.</summary>
    public static IReadOnlyList<Category> Categories => _cats;

    /// <summary>Flat list of every symbol across all categories.</summary>
    public static IReadOnlyList<SymbolItem> All { get; } =
        _cats.SelectMany(c => c.Items).ToList();

    /// <summary>
    /// Filter by category (null/empty = all) and a case-insensitive search over symbol + name.
    /// Never throws; returns an empty list on any trouble.
    /// </summary>
    public static IReadOnlyList<SymbolItem> Filter(string? categoryEn, string? search)
    {
        try
        {
            IEnumerable<SymbolItem> q = string.IsNullOrEmpty(categoryEn)
                ? All
                : _cats.FirstOrDefault(c => c.En == categoryEn)?.Items ?? All;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                q = q.Where(i =>
                    i.Symbol.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    i.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
            }
            return q.ToList();
        }
        catch
        {
            return Array.Empty<SymbolItem>();
        }
    }

    private static List<Category> BuildCategories()
    {
        const string cArrows = "Arrows", cMath = "Math", cCurrency = "Currency",
            cPunct = "Punctuation", cGreek = "Greek", cBox = "Box Drawing",
            cStars = "Stars & Bullets", cFrac = "Fractions", cSup = "Super / Subscript";

        var arrows = new[]
        {
            S("←", "Left arrow", "左箭嘴", cArrows), S("→", "Right arrow", "右箭嘴", cArrows),
            S("↑", "Up arrow", "上箭嘴", cArrows), S("↓", "Down arrow", "下箭嘴", cArrows),
            S("↔", "Left-right arrow", "左右箭嘴", cArrows), S("↕", "Up-down arrow", "上下箭嘴", cArrows),
            S("↖", "Up-left arrow", "左上箭嘴", cArrows), S("↗", "Up-right arrow", "右上箭嘴", cArrows),
            S("↘", "Down-right arrow", "右下箭嘴", cArrows), S("↙", "Down-left arrow", "左下箭嘴", cArrows),
            S("⇐", "Double left", "雙左箭嘴", cArrows), S("⇒", "Double right", "雙右箭嘴", cArrows),
            S("⇑", "Double up", "雙上箭嘴", cArrows), S("⇓", "Double down", "雙下箭嘴", cArrows),
            S("⇔", "Double left-right", "雙左右箭嘴", cArrows), S("⟵", "Long left", "長左箭嘴", cArrows),
            S("⟶", "Long right", "長右箭嘴", cArrows), S("↩", "Return left", "回轉左", cArrows),
            S("↪", "Return right", "回轉右", cArrows), S("⤴", "Arrow up-right curve", "上翹箭嘴", cArrows),
            S("⤵", "Arrow down-right curve", "下翹箭嘴", cArrows), S("↻", "Clockwise", "順時針", cArrows),
            S("↺", "Anticlockwise", "逆時針", cArrows), S("➜", "Heavy arrow", "粗箭嘴", cArrows),
        };

        var math = new[]
        {
            S("±", "Plus-minus", "正負", cMath), S("×", "Times", "乘", cMath),
            S("÷", "Divide", "除", cMath), S("∑", "Summation", "總和", cMath),
            S("∏", "Product", "連乘", cMath), S("∫", "Integral", "積分", cMath),
            S("∂", "Partial", "偏微分", cMath), S("∇", "Nabla", "梯度算子", cMath),
            S("√", "Square root", "根號", cMath), S("∛", "Cube root", "立方根", cMath),
            S("≠", "Not equal", "唔等於", cMath), S("≈", "Approx", "約等於", cMath),
            S("≡", "Identical", "恆等", cMath), S("≤", "Less-equal", "細過或等", cMath),
            S("≥", "Greater-equal", "大過或等", cMath), S("∞", "Infinity", "無限", cMath),
            S("∈", "Element of", "屬於", cMath), S("∉", "Not element", "唔屬於", cMath),
            S("⊂", "Subset", "子集", cMath), S("⊆", "Subset-equal", "子集或等", cMath),
            S("∪", "Union", "聯集", cMath), S("∩", "Intersection", "交集", cMath),
            S("∀", "For all", "對於所有", cMath), S("∃", "Exists", "存在", cMath),
            S("∅", "Empty set", "空集", cMath), S("∝", "Proportional", "成比例", cMath),
            S("∠", "Angle", "角", cMath), S("°", "Degree", "度", cMath),
            S("µ", "Micro", "微", cMath), S("π", "Pi", "圓周率", cMath),
            S("∴", "Therefore", "所以", cMath), S("∵", "Because", "因為", cMath),
        };

        var currency = new[]
        {
            S("$", "Dollar", "美元", cCurrency), S("€", "Euro", "歐元", cCurrency),
            S("£", "Pound", "英鎊", cCurrency), S("¥", "Yen / Yuan", "日圓／人民幣", cCurrency),
            S("₿", "Bitcoin", "比特幣", cCurrency), S("₩", "Won", "韓圜", cCurrency),
            S("₹", "Rupee", "印度盧比", cCurrency), S("₽", "Ruble", "俄羅斯盧布", cCurrency),
            S("₴", "Hryvnia", "烏克蘭格里夫納", cCurrency), S("₫", "Dong", "越南盾", cCurrency),
            S("₱", "Peso", "菲律賓披索", cCurrency), S("₡", "Colon", "哥斯達黎加科朗", cCurrency),
            S("₪", "Shekel", "以色列謝克爾", cCurrency), S("₭", "Kip", "老撾基普", cCurrency),
            S("₮", "Tugrik", "蒙古圖格里克", cCurrency), S("₦", "Naira", "奈及利亞奈拉", cCurrency),
            S("¢", "Cent", "仙", cCurrency), S("₲", "Guarani", "瓜拉尼", cCurrency),
            S("₺", "Lira", "土耳其里拉", cCurrency), S("﷼", "Rial", "里亞爾", cCurrency),
        };

        var punct = new[]
        {
            S("…", "Ellipsis", "省略號", cPunct), S("—", "Em dash", "破折號", cPunct),
            S("–", "En dash", "連接號", cPunct), S("«", "Left guillemet", "左書名號", cPunct),
            S("»", "Right guillemet", "右書名號", cPunct), S("„", "Low quote", "低引號", cPunct),
            S("“", "Left double quote", "左雙引號", cPunct), S("”", "Right double quote", "右雙引號", cPunct),
            S("‘", "Left single quote", "左單引號", cPunct), S("’", "Right single quote", "右單引號", cPunct),
            S("•", "Bullet", "圓點", cPunct), S("·", "Middle dot", "間隔號", cPunct),
            S("†", "Dagger", "劍標", cPunct), S("‡", "Double dagger", "雙劍標", cPunct),
            S("§", "Section", "章節符", cPunct), S("¶", "Pilcrow", "段落符", cPunct),
            S("©", "Copyright", "版權", cPunct), S("®", "Registered", "註冊商標", cPunct),
            S("™", "Trademark", "商標", cPunct), S("‰", "Per mille", "千分號", cPunct),
            S("¡", "Inverted !", "倒感嘆號", cPunct), S("¿", "Inverted ?", "倒問號", cPunct),
            S("〜", "Wave dash", "波浪號", cPunct), S("　", "Ideographic space", "全形空格", cPunct),
        };

        var greek = new[]
        {
            S("α", "Alpha", "阿爾法", cGreek), S("β", "Beta", "貝塔", cGreek),
            S("γ", "Gamma", "伽瑪", cGreek), S("δ", "Delta", "德爾塔", cGreek),
            S("ε", "Epsilon", "艾普西龍", cGreek), S("ζ", "Zeta", "澤塔", cGreek),
            S("η", "Eta", "伊塔", cGreek), S("θ", "Theta", "西塔", cGreek),
            S("ι", "Iota", "約塔", cGreek), S("κ", "Kappa", "卡帕", cGreek),
            S("λ", "Lambda", "蘭姆達", cGreek), S("μ", "Mu", "繆", cGreek),
            S("ν", "Nu", "紐", cGreek), S("ξ", "Xi", "克西", cGreek),
            S("π", "Pi", "派", cGreek), S("ρ", "Rho", "柔", cGreek),
            S("σ", "Sigma", "西格瑪", cGreek), S("τ", "Tau", "陶", cGreek),
            S("φ", "Phi", "斐", cGreek), S("χ", "Chi", "希", cGreek),
            S("ψ", "Psi", "普西", cGreek), S("ω", "Omega (small)", "細寫奧米加", cGreek),
            S("Γ", "Gamma cap", "大寫伽瑪", cGreek), S("Δ", "Delta cap", "大寫德爾塔", cGreek),
            S("Θ", "Theta cap", "大寫西塔", cGreek), S("Λ", "Lambda cap", "大寫蘭姆達", cGreek),
            S("Π", "Pi cap", "大寫派", cGreek), S("Σ", "Sigma cap", "大寫西格瑪", cGreek),
            S("Φ", "Phi cap", "大寫斐", cGreek), S("Ψ", "Psi cap", "大寫普西", cGreek),
            S("Ω", "Omega", "奧米加", cGreek),
        };

        var box = new[]
        {
            S("─", "Horizontal", "橫線", cBox), S("│", "Vertical", "直線", cBox),
            S("┌", "Down-right", "左上角", cBox), S("┐", "Down-left", "右上角", cBox),
            S("└", "Up-right", "左下角", cBox), S("┘", "Up-left", "右下角", cBox),
            S("├", "Vertical-right", "左T", cBox), S("┤", "Vertical-left", "右T", cBox),
            S("┬", "Down-horizontal", "上T", cBox), S("┴", "Up-horizontal", "下T", cBox),
            S("┼", "Cross", "十字", cBox), S("═", "Double horizontal", "雙橫線", cBox),
            S("║", "Double vertical", "雙直線", cBox), S("╔", "Double down-right", "雙左上角", cBox),
            S("╗", "Double down-left", "雙右上角", cBox), S("╚", "Double up-right", "雙左下角", cBox),
            S("╝", "Double up-left", "雙右下角", cBox), S("╬", "Double cross", "雙十字", cBox),
            S("╭", "Round down-right", "圓左上角", cBox), S("╮", "Round down-left", "圓右上角", cBox),
            S("╰", "Round up-right", "圓左下角", cBox), S("╯", "Round up-left", "圓右下角", cBox),
            S("░", "Light shade", "淺陰影", cBox), S("▒", "Medium shade", "中陰影", cBox),
            S("▓", "Dark shade", "深陰影", cBox), S("█", "Full block", "實心塊", cBox),
        };

        var stars = new[]
        {
            S("★", "Black star", "實心星", cStars), S("☆", "White star", "空心星", cStars),
            S("✦", "Four-point star", "四角星", cStars), S("✧", "White four-point", "空心四角星", cStars),
            S("✪", "Circled star", "圓星", cStars), S("✯", "Pinwheel star", "風車星", cStars),
            S("❋", "Heavy flower", "花星", cStars), S("●", "Black circle", "實心圓", cStars),
            S("○", "White circle", "空心圓", cStars), S("◉", "Fisheye", "牛眼", cStars),
            S("◆", "Black diamond", "實心菱", cStars), S("◇", "White diamond", "空心菱", cStars),
            S("■", "Black square", "實心方", cStars), S("□", "White square", "空心方", cStars),
            S("▪", "Small black square", "細實心方", cStars), S("▫", "Small white square", "細空心方", cStars),
            S("▶", "Play right", "右三角", cStars), S("◀", "Play left", "左三角", cStars),
            S("▲", "Up triangle", "上三角", cStars), S("▼", "Down triangle", "下三角", cStars),
            S("✔", "Check", "剔號", cStars), S("✗", "Cross mark", "叉號", cStars),
            S("✚", "Heavy plus", "粗加號", cStars), S("❤", "Heart", "心", cStars),
            S("☑", "Ballot check", "剔格", cStars), S("☒", "Ballot cross", "叉格", cStars),
        };

        var frac = new[]
        {
            S("½", "One half", "二分一", cFrac), S("⅓", "One third", "三分一", cFrac),
            S("⅔", "Two thirds", "三分二", cFrac), S("¼", "One quarter", "四分一", cFrac),
            S("¾", "Three quarters", "四分三", cFrac), S("⅕", "One fifth", "五分一", cFrac),
            S("⅖", "Two fifths", "五分二", cFrac), S("⅗", "Three fifths", "五分三", cFrac),
            S("⅘", "Four fifths", "五分四", cFrac), S("⅙", "One sixth", "六分一", cFrac),
            S("⅚", "Five sixths", "六分五", cFrac), S("⅛", "One eighth", "八分一", cFrac),
            S("⅜", "Three eighths", "八分三", cFrac), S("⅝", "Five eighths", "八分五", cFrac),
            S("⅞", "Seven eighths", "八分七", cFrac), S("⅐", "One seventh", "七分一", cFrac),
            S("⅑", "One ninth", "九分一", cFrac), S("⅒", "One tenth", "十分一", cFrac),
        };

        var sup = new[]
        {
            S("⁰", "Superscript 0", "上標0", cSup), S("¹", "Superscript 1", "上標1", cSup),
            S("²", "Superscript 2", "上標2", cSup), S("³", "Superscript 3", "上標3", cSup),
            S("⁴", "Superscript 4", "上標4", cSup), S("⁵", "Superscript 5", "上標5", cSup),
            S("⁶", "Superscript 6", "上標6", cSup), S("⁷", "Superscript 7", "上標7", cSup),
            S("⁸", "Superscript 8", "上標8", cSup), S("⁹", "Superscript 9", "上標9", cSup),
            S("ⁿ", "Superscript n", "上標n", cSup), S("⁺", "Superscript +", "上標加", cSup),
            S("⁻", "Superscript -", "上標減", cSup), S("₀", "Subscript 0", "下標0", cSup),
            S("₁", "Subscript 1", "下標1", cSup), S("₂", "Subscript 2", "下標2", cSup),
            S("₃", "Subscript 3", "下標3", cSup), S("₄", "Subscript 4", "下標4", cSup),
            S("₅", "Subscript 5", "下標5", cSup), S("₆", "Subscript 6", "下標6", cSup),
            S("₇", "Subscript 7", "下標7", cSup), S("₈", "Subscript 8", "下標8", cSup),
            S("₉", "Subscript 9", "下標9", cSup), S("₊", "Subscript +", "下標加", cSup),
            S("₋", "Subscript -", "下標減", cSup),
        };

        return new List<Category>
        {
            new() { En = cArrows,   Zh = "箭嘴",     Items = arrows },
            new() { En = cMath,     Zh = "數學",     Items = math },
            new() { En = cCurrency, Zh = "貨幣",     Items = currency },
            new() { En = cPunct,    Zh = "標點",     Items = punct },
            new() { En = cGreek,    Zh = "希臘字母", Items = greek },
            new() { En = cBox,      Zh = "框線",     Items = box },
            new() { En = cStars,    Zh = "星與點",   Items = stars },
            new() { En = cFrac,     Zh = "分數",     Items = frac },
            new() { En = cSup,      Zh = "上下標",   Items = sup },
        };
    }
}
