using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 密語產生器 · Diceware-style passphrase generator. Pure managed C#, unbiased word choice via
/// <see cref="RandomNumberGenerator.GetInt32(int)"/>. No dice needed — picks from an embedded
/// curated word list. Never throws.
/// </summary>
public static class DicewareService
{
    /// <summary>How adjacent words are joined.</summary>
    public enum Separator { Space, Hyphen, Dot, None }

    /// <summary>The embedded word list (short, common, easy-to-type English words).</summary>
    public static IReadOnlyList<string> Words => _words;

    /// <summary>Number of distinct words available for selection.</summary>
    public static int WordCount => _words.Length;

    /// <summary>Bits of entropy per word = log2(listSize).</summary>
    public static double BitsPerWord => Math.Log2(_words.Length);

    /// <summary>
    /// Generate a single passphrase. All inputs are clamped to safe ranges; never throws.
    /// </summary>
    public static string Generate(int wordCount, Separator sep, bool capitalize, bool appendNumber)
    {
        try
        {
            wordCount = Math.Clamp(wordCount, 1, 64);
            var picked = new string[wordCount];
            for (int i = 0; i < wordCount; i++)
            {
                string w = _words[RandomNumberGenerator.GetInt32(_words.Length)];
                if (capitalize && w.Length > 0)
                    w = char.ToUpperInvariant(w[0]) + w.Substring(1);
                picked[i] = w;
            }

            string glue = sep switch
            {
                Separator.Space => " ",
                Separator.Hyphen => "-",
                Separator.Dot => ".",
                _ => ""
            };

            var sb = new StringBuilder(string.Join(glue, picked));
            if (appendNumber)
            {
                // Two-digit trailing number (10..99) — memorable but adds a little entropy.
                if (glue.Length > 0) sb.Append(glue);
                sb.Append(RandomNumberGenerator.GetInt32(10, 100));
            }
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Generate <paramref name="phrases"/> passphrases. Never throws; returns an empty list on error.</summary>
    public static List<string> GenerateMany(int phrases, int wordCount, Separator sep, bool capitalize, bool appendNumber)
    {
        var list = new List<string>();
        try
        {
            phrases = Math.Clamp(phrases, 1, 50);
            for (int i = 0; i < phrases; i++)
                list.Add(Generate(wordCount, sep, capitalize, appendNumber));
        }
        catch { /* never throw */ }
        return list;
    }

    /// <summary>Estimated entropy in bits: words * log2(listSize) + extras (~6.5 bits for a 2-digit number).</summary>
    public static double EstimateBits(int wordCount, bool appendNumber)
    {
        try
        {
            wordCount = Math.Clamp(wordCount, 1, 64);
            double bits = wordCount * BitsPerWord;
            if (appendNumber) bits += Math.Log2(90); // 10..99 → 90 possibilities
            return bits;
        }
        catch
        {
            return 0;
        }
    }

    // ~360 short, common English words. Curated to be easy to type and read; no offensive terms.
    private static readonly string[] _words =
    {
        "able","acid","acre","aged","also","army","atom","aunt","auto","away",
        "baby","back","bake","ball","band","bank","barn","base","bath","bead",
        "beam","bean","bear","beat","beef","bell","belt","bend","best","bike",
        "bird","bite","blue","boat","body","bold","bolt","bone","book","boot",
        "born","boss","both","bowl","brew","bulb","bull","burn","bush","busy",
        "cafe","cage","cake","calm","camp","cane","cape","card","care","cart",
        "case","cash","cast","cave","cell","chat","chef","chin","chip","city",
        "clam","clay","clip","clod","club","clue","coal","coat","code","coin",
        "cold","comb","cone","cook","cool","cope","copy","cord","core","cork",
        "corn","cost","crab","crew","crop","crow","cube","cult","curl","cute",
        "damp","dark","dash","data","date","dawn","deal","dear","deck","deed",
        "deep","deer","desk","dial","dice","dime","dine","dirt","dish","dock",
        "does","dome","done","door","dose","dove","down","drag","draw","drip",
        "drop","drum","dual","duck","dull","dune","dusk","dust","duty","each",
        "earn","east","easy","echo","edge","edit","exit","face","fact","fade",
        "fail","fair","fall","fame","farm","fast","fate","fawn","fear","feed",
        "feel","fern","file","fill","film","find","fine","fire","fish","fist",
        "five","flag","flat","flaw","flea","fled","flew","flip","flow","foam",
        "foil","fold","folk","fond","font","food","fool","foot","ford","fork",
        "form","fort","four","free","frog","fuel","full","fund","fuse","gain",
        "game","gate","gaze","gear","gift","girl","give","glad","glow","glue",
        "goal","goat","gold","golf","gone","good","gown","grab","gram","gray",
        "grew","grid","grim","grin","grip","grow","gulf","hail","hair","half",
        "hall","hand","hang","hard","harm","hawk","haze","head","heal","heap",
        "hear","heat","heel","held","herd","hero","hide","high","hike","hill",
        "hint","hive","hold","hole","holy","home","hook","hope","horn","host",
        "hour","huge","hull","hunt","hurt","hush","icon","idea","idle","inch",
        "iris","iron","isle","itch","item","jade","jail","jazz","jean","join",
        "joke","jolt","jump","june","jury","just","keen","keep","kelp","kept",
        "kick","kind","king","kiss","kite","knee","knew","knit","knob","knot",
        "know","lace","lack","lady","lake","lamb","lamp","land","lane","lark",
        "last","late","lawn","lazy","lead","leaf","lean","left","lend","lens",
        "less","life","lift","like","lime","limb","line","link","lion","list",
        "live","load","loaf","loan","lock","loft","logo","lone","long","look",
        "loop","lord","lose","loss","loud","love","luck","lump","lung","lush",
        "made","mail","main","make","male","mall","many","maple","mark","mask",
        "mass","mast","mate","math","maze","meal","mean","meat","meet","melt",
        "menu","mercy","mesh","mild","mile","milk","mill","mind","mine","mint",
        "mist","mode","mole","monk","mood","moon","moss","moth","move","much",
        "mule","muse","must","mute","nail","name","navy","near","neat","neck",
        "need","nest","news","next","nice","node","none","noon","norm","nose",
        "note","noun","oath","oats","open","oval","oven","over","pace","pack",
        "page","paid","pain","pair","pale","palm","park","part","pass","past",
        "path","peak","pear","peel","peer","pest","pick","pier","pile","pine",
        "pink","pint","pipe","plan","play","plot","plow","plug","plum","plus",
        "poem","poet","pole","poll","pond","pony","pool","pork","port","pose",
        "post","pour","pray","prep","prey","prop","pull","pump","pure","push",
        "quad","quiet","quit","quiz","race","rack","raft","rage","rail","rain",
        "rake","ramp","rank","rare","rate","read","real","reap","reed","reef",
        "rely","rent","rest","rice","rich","ride","ring","riot","ripe","rise",
        "risk","road","roam","roar","robe","rock","rode","role","roll","roof",
        "room","root","rope","rose","ruby","rule","runs","rush","rust","sack",
        "safe","sage","said","sail","salt","same","sand","sane","sang","sank",
        "save","scan","seal","seat","seed","seek","seem","seen","self","sell",
        "send","shed","ship","shoe","shop","shot","show","shut","side","sift",
        "sign","silk","sing","sink","site","size","skin","skip","slab","slam",
        "slap","sled","slid","slim","slip","slot","slow","snap","snow","soak",
        "soap","sock","soda","sofa","soft","soil","sold","sole","some","song",
        "soon","sort","soul","soup","sour","span","spin","spot","spur","stag",
        "star","stay","stem","step","stir","stop","stow","stub","stud","stun",
        "such","suit","sung","sunk","sure","surf","swam","swan","swap","swim",
        "tail","take","tale","talk","tall","tame","tank","tape","task","team",
        "tear","tell","tend","tent","term","test","text","than","that","thaw",
        "them","then","thin","this","thud","thumb","tick","tide","tidy","tile",
        "till","time","tiny","tint","tips","tire","toad","toast","toil","told",
        "toll","tomb","tone","took","tool","torn","tour","town","trap","tray",
        "tree","trek","trim","trip","true","tube","tuck","tug","tuna","tune",
        "turf","turn","twig","twin","type","ugly","undo","unit","upon","urge",
        "used","user","vain","vale","vane","vary","vase","vast","veil","vein",
        "verb","very","vest","vibe","view","vine","void","vote","wade","wage",
        "wait","wake","walk","wall","wand","want","ward","warm","warn","wash",
        "wave","wear","weed","week","weld","well","went","were","west","what",
        "when","whip","wick","wide","wife","wild","will","wind","wine","wing",
        "wink","wipe","wire","wise","wish","wolf","wood","wool","word","wore",
        "work","worm","wrap","wren","yard","yarn","yawn","year","yell","yoga",
        "yolk","zone","zoom"
    };
}
