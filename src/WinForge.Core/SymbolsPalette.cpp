#include "SymbolsPalette.h"

#include <algorithm>
#include <array>
#include <cwctype>

namespace winforge::core::symbols
{
    namespace
    {
        constexpr std::array<SymbolCategory, 9> kSymbolsCategories{
        SymbolCategory{ L"arrows", L"Arrows", L"\u7BAD\u5634" },
        SymbolCategory{ L"math", L"Math", L"\u6578\u5B78" },
        SymbolCategory{ L"currency", L"Currency", L"\u8CA8\u5E63" },
        SymbolCategory{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE" },
        SymbolCategory{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD" },
        SymbolCategory{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA" },
        SymbolCategory{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE" },
        SymbolCategory{ L"fractions", L"Fractions", L"\u5206\u6578" },
        SymbolCategory{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19" },
        };

        constexpr std::array<SymbolEntry, 226> kSymbolsEntries{
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2190", L"Left arrow", L"\u5DE6\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2192", L"Right arrow", L"\u53F3\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2191", L"Up arrow", L"\u4E0A\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2193", L"Down arrow", L"\u4E0B\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2194", L"Left-right arrow", L"\u5DE6\u53F3\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2195", L"Up-down arrow", L"\u4E0A\u4E0B\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2196", L"Up-left arrow", L"\u5DE6\u4E0A\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2197", L"Up-right arrow", L"\u53F3\u4E0A\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2198", L"Down-right arrow", L"\u53F3\u4E0B\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2199", L"Down-left arrow", L"\u5DE6\u4E0B\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21D0", L"Double left", L"\u96D9\u5DE6\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21D2", L"Double right", L"\u96D9\u53F3\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21D1", L"Double up", L"\u96D9\u4E0A\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21D3", L"Double down", L"\u96D9\u4E0B\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21D4", L"Double left-right", L"\u96D9\u5DE6\u53F3\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u27F5", L"Long left", L"\u9577\u5DE6\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u27F6", L"Long right", L"\u9577\u53F3\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21A9", L"Return left", L"\u56DE\u8F49\u5DE6" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21AA", L"Return right", L"\u56DE\u8F49\u53F3" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2934", L"Arrow up-right curve", L"\u4E0A\u7FF9\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u2935", L"Arrow down-right curve", L"\u4E0B\u7FF9\u7BAD\u5634" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21BB", L"Clockwise", L"\u9806\u6642\u91DD" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u21BA", L"Anticlockwise", L"\u9006\u6642\u91DD" },
        SymbolEntry{ L"arrows", L"Arrows", L"\u7BAD\u5634", L"\u279C", L"Heavy arrow", L"\u7C97\u7BAD\u5634" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u00B1", L"Plus-minus", L"\u6B63\u8CA0" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u00D7", L"Times", L"\u4E58" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u00F7", L"Divide", L"\u9664" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2211", L"Summation", L"\u7E3D\u548C" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u220F", L"Product", L"\u9023\u4E58" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u222B", L"Integral", L"\u7A4D\u5206" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2202", L"Partial", L"\u504F\u5FAE\u5206" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2207", L"Nabla", L"\u68AF\u5EA6\u7B97\u5B50" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u221A", L"Square root", L"\u6839\u865F" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u221B", L"Cube root", L"\u7ACB\u65B9\u6839" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2260", L"Not equal", L"\u5514\u7B49\u65BC" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2248", L"Approx", L"\u7D04\u7B49\u65BC" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2261", L"Identical", L"\u6046\u7B49" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2264", L"Less-equal", L"\u7D30\u904E\u6216\u7B49" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2265", L"Greater-equal", L"\u5927\u904E\u6216\u7B49" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u221E", L"Infinity", L"\u7121\u9650" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2208", L"Element of", L"\u5C6C\u65BC" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2209", L"Not element", L"\u5514\u5C6C\u65BC" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2282", L"Subset", L"\u5B50\u96C6" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2286", L"Subset-equal", L"\u5B50\u96C6\u6216\u7B49" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u222A", L"Union", L"\u806F\u96C6" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2229", L"Intersection", L"\u4EA4\u96C6" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2200", L"For all", L"\u5C0D\u65BC\u6240\u6709" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2203", L"Exists", L"\u5B58\u5728" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2205", L"Empty set", L"\u7A7A\u96C6" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u221D", L"Proportional", L"\u6210\u6BD4\u4F8B" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2220", L"Angle", L"\u89D2" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u00B0", L"Degree", L"\u5EA6" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u00B5", L"Micro", L"\u5FAE" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u03C0", L"Pi", L"\u5713\u5468\u7387" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2234", L"Therefore", L"\u6240\u4EE5" },
        SymbolEntry{ L"math", L"Math", L"\u6578\u5B78", L"\u2235", L"Because", L"\u56E0\u70BA" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"$", L"Dollar", L"\u7F8E\u5143" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20AC", L"Euro", L"\u6B50\u5143" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u00A3", L"Pound", L"\u82F1\u938A" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u00A5", L"Yen / Yuan", L"\u65E5\u5713\uFF0F\u4EBA\u6C11\u5E63" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20BF", L"Bitcoin", L"\u6BD4\u7279\u5E63" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20A9", L"Won", L"\u97D3\u571C" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20B9", L"Rupee", L"\u5370\u5EA6\u76E7\u6BD4" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20BD", L"Ruble", L"\u4FC4\u7F85\u65AF\u76E7\u5E03" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20B4", L"Hryvnia", L"\u70CF\u514B\u862D\u683C\u91CC\u592B\u7D0D" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20AB", L"Dong", L"\u8D8A\u5357\u76FE" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20B1", L"Peso", L"\u83F2\u5F8B\u8CD3\u62AB\u7D22" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20A1", L"Colon", L"\u54E5\u65AF\u9054\u9ECE\u52A0\u79D1\u6717" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20AA", L"Shekel", L"\u4EE5\u8272\u5217\u8B1D\u514B\u723E" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20AD", L"Kip", L"\u8001\u64BE\u57FA\u666E" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20AE", L"Tugrik", L"\u8499\u53E4\u5716\u683C\u91CC\u514B" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20A6", L"Naira", L"\u5948\u53CA\u5229\u4E9E\u5948\u62C9" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u00A2", L"Cent", L"\u4ED9" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20B2", L"Guarani", L"\u74DC\u62C9\u5C3C" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\u20BA", L"Lira", L"\u571F\u8033\u5176\u91CC\u62C9" },
        SymbolEntry{ L"currency", L"Currency", L"\u8CA8\u5E63", L"\uFDFC", L"Rial", L"\u91CC\u4E9E\u723E" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2026", L"Ellipsis", L"\u7701\u7565\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2014", L"Em dash", L"\u7834\u6298\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2013", L"En dash", L"\u9023\u63A5\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00AB", L"Left guillemet", L"\u5DE6\u66F8\u540D\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00BB", L"Right guillemet", L"\u53F3\u66F8\u540D\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u201E", L"Low quote", L"\u4F4E\u5F15\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u201C", L"Left double quote", L"\u5DE6\u96D9\u5F15\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u201D", L"Right double quote", L"\u53F3\u96D9\u5F15\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2018", L"Left single quote", L"\u5DE6\u55AE\u5F15\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2019", L"Right single quote", L"\u53F3\u55AE\u5F15\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2022", L"Bullet", L"\u5713\u9EDE" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00B7", L"Middle dot", L"\u9593\u9694\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2020", L"Dagger", L"\u528D\u6A19" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2021", L"Double dagger", L"\u96D9\u528D\u6A19" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00A7", L"Section", L"\u7AE0\u7BC0\u7B26" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00B6", L"Pilcrow", L"\u6BB5\u843D\u7B26" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00A9", L"Copyright", L"\u7248\u6B0A" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00AE", L"Registered", L"\u8A3B\u518A\u5546\u6A19" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2122", L"Trademark", L"\u5546\u6A19" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u2030", L"Per mille", L"\u5343\u5206\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00A1", L"Inverted !", L"\u5012\u611F\u5606\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u00BF", L"Inverted ?", L"\u5012\u554F\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u301C", L"Wave dash", L"\u6CE2\u6D6A\u865F" },
        SymbolEntry{ L"punctuation", L"Punctuation", L"\u6A19\u9EDE", L"\u3000", L"Ideographic space", L"\u5168\u5F62\u7A7A\u683C" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B1", L"Alpha", L"\u963F\u723E\u6CD5" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B2", L"Beta", L"\u8C9D\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B3", L"Gamma", L"\u4F3D\u746A" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B4", L"Delta", L"\u5FB7\u723E\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B5", L"Epsilon", L"\u827E\u666E\u897F\u9F8D" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B6", L"Zeta", L"\u6FA4\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B7", L"Eta", L"\u4F0A\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B8", L"Theta", L"\u897F\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03B9", L"Iota", L"\u7D04\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03BA", L"Kappa", L"\u5361\u5E15" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03BB", L"Lambda", L"\u862D\u59C6\u9054" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03BC", L"Mu", L"\u7E46" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03BD", L"Nu", L"\u7D10" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03BE", L"Xi", L"\u514B\u897F" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C0", L"Pi", L"\u6D3E" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C1", L"Rho", L"\u67D4" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C3", L"Sigma", L"\u897F\u683C\u746A" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C4", L"Tau", L"\u9676" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C6", L"Phi", L"\u6590" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C7", L"Chi", L"\u5E0C" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C8", L"Psi", L"\u666E\u897F" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03C9", L"Omega (small)", L"\u7D30\u5BEB\u5967\u7C73\u52A0" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u0393", L"Gamma cap", L"\u5927\u5BEB\u4F3D\u746A" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u0394", L"Delta cap", L"\u5927\u5BEB\u5FB7\u723E\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u0398", L"Theta cap", L"\u5927\u5BEB\u897F\u5854" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u039B", L"Lambda cap", L"\u5927\u5BEB\u862D\u59C6\u9054" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03A0", L"Pi cap", L"\u5927\u5BEB\u6D3E" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03A3", L"Sigma cap", L"\u5927\u5BEB\u897F\u683C\u746A" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03A6", L"Phi cap", L"\u5927\u5BEB\u6590" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03A8", L"Psi cap", L"\u5927\u5BEB\u666E\u897F" },
        SymbolEntry{ L"greek", L"Greek", L"\u5E0C\u81D8\u5B57\u6BCD", L"\u03A9", L"Omega", L"\u5967\u7C73\u52A0" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2500", L"Horizontal", L"\u6A6B\u7DDA" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2502", L"Vertical", L"\u76F4\u7DDA" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u250C", L"Down-right", L"\u5DE6\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2510", L"Down-left", L"\u53F3\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2514", L"Up-right", L"\u5DE6\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2518", L"Up-left", L"\u53F3\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u251C", L"Vertical-right", L"\u5DE6T" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2524", L"Vertical-left", L"\u53F3T" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u252C", L"Down-horizontal", L"\u4E0AT" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2534", L"Up-horizontal", L"\u4E0BT" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u253C", L"Cross", L"\u5341\u5B57" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2550", L"Double horizontal", L"\u96D9\u6A6B\u7DDA" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2551", L"Double vertical", L"\u96D9\u76F4\u7DDA" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2554", L"Double down-right", L"\u96D9\u5DE6\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2557", L"Double down-left", L"\u96D9\u53F3\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u255A", L"Double up-right", L"\u96D9\u5DE6\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u255D", L"Double up-left", L"\u96D9\u53F3\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u256C", L"Double cross", L"\u96D9\u5341\u5B57" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u256D", L"Round down-right", L"\u5713\u5DE6\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u256E", L"Round down-left", L"\u5713\u53F3\u4E0A\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2570", L"Round up-right", L"\u5713\u5DE6\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u256F", L"Round up-left", L"\u5713\u53F3\u4E0B\u89D2" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2591", L"Light shade", L"\u6DFA\u9670\u5F71" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2592", L"Medium shade", L"\u4E2D\u9670\u5F71" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2593", L"Dark shade", L"\u6DF1\u9670\u5F71" },
        SymbolEntry{ L"box-drawing", L"Box Drawing", L"\u6846\u7DDA", L"\u2588", L"Full block", L"\u5BE6\u5FC3\u584A" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2605", L"Black star", L"\u5BE6\u5FC3\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2606", L"White star", L"\u7A7A\u5FC3\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2726", L"Four-point star", L"\u56DB\u89D2\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2727", L"White four-point", L"\u7A7A\u5FC3\u56DB\u89D2\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u272A", L"Circled star", L"\u5713\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u272F", L"Pinwheel star", L"\u98A8\u8ECA\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u274B", L"Heavy flower", L"\u82B1\u661F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25CF", L"Black circle", L"\u5BE6\u5FC3\u5713" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25CB", L"White circle", L"\u7A7A\u5FC3\u5713" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25C9", L"Fisheye", L"\u725B\u773C" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25C6", L"Black diamond", L"\u5BE6\u5FC3\u83F1" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25C7", L"White diamond", L"\u7A7A\u5FC3\u83F1" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25A0", L"Black square", L"\u5BE6\u5FC3\u65B9" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25A1", L"White square", L"\u7A7A\u5FC3\u65B9" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25AA", L"Small black square", L"\u7D30\u5BE6\u5FC3\u65B9" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25AB", L"Small white square", L"\u7D30\u7A7A\u5FC3\u65B9" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25B6", L"Play right", L"\u53F3\u4E09\u89D2" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25C0", L"Play left", L"\u5DE6\u4E09\u89D2" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25B2", L"Up triangle", L"\u4E0A\u4E09\u89D2" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u25BC", L"Down triangle", L"\u4E0B\u4E09\u89D2" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2714", L"Check", L"\u5254\u865F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2717", L"Cross mark", L"\u53C9\u865F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u271A", L"Heavy plus", L"\u7C97\u52A0\u865F" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2764", L"Heart", L"\u5FC3" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2611", L"Ballot check", L"\u5254\u683C" },
        SymbolEntry{ L"stars-bullets", L"Stars & Bullets", L"\u661F\u8207\u9EDE", L"\u2612", L"Ballot cross", L"\u53C9\u683C" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u00BD", L"One half", L"\u4E8C\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2153", L"One third", L"\u4E09\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2154", L"Two thirds", L"\u4E09\u5206\u4E8C" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u00BC", L"One quarter", L"\u56DB\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u00BE", L"Three quarters", L"\u56DB\u5206\u4E09" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2155", L"One fifth", L"\u4E94\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2156", L"Two fifths", L"\u4E94\u5206\u4E8C" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2157", L"Three fifths", L"\u4E94\u5206\u4E09" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2158", L"Four fifths", L"\u4E94\u5206\u56DB" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2159", L"One sixth", L"\u516D\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u215A", L"Five sixths", L"\u516D\u5206\u4E94" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u215B", L"One eighth", L"\u516B\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u215C", L"Three eighths", L"\u516B\u5206\u4E09" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u215D", L"Five eighths", L"\u516B\u5206\u4E94" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u215E", L"Seven eighths", L"\u516B\u5206\u4E03" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2150", L"One seventh", L"\u4E03\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2151", L"One ninth", L"\u4E5D\u5206\u4E00" },
        SymbolEntry{ L"fractions", L"Fractions", L"\u5206\u6578", L"\u2152", L"One tenth", L"\u5341\u5206\u4E00" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2070", L"Superscript 0", L"\u4E0A\u6A190" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u00B9", L"Superscript 1", L"\u4E0A\u6A191" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u00B2", L"Superscript 2", L"\u4E0A\u6A192" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u00B3", L"Superscript 3", L"\u4E0A\u6A193" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2074", L"Superscript 4", L"\u4E0A\u6A194" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2075", L"Superscript 5", L"\u4E0A\u6A195" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2076", L"Superscript 6", L"\u4E0A\u6A196" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2077", L"Superscript 7", L"\u4E0A\u6A197" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2078", L"Superscript 8", L"\u4E0A\u6A198" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2079", L"Superscript 9", L"\u4E0A\u6A199" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u207F", L"Superscript n", L"\u4E0A\u6A19n" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u207A", L"Superscript +", L"\u4E0A\u6A19\u52A0" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u207B", L"Superscript -", L"\u4E0A\u6A19\u6E1B" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2080", L"Subscript 0", L"\u4E0B\u6A190" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2081", L"Subscript 1", L"\u4E0B\u6A191" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2082", L"Subscript 2", L"\u4E0B\u6A192" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2083", L"Subscript 3", L"\u4E0B\u6A193" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2084", L"Subscript 4", L"\u4E0B\u6A194" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2085", L"Subscript 5", L"\u4E0B\u6A195" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2086", L"Subscript 6", L"\u4E0B\u6A196" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2087", L"Subscript 7", L"\u4E0B\u6A197" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2088", L"Subscript 8", L"\u4E0B\u6A198" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u2089", L"Subscript 9", L"\u4E0B\u6A199" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u208A", L"Subscript +", L"\u4E0B\u6A19\u52A0" },
        SymbolEntry{ L"super-subscript", L"Super / Subscript", L"\u4E0A\u4E0B\u6A19", L"\u208B", L"Subscript -", L"\u4E0B\u6A19\u6E1B" },
        };

        [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
        {
            while (!value.empty() && std::iswspace(static_cast<wint_t>(value.front())))
            {
                value.remove_prefix(1);
            }

            while (!value.empty() && std::iswspace(static_cast<wint_t>(value.back())))
            {
                value.remove_suffix(1);
            }

            return value;
        }

        [[nodiscard]] bool EqualsIgnoreCase(wchar_t left, wchar_t right) noexcept
        {
            return std::towlower(static_cast<wint_t>(left)) ==
                std::towlower(static_cast<wint_t>(right));
        }

        [[nodiscard]] bool ContainsIgnoreCase(
            std::wstring_view haystack,
            std::wstring_view needle) noexcept
        {
            return std::search(
                haystack.begin(),
                haystack.end(),
                needle.begin(),
                needle.end(),
                [](wchar_t left, wchar_t right)
                {
                    return EqualsIgnoreCase(left, right);
                }) != haystack.end();
        }

        [[nodiscard]] bool IsKnownCategory(std::wstring_view key) noexcept
        {
            return std::any_of(
                kSymbolsCategories.begin(),
                kSymbolsCategories.end(),
                [key](SymbolCategory const& category)
                {
                    return category.key == key;
                });
        }
    }

    std::span<SymbolCategory const> SymbolsCategories() noexcept
    {
        return kSymbolsCategories;
    }

    std::span<SymbolEntry const> SymbolsEntries() noexcept
    {
        return kSymbolsEntries;
    }

    bool SymbolsMatchesLiteral(
        SymbolEntry const& entry,
        std::wstring_view category_key,
        std::wstring_view query) noexcept
    {
        // The behavioral oracle falls back to all categories when an unknown
        // category is supplied; preserve that defensive, non-throwing result.
        if (!category_key.empty() && IsKnownCategory(category_key) &&
            entry.category_key != category_key)
        {
            return false;
        }

        auto const normalizedQuery = Trim(query);
        return normalizedQuery.empty() ||
            ContainsIgnoreCase(entry.glyph, normalizedQuery) ||
            ContainsIgnoreCase(entry.name_en, normalizedQuery) ||
            ContainsIgnoreCase(entry.name_zh, normalizedQuery);
    }
}
