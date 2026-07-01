using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>One convertible unit inside a category — bilingual name + factor to the base unit.</summary>
public sealed class UnitDef
{
    public string Id { get; init; } = "";
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";

    /// <summary>Value in base units = input * Factor + Offset (offset used only for temperature).</summary>
    public double Factor { get; init; } = 1.0;
    public double Offset { get; init; } = 0.0;

    public string Label(Loc loc) => $"{loc.Pick(En, Zh)}";
}

/// <summary>A family of units that convert among themselves (length, mass, …).</summary>
public sealed class UnitCategory
{
    public string Id { get; init; } = "";
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public IReadOnlyList<UnitDef> Units { get; init; } = Array.Empty<UnitDef>();

    public string Label(Loc loc) => loc.Pick(En, Zh);
}

/// <summary>
/// 簡單單位換算 · Simple offline unit conversion (length, mass, temperature, data, speed, area, volume).
/// Linear via base-unit factors; temperature handled with an explicit affine transform. No network.
/// </summary>
public static class UnitConvertService
{
    public static IReadOnlyList<UnitCategory> Categories { get; } = new[]
    {
        new UnitCategory
        {
            Id = "length", En = "Length", Zh = "長度",
            Units = new[]
            {
                new UnitDef { Id = "mm", En = "Millimetre (mm)", Zh = "毫米 (mm)", Factor = 0.001 },
                new UnitDef { Id = "cm", En = "Centimetre (cm)", Zh = "厘米 (cm)", Factor = 0.01 },
                new UnitDef { Id = "m",  En = "Metre (m)",       Zh = "米 (m)",    Factor = 1.0 },
                new UnitDef { Id = "km", En = "Kilometre (km)",  Zh = "公里 (km)", Factor = 1000.0 },
                new UnitDef { Id = "in", En = "Inch (in)",       Zh = "英寸 (in)", Factor = 0.0254 },
                new UnitDef { Id = "ft", En = "Foot (ft)",       Zh = "英尺 (ft)", Factor = 0.3048 },
                new UnitDef { Id = "yd", En = "Yard (yd)",       Zh = "碼 (yd)",   Factor = 0.9144 },
                new UnitDef { Id = "mi", En = "Mile (mi)",       Zh = "英里 (mi)", Factor = 1609.344 },
                new UnitDef { Id = "nmi", En = "Nautical mile",  Zh = "海里",      Factor = 1852.0 },
            },
        },
        new UnitCategory
        {
            Id = "mass", En = "Mass / Weight", Zh = "質量／重量",
            Units = new[]
            {
                new UnitDef { Id = "mg", En = "Milligram (mg)", Zh = "毫克 (mg)", Factor = 1e-6 },
                new UnitDef { Id = "g",  En = "Gram (g)",       Zh = "克 (g)",    Factor = 0.001 },
                new UnitDef { Id = "kg", En = "Kilogram (kg)",  Zh = "公斤 (kg)", Factor = 1.0 },
                new UnitDef { Id = "t",  En = "Tonne (t)",      Zh = "公噸 (t)",  Factor = 1000.0 },
                new UnitDef { Id = "oz", En = "Ounce (oz)",     Zh = "盎司 (oz)", Factor = 0.028349523125 },
                new UnitDef { Id = "lb", En = "Pound (lb)",     Zh = "磅 (lb)",   Factor = 0.45359237 },
                new UnitDef { Id = "st", En = "Stone (st)",     Zh = "英石 (st)", Factor = 6.35029318 },
                new UnitDef { Id = "catty", En = "Catty (斤)",  Zh = "斤",        Factor = 0.6048 },
            },
        },
        new UnitCategory
        {
            Id = "temp", En = "Temperature", Zh = "溫度",
            Units = new[]
            {
                // Base = Celsius. value_base = input*Factor + Offset.
                new UnitDef { Id = "c", En = "Celsius (°C)",    Zh = "攝氏 (°C)", Factor = 1.0,        Offset = 0.0 },
                new UnitDef { Id = "f", En = "Fahrenheit (°F)", Zh = "華氏 (°F)", Factor = 5.0 / 9.0,  Offset = -160.0 / 9.0 },
                new UnitDef { Id = "k", En = "Kelvin (K)",      Zh = "開爾文 (K)", Factor = 1.0,       Offset = -273.15 },
                new UnitDef { Id = "r", En = "Rankine (°R)",    Zh = "蘭氏 (°R)",  Factor = 5.0 / 9.0,  Offset = -273.15 },
            },
        },
        new UnitCategory
        {
            Id = "data", En = "Data size", Zh = "資料大小",
            Units = new[]
            {
                new UnitDef { Id = "bit", En = "Bit (bit)",      Zh = "位元 (bit)",  Factor = 0.125 },
                new UnitDef { Id = "b",   En = "Byte (B)",       Zh = "位元組 (B)",  Factor = 1.0 },
                new UnitDef { Id = "kb",  En = "Kilobyte (KB)",  Zh = "KB",          Factor = 1000.0 },
                new UnitDef { Id = "mb",  En = "Megabyte (MB)",  Zh = "MB",          Factor = 1e6 },
                new UnitDef { Id = "gb",  En = "Gigabyte (GB)",  Zh = "GB",          Factor = 1e9 },
                new UnitDef { Id = "tb",  En = "Terabyte (TB)",  Zh = "TB",          Factor = 1e12 },
                new UnitDef { Id = "kib", En = "Kibibyte (KiB)", Zh = "KiB",         Factor = 1024.0 },
                new UnitDef { Id = "mib", En = "Mebibyte (MiB)", Zh = "MiB",         Factor = 1048576.0 },
                new UnitDef { Id = "gib", En = "Gibibyte (GiB)", Zh = "GiB",         Factor = 1073741824.0 },
                new UnitDef { Id = "tib", En = "Tebibyte (TiB)", Zh = "TiB",         Factor = 1099511627776.0 },
            },
        },
        new UnitCategory
        {
            Id = "speed", En = "Speed", Zh = "速度",
            Units = new[]
            {
                new UnitDef { Id = "mps",  En = "Metre/sec (m/s)",  Zh = "米／秒 (m/s)",   Factor = 1.0 },
                new UnitDef { Id = "kmh",  En = "Km/hour (km/h)",   Zh = "公里／時 (km/h)", Factor = 1000.0 / 3600.0 },
                new UnitDef { Id = "mph",  En = "Mile/hour (mph)",  Zh = "英里／時 (mph)",  Factor = 1609.344 / 3600.0 },
                new UnitDef { Id = "kn",   En = "Knot (kn)",        Zh = "節 (kn)",         Factor = 1852.0 / 3600.0 },
                new UnitDef { Id = "fts",  En = "Foot/sec (ft/s)",  Zh = "英尺／秒 (ft/s)", Factor = 0.3048 },
            },
        },
        new UnitCategory
        {
            Id = "area", En = "Area", Zh = "面積",
            Units = new[]
            {
                new UnitDef { Id = "m2",  En = "Square metre (m²)", Zh = "平方米 (m²)",  Factor = 1.0 },
                new UnitDef { Id = "km2", En = "Square km (km²)",   Zh = "平方公里 (km²)", Factor = 1e6 },
                new UnitDef { Id = "ft2", En = "Square foot (ft²)", Zh = "平方英尺 (ft²)", Factor = 0.09290304 },
                new UnitDef { Id = "ac",  En = "Acre",              Zh = "英畝",         Factor = 4046.8564224 },
                new UnitDef { Id = "ha",  En = "Hectare",           Zh = "公頃",         Factor = 10000.0 },
            },
        },
        new UnitCategory
        {
            Id = "volume", En = "Volume", Zh = "體積",
            Units = new[]
            {
                new UnitDef { Id = "ml",  En = "Millilitre (mL)",  Zh = "毫升 (mL)", Factor = 0.001 },
                new UnitDef { Id = "l",   En = "Litre (L)",        Zh = "公升 (L)",  Factor = 1.0 },
                new UnitDef { Id = "m3",  En = "Cubic metre (m³)", Zh = "立方米 (m³)", Factor = 1000.0 },
                new UnitDef { Id = "tsp", En = "Teaspoon (US)",    Zh = "茶匙（美）", Factor = 0.00492892159375 },
                new UnitDef { Id = "tbsp", En = "Tablespoon (US)", Zh = "湯匙（美）", Factor = 0.01478676478125 },
                new UnitDef { Id = "cup", En = "Cup (US)",         Zh = "杯（美）",  Factor = 0.2365882365 },
                new UnitDef { Id = "galus", En = "Gallon (US)",    Zh = "加侖（美）", Factor = 3.785411784 },
                new UnitDef { Id = "galuk", En = "Gallon (UK)",    Zh = "加侖（英）", Factor = 4.54609 },
            },
        },
        new UnitCategory
        {
            Id = "time", En = "Time", Zh = "時間",
            Units = new[]
            {
                new UnitDef { Id = "ms",   En = "Millisecond (ms)", Zh = "毫秒 (ms)",  Factor = 0.001 },
                new UnitDef { Id = "s",    En = "Second (s)",       Zh = "秒 (s)",     Factor = 1.0 },
                new UnitDef { Id = "min",  En = "Minute (min)",     Zh = "分鐘 (min)", Factor = 60.0 },
                new UnitDef { Id = "h",    En = "Hour (h)",         Zh = "小時 (h)",   Factor = 3600.0 },
                new UnitDef { Id = "day",  En = "Day",              Zh = "日",         Factor = 86400.0 },
                new UnitDef { Id = "week", En = "Week",             Zh = "星期",       Factor = 604800.0 },
                new UnitDef { Id = "year", En = "Year (365.25 d)",  Zh = "年（365.25 日）", Factor = 31557600.0 },
            },
        },
        new UnitCategory
        {
            Id = "pressure", En = "Pressure", Zh = "壓力",
            Units = new[]
            {
                new UnitDef { Id = "pa",   En = "Pascal (Pa)",      Zh = "帕斯卡 (Pa)", Factor = 1.0 },
                new UnitDef { Id = "kpa",  En = "Kilopascal (kPa)", Zh = "千帕 (kPa)",  Factor = 1000.0 },
                new UnitDef { Id = "bar",  En = "Bar",              Zh = "巴 (bar)",    Factor = 100000.0 },
                new UnitDef { Id = "atm",  En = "Atmosphere (atm)", Zh = "標準大氣壓 (atm)", Factor = 101325.0 },
                new UnitDef { Id = "psi",  En = "PSI",              Zh = "磅／平方吋 (psi)", Factor = 6894.757293168 },
                new UnitDef { Id = "mmhg", En = "mmHg (Torr)",      Zh = "毫米汞柱 (mmHg)",  Factor = 133.322387415 },
            },
        },
    };

    public static UnitCategory? Category(string id) => Categories.FirstOrDefault(c => c.Id == id);

    /// <summary>Convert <paramref name="value"/> from one unit to another in the same category.</summary>
    public static double Convert(double value, UnitDef from, UnitDef to)
    {
        var baseValue = value * from.Factor + from.Offset;          // → base unit
        return (baseValue - to.Offset) / to.Factor;                 // base → target
    }
}
