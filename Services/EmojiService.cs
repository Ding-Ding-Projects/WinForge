using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// Emoji 選擇器 · Emoji picker data source. Pure managed C#: embeds ~300 common emoji, each with an
/// English name, a category and keywords. Filtering by category + free-text search is done in-memory.
/// No P/Invoke, no processes, no network. Bilingual chrome lives in the page.
/// </summary>
public static class EmojiService
{
    /// <summary>Category tags used for filtering (stable, language-neutral keys).</summary>
    public const string CatSmileys = "Smileys & Emotion";
    public const string CatPeople = "People";
    public const string CatAnimals = "Animals & Nature";
    public const string CatFood = "Food & Drink";
    public const string CatTravel = "Travel & Places";
    public const string CatActivities = "Activities";
    public const string CatObjects = "Objects";
    public const string CatSymbols = "Symbols";
    public const string CatFlags = "Flags";

    /// <summary>The ordered list of category keys (without the synthetic "All").</summary>
    public static readonly string[] Categories =
    {
        CatSmileys, CatPeople, CatAnimals, CatFood, CatTravel, CatActivities, CatObjects, CatSymbols, CatFlags
    };

    /// <summary>One emoji entry. Public props use classic {Binding}-friendly names.</summary>
    public sealed class EmojiItem
    {
        public string Emoji { get; }
        public string Name { get; }
        public string Keywords { get; }
        public string Category { get; }

        public EmojiItem(string emoji, string name, string category, string keywords = "")
        {
            Emoji = emoji;
            Name = name;
            Category = category;
            Keywords = keywords ?? string.Empty;
        }
    }

    private static readonly List<EmojiItem> _all = Build();

    /// <summary>All embedded emoji, in a stable order.</summary>
    public static IReadOnlyList<EmojiItem> All => _all;

    /// <summary>
    /// Filter by category (null/empty/"All" = every category) and a case-insensitive substring that
    /// matches the emoji itself, its English name or its keywords. Never throws.
    /// </summary>
    public static IReadOnlyList<EmojiItem> Filter(string? category, string? search)
    {
        try
        {
            IEnumerable<EmojiItem> q = _all;
            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(e => string.Equals(e.Category, category, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(e =>
                    e.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    e.Keywords.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    e.Emoji.Contains(s, StringComparison.Ordinal));
            }

            return q.ToList();
        }
        catch
        {
            return _all;
        }
    }

    private static List<EmojiItem> Build()
    {
        var list = new List<EmojiItem>();
        void Add(string emoji, string name, string cat, string kw = "") => list.Add(new EmojiItem(emoji, name, cat, kw));

        // --- Smileys & Emotion -------------------------------------------------
        Add("\U0001F600", "grinning face", CatSmileys, "smile happy");
        Add("\U0001F603", "grinning face with big eyes", CatSmileys, "smile happy");
        Add("\U0001F604", "grinning face with smiling eyes", CatSmileys, "smile happy");
        Add("\U0001F601", "beaming face", CatSmileys, "grin");
        Add("\U0001F606", "grinning squinting face", CatSmileys, "laugh");
        Add("\U0001F605", "grinning face with sweat", CatSmileys, "nervous");
        Add("\U0001F923", "rolling on the floor laughing", CatSmileys, "rofl lol");
        Add("\U0001F602", "face with tears of joy", CatSmileys, "laugh lol");
        Add("\U0001F642", "slightly smiling face", CatSmileys, "smile");
        Add("\U0001F643", "upside-down face", CatSmileys, "silly");
        Add("\U0001F609", "winking face", CatSmileys, "wink");
        Add("\U0001F60A", "smiling face with smiling eyes", CatSmileys, "blush happy");
        Add("\U0001F607", "smiling face with halo", CatSmileys, "angel innocent");
        Add("\U0001F970", "smiling face with hearts", CatSmileys, "love adore");
        Add("\U0001F60D", "smiling face with heart-eyes", CatSmileys, "love");
        Add("\U0001F929", "star-struck", CatSmileys, "wow amazed");
        Add("\U0001F618", "face blowing a kiss", CatSmileys, "kiss love");
        Add("\U0001F617", "kissing face", CatSmileys, "kiss");
        Add("\U0001F61A", "kissing face with closed eyes", CatSmileys, "kiss");
        Add("\U0001F60B", "face savoring food", CatSmileys, "yum tasty");
        Add("\U0001F61B", "face with tongue", CatSmileys, "silly");
        Add("\U0001F61C", "winking face with tongue", CatSmileys, "silly joke");
        Add("\U0001F92A", "zany face", CatSmileys, "crazy goofy");
        Add("\U0001F928", "face with raised eyebrow", CatSmileys, "skeptic suspicious");
        Add("\U0001F9D0", "face with monocle", CatSmileys, "inspect");
        Add("\U0001F913", "nerd face", CatSmileys, "geek glasses");
        Add("\U0001F60E", "smiling face with sunglasses", CatSmileys, "cool");
        Add("\U0001F929", "star struck", CatSmileys, "wow");
        Add("\U0001F914", "thinking face", CatSmileys, "hmm ponder");
        Add("\U0001F910", "zipper-mouth face", CatSmileys, "quiet secret");
        Add("\U0001F611", "expressionless face", CatSmileys, "blank");
        Add("\U0001F610", "neutral face", CatSmileys, "meh");
        Add("\U0001F636", "face without mouth", CatSmileys, "speechless");
        Add("\U0001F60F", "smirking face", CatSmileys, "smug");
        Add("\U0001F612", "unamused face", CatSmileys, "meh annoyed");
        Add("\U0001F644", "face with rolling eyes", CatSmileys, "eyeroll");
        Add("\U0001F62C", "grimacing face", CatSmileys, "awkward");
        Add("\U0001F925", "lying face", CatSmileys, "pinocchio liar");
        Add("\U0001F60C", "relieved face", CatSmileys, "calm");
        Add("\U0001F614", "pensive face", CatSmileys, "sad");
        Add("\U0001F62A", "sleepy face", CatSmileys, "tired");
        Add("\U0001F634", "sleeping face", CatSmileys, "zzz");
        Add("\U0001F637", "face with medical mask", CatSmileys, "sick mask");
        Add("\U0001F912", "face with thermometer", CatSmileys, "sick fever");
        Add("\U0001F915", "face with head-bandage", CatSmileys, "hurt");
        Add("\U0001F922", "nauseated face", CatSmileys, "sick");
        Add("\U0001F92E", "face vomiting", CatSmileys, "sick puke");
        Add("\U0001F927", "sneezing face", CatSmileys, "sick tissue");
        Add("\U0001F975", "hot face", CatSmileys, "heat sweat");
        Add("\U0001F976", "cold face", CatSmileys, "freezing");
        Add("\U0001F974", "woozy face", CatSmileys, "dizzy drunk");
        Add("\U0001F635", "dizzy face", CatSmileys, "ko");
        Add("\U0001F92F", "exploding head", CatSmileys, "mind blown");
        Add("\U0001F920", "cowboy hat face", CatSmileys, "yeehaw");
        Add("\U0001F973", "partying face", CatSmileys, "party celebrate");
        Add("\U0001F60F", "smirk", CatSmileys, "smug");
        Add("\U0001F614", "sad", CatSmileys, "down");
        Add("\U0001F615", "confused face", CatSmileys, "unsure");
        Add("\U0001F61F", "worried face", CatSmileys, "concern");
        Add("\U0001F641", "slightly frowning face", CatSmileys, "sad");
        Add("\U00002639\U0000FE0F", "frowning face", CatSmileys, "sad");
        Add("\U0001F62E", "face with open mouth", CatSmileys, "surprised wow");
        Add("\U0001F62F", "hushed face", CatSmileys, "surprised");
        Add("\U0001F632", "astonished face", CatSmileys, "shock");
        Add("\U0001F633", "flushed face", CatSmileys, "embarrassed");
        Add("\U0001F97A", "pleading face", CatSmileys, "puppy eyes");
        Add("\U0001F626", "frowning face with open mouth", CatSmileys, "distress");
        Add("\U0001F627", "anguished face", CatSmileys, "worried");
        Add("\U0001F628", "fearful face", CatSmileys, "scared");
        Add("\U0001F630", "anxious face with sweat", CatSmileys, "nervous");
        Add("\U0001F625", "sad but relieved face", CatSmileys, "phew");
        Add("\U0001F622", "crying face", CatSmileys, "cry tear");
        Add("\U0001F62D", "loudly crying face", CatSmileys, "sob cry");
        Add("\U0001F631", "face screaming in fear", CatSmileys, "scream");
        Add("\U0001F616", "confounded face", CatSmileys, "frustrated");
        Add("\U0001F623", "persevering face", CatSmileys, "struggle");
        Add("\U0001F61E", "disappointed face", CatSmileys, "sad");
        Add("\U0001F613", "downcast face with sweat", CatSmileys, "stress");
        Add("\U0001F629", "weary face", CatSmileys, "tired");
        Add("\U0001F62B", "tired face", CatSmileys, "exhausted");
        Add("\U0001F971", "yawning face", CatSmileys, "bored tired");
        Add("\U0001F624", "face with steam from nose", CatSmileys, "triumph angry");
        Add("\U0001F621", "pouting face", CatSmileys, "angry rage");
        Add("\U0001F620", "angry face", CatSmileys, "mad");
        Add("\U0001F92C", "face with symbols on mouth", CatSmileys, "swearing curse");
        Add("\U0001F608", "smiling face with horns", CatSmileys, "devil");
        Add("\U0001F47F", "angry face with horns", CatSmileys, "devil imp");
        Add("\U0001F480", "skull", CatSmileys, "dead");
        Add("\U0001F4A9", "pile of poo", CatSmileys, "poop");
        Add("\U0001F921", "clown face", CatSmileys, "circus");
        Add("\U0001F479", "ogre", CatSmileys, "monster");
        Add("\U0001F47B", "ghost", CatSmileys, "boo");
        Add("\U0001F47D", "alien", CatSmileys, "ufo");
        Add("\U0001F916", "robot", CatSmileys, "bot ai");
        Add("\U0001F63A", "grinning cat", CatSmileys, "cat smile");
        Add("\U0001F638", "grinning cat with smiling eyes", CatSmileys, "cat");
        Add("\U0001F63B", "smiling cat with heart-eyes", CatSmileys, "cat love");
        Add("\U0001F640", "weary cat", CatSmileys, "cat scared");
        Add("\U0001F63F", "crying cat", CatSmileys, "cat cry");
        Add("\U0001F648", "see-no-evil monkey", CatSmileys, "monkey");
        Add("\U0001F649", "hear-no-evil monkey", CatSmileys, "monkey");
        Add("\U0001F64A", "speak-no-evil monkey", CatSmileys, "monkey");
        Add("\U0001F498", "heart with arrow", CatSmileys, "cupid love");
        Add("\U00002764\U0000FE0F", "red heart", CatSmileys, "love");
        Add("\U0001F9E1", "orange heart", CatSmileys, "love");
        Add("\U0001F49B", "yellow heart", CatSmileys, "love");
        Add("\U0001F49A", "green heart", CatSmileys, "love");
        Add("\U0001F499", "blue heart", CatSmileys, "love");
        Add("\U0001F49C", "purple heart", CatSmileys, "love");
        Add("\U0001F5A4", "black heart", CatSmileys, "love");
        Add("\U0001F494", "broken heart", CatSmileys, "heartbreak");
        Add("\U0001F495", "two hearts", CatSmileys, "love");
        Add("\U0001F49E", "revolving hearts", CatSmileys, "love");
        Add("\U0001F4AF", "hundred points", CatSmileys, "100 perfect");
        Add("\U0001F4A5", "collision", CatSmileys, "boom bang");
        Add("\U0001F4A6", "sweat droplets", CatSmileys, "water");
        Add("\U0001F4A4", "zzz", CatSmileys, "sleep");

        // --- People / body -----------------------------------------------------
        Add("\U0001F44B", "waving hand", CatPeople, "hi bye hello");
        Add("\U0001F91A", "raised back of hand", CatPeople, "hand");
        Add("\U0001F590\U0000FE0F", "hand with fingers splayed", CatPeople, "hand");
        Add("\U0000270B", "raised hand", CatPeople, "stop hand");
        Add("\U0001F44C", "OK hand", CatPeople, "okay");
        Add("\U0001F90C", "pinched fingers", CatPeople, "italian");
        Add("\U0001F90F", "pinching hand", CatPeople, "small");
        Add("\U0000270C\U0000FE0F", "victory hand", CatPeople, "peace v");
        Add("\U0001F91E", "crossed fingers", CatPeople, "luck hope");
        Add("\U0001F91F", "love-you gesture", CatPeople, "ily");
        Add("\U0001F918", "sign of the horns", CatPeople, "rock");
        Add("\U0001F919", "call me hand", CatPeople, "shaka");
        Add("\U0001F448", "backhand index pointing left", CatPeople, "point");
        Add("\U0001F449", "backhand index pointing right", CatPeople, "point");
        Add("\U0001F446", "backhand index pointing up", CatPeople, "point");
        Add("\U0001F447", "backhand index pointing down", CatPeople, "point");
        Add("\U0000261D\U0000FE0F", "index pointing up", CatPeople, "point one");
        Add("\U0001F44D", "thumbs up", CatPeople, "like yes good");
        Add("\U0001F44E", "thumbs down", CatPeople, "dislike no bad");
        Add("\U0000270A", "raised fist", CatPeople, "power");
        Add("\U0001F44A", "oncoming fist", CatPeople, "punch bump");
        Add("\U0001F91B", "left-facing fist", CatPeople, "bump");
        Add("\U0001F91C", "right-facing fist", CatPeople, "bump");
        Add("\U0001F44F", "clapping hands", CatPeople, "clap applause");
        Add("\U0001F64C", "raising hands", CatPeople, "celebrate hooray");
        Add("\U0001F450", "open hands", CatPeople, "hug");
        Add("\U0001F932", "palms up together", CatPeople, "pray");
        Add("\U0001F91D", "handshake", CatPeople, "deal agree");
        Add("\U0001F64F", "folded hands", CatPeople, "pray thanks please");
        Add("\U0000270D\U0000FE0F", "writing hand", CatPeople, "write");
        Add("\U0001F485", "nail polish", CatPeople, "manicure");
        Add("\U0001F4AA", "flexed biceps", CatPeople, "muscle strong");
        Add("\U0001F9E0", "brain", CatPeople, "smart mind");
        Add("\U0001F440", "eyes", CatPeople, "look watch");
        Add("\U0001F441\U0000FE0F", "eye", CatPeople, "look");
        Add("\U0001F444", "mouth", CatPeople, "lips");
        Add("\U0001F445", "tongue", CatPeople, "taste");
        Add("\U0001F476", "baby", CatPeople, "child");
        Add("\U0001F9D2", "child", CatPeople, "kid");
        Add("\U0001F466", "boy", CatPeople, "kid");
        Add("\U0001F467", "girl", CatPeople, "kid");
        Add("\U0001F9D1", "person", CatPeople, "adult");
        Add("\U0001F468", "man", CatPeople, "male");
        Add("\U0001F469", "woman", CatPeople, "female");
        Add("\U0001F9D3", "older person", CatPeople, "elder");
        Add("\U0001F474", "old man", CatPeople, "grandpa");
        Add("\U0001F475", "old woman", CatPeople, "grandma");
        Add("\U0001F468\U0000200D\U0001F4BB", "man technologist", CatPeople, "developer coder");
        Add("\U0001F469\U0000200D\U0001F4BB", "woman technologist", CatPeople, "developer coder");
        Add("\U0001F477", "construction worker", CatPeople, "builder");
        Add("\U0001F482", "guard", CatPeople, "soldier");
        Add("\U0001F575\U0000FE0F", "detective", CatPeople, "spy");
        Add("\U0001F468\U0000200D\U0001F373", "man cook", CatPeople, "chef");
        Add("\U0001F469\U0000200D\U0001F373", "woman cook", CatPeople, "chef");
        Add("\U0001F477\U0000200D\U00002642\U0000FE0F", "man construction worker", CatPeople, "builder");
        Add("\U0001F934", "prince", CatPeople, "royal");
        Add("\U0001F478", "princess", CatPeople, "royal");
        Add("\U0001F385", "Santa Claus", CatPeople, "christmas");
        Add("\U0001F936", "Mrs. Claus", CatPeople, "christmas");
        Add("\U0001F9B8", "superhero", CatPeople, "hero");
        Add("\U0001F9B9", "supervillain", CatPeople, "villain");
        Add("\U0001F9D9", "mage", CatPeople, "wizard");
        Add("\U0001F9DA", "fairy", CatPeople, "magic");
        Add("\U0001F9DB", "vampire", CatPeople, "dracula");
        Add("\U0001F9DF", "zombie", CatPeople, "undead");
        Add("\U0001F486", "person getting massage", CatPeople, "spa relax");
        Add("\U0001F487", "person getting haircut", CatPeople, "salon");
        Add("\U0001F6B6", "person walking", CatPeople, "walk");
        Add("\U0001F3C3", "person running", CatPeople, "run");
        Add("\U0001F483", "woman dancing", CatPeople, "dance");
        Add("\U0001F57A", "man dancing", CatPeople, "dance");
        Add("\U0001F46B", "woman and man holding hands", CatPeople, "couple");
        Add("\U0001F46A", "family", CatPeople, "parents kids");
        Add("\U0001F5E3\U0000FE0F", "speaking head", CatPeople, "talk");
        Add("\U0001F464", "bust in silhouette", CatPeople, "user profile");
        Add("\U0001F465", "busts in silhouette", CatPeople, "users group");

        // --- Animals & Nature --------------------------------------------------
        Add("\U0001F436", "dog face", CatAnimals, "puppy pet");
        Add("\U0001F431", "cat face", CatAnimals, "kitten pet");
        Add("\U0001F42D", "mouse face", CatAnimals, "rodent");
        Add("\U0001F439", "hamster", CatAnimals, "pet");
        Add("\U0001F430", "rabbit face", CatAnimals, "bunny");
        Add("\U0001F98A", "fox", CatAnimals, "");
        Add("\U0001F43B", "bear", CatAnimals, "");
        Add("\U0001F43C", "panda", CatAnimals, "");
        Add("\U0001F428", "koala", CatAnimals, "");
        Add("\U0001F42F", "tiger face", CatAnimals, "");
        Add("\U0001F981", "lion", CatAnimals, "");
        Add("\U0001F42E", "cow face", CatAnimals, "");
        Add("\U0001F437", "pig face", CatAnimals, "");
        Add("\U0001F438", "frog", CatAnimals, "");
        Add("\U0001F435", "monkey face", CatAnimals, "");
        Add("\U0001F414", "chicken", CatAnimals, "hen");
        Add("\U0001F427", "penguin", CatAnimals, "");
        Add("\U0001F426", "bird", CatAnimals, "");
        Add("\U0001F424", "baby chick", CatAnimals, "");
        Add("\U0001F986", "duck", CatAnimals, "");
        Add("\U0001F985", "eagle", CatAnimals, "");
        Add("\U0001F989", "owl", CatAnimals, "");
        Add("\U0001F987", "bat", CatAnimals, "");
        Add("\U0001F43A", "wolf", CatAnimals, "");
        Add("\U0001F417", "boar", CatAnimals, "");
        Add("\U0001F434", "horse face", CatAnimals, "");
        Add("\U0001F984", "unicorn", CatAnimals, "");
        Add("\U0001F41D", "honeybee", CatAnimals, "bee");
        Add("\U0001F41B", "bug", CatAnimals, "caterpillar");
        Add("\U0001F98B", "butterfly", CatAnimals, "");
        Add("\U0001F40C", "snail", CatAnimals, "");
        Add("\U0001F41E", "lady beetle", CatAnimals, "ladybug");
        Add("\U0001F41C", "ant", CatAnimals, "");
        Add("\U0001F577\U0000FE0F", "spider", CatAnimals, "");
        Add("\U0001F422", "turtle", CatAnimals, "tortoise");
        Add("\U0001F40D", "snake", CatAnimals, "");
        Add("\U0001F98E", "lizard", CatAnimals, "");
        Add("\U0001F419", "octopus", CatAnimals, "");
        Add("\U0001F41F", "fish", CatAnimals, "");
        Add("\U0001F420", "tropical fish", CatAnimals, "");
        Add("\U0001F42C", "dolphin", CatAnimals, "");
        Add("\U0001F433", "spouting whale", CatAnimals, "");
        Add("\U0001F988", "shark", CatAnimals, "");
        Add("\U0001F40B", "whale", CatAnimals, "");
        Add("\U0001F980", "crab", CatAnimals, "");
        Add("\U0001F990", "shrimp", CatAnimals, "");
        Add("\U0001F419", "octopus alt", CatAnimals, "");
        Add("\U0001F418", "elephant", CatAnimals, "");
        Add("\U0001F98F", "rhinoceros", CatAnimals, "rhino");
        Add("\U0001F992", "giraffe", CatAnimals, "");
        Add("\U0001F998", "kangaroo", CatAnimals, "");
        Add("\U0001F406", "leopard", CatAnimals, "");
        Add("\U0001F993", "zebra", CatAnimals, "");
        Add("\U0001F40E", "horse", CatAnimals, "");
        Add("\U0001F416", "pig", CatAnimals, "");
        Add("\U0001F411", "ewe", CatAnimals, "sheep");
        Add("\U0001F410", "goat", CatAnimals, "");
        Add("\U0001F42B", "two-hump camel", CatAnimals, "");
        Add("\U0001F999", "llama", CatAnimals, "");
        Add("\U0001F995", "sauropod", CatAnimals, "dinosaur");
        Add("\U0001F996", "T-Rex", CatAnimals, "dinosaur");
        Add("\U0001F335", "cactus", CatAnimals, "plant");
        Add("\U0001F384", "Christmas tree", CatAnimals, "xmas");
        Add("\U0001F332", "evergreen tree", CatAnimals, "pine");
        Add("\U0001F333", "deciduous tree", CatAnimals, "tree");
        Add("\U0001F33F", "herb", CatAnimals, "leaf");
        Add("\U0001F340", "four leaf clover", CatAnimals, "luck");
        Add("\U0001F341", "maple leaf", CatAnimals, "autumn");
        Add("\U0001F344", "mushroom", CatAnimals, "fungus");
        Add("\U0001F337", "tulip", CatAnimals, "flower");
        Add("\U0001F338", "cherry blossom", CatAnimals, "sakura flower");
        Add("\U0001F339", "rose", CatAnimals, "flower");
        Add("\U0001F33B", "sunflower", CatAnimals, "flower");
        Add("\U0001F33C", "blossom", CatAnimals, "flower");
        Add("\U0001F490", "bouquet", CatAnimals, "flowers");
        Add("\U0001F30D", "globe showing Europe-Africa", CatAnimals, "earth world");
        Add("\U0001F31E", "sun with face", CatAnimals, "");
        Add("\U0001F319", "crescent moon", CatAnimals, "night");
        Add("\U00002B50", "star", CatAnimals, "");
        Add("\U00002728", "sparkles", CatAnimals, "shine glitter");
        Add("\U000026A1", "high voltage", CatAnimals, "lightning bolt");
        Add("\U0001F525", "fire", CatAnimals, "flame lit");
        Add("\U0001F308", "rainbow", CatAnimals, "");
        Add("\U00002600\U0000FE0F", "sun", CatAnimals, "sunny");
        Add("\U000026C5", "sun behind cloud", CatAnimals, "partly cloudy");
        Add("\U00002601\U0000FE0F", "cloud", CatAnimals, "cloudy");
        Add("\U0001F327\U0000FE0F", "cloud with rain", CatAnimals, "rain");
        Add("\U000026C4", "snowman without snow", CatAnimals, "winter");
        Add("\U00002744\U0000FE0F", "snowflake", CatAnimals, "snow cold");
        Add("\U0001F4A7", "droplet", CatAnimals, "water");
        Add("\U0001F30A", "water wave", CatAnimals, "ocean sea");

        // --- Food & Drink ------------------------------------------------------
        Add("\U0001F34F", "green apple", CatFood, "fruit");
        Add("\U0001F34E", "red apple", CatFood, "fruit");
        Add("\U0001F34A", "tangerine", CatFood, "orange fruit");
        Add("\U0001F34B", "lemon", CatFood, "fruit sour");
        Add("\U0001F34C", "banana", CatFood, "fruit");
        Add("\U0001F349", "watermelon", CatFood, "fruit");
        Add("\U0001F347", "grapes", CatFood, "fruit");
        Add("\U0001F353", "strawberry", CatFood, "fruit");
        Add("\U0001F352", "cherries", CatFood, "fruit");
        Add("\U0001F351", "peach", CatFood, "fruit");
        Add("\U0001F96D", "mango", CatFood, "fruit");
        Add("\U0001F34D", "pineapple", CatFood, "fruit");
        Add("\U0001F95D", "kiwi fruit", CatFood, "fruit");
        Add("\U0001F345", "tomato", CatFood, "vegetable");
        Add("\U0001F951", "avocado", CatFood, "");
        Add("\U0001F346", "eggplant", CatFood, "aubergine");
        Add("\U0001F955", "carrot", CatFood, "vegetable");
        Add("\U0001F33D", "ear of corn", CatFood, "maize");
        Add("\U0001F336\U0000FE0F", "hot pepper", CatFood, "chili spicy");
        Add("\U0001F966", "broccoli", CatFood, "vegetable");
        Add("\U0001F344", "mushroom food", CatFood, "");
        Add("\U0001F35E", "bread", CatFood, "loaf");
        Add("\U0001F950", "croissant", CatFood, "pastry");
        Add("\U0001F956", "baguette bread", CatFood, "");
        Add("\U0001F968", "pretzel", CatFood, "");
        Add("\U0001F9C0", "cheese wedge", CatFood, "");
        Add("\U0001F95A", "egg", CatFood, "");
        Add("\U0001F373", "cooking", CatFood, "fried egg");
        Add("\U0001F953", "bacon", CatFood, "");
        Add("\U0001F95E", "pancakes", CatFood, "");
        Add("\U0001F354", "hamburger", CatFood, "burger");
        Add("\U0001F35F", "french fries", CatFood, "fries");
        Add("\U0001F355", "pizza", CatFood, "slice");
        Add("\U0001F32D", "hot dog", CatFood, "");
        Add("\U0001F32E", "taco", CatFood, "");
        Add("\U0001F32F", "burrito", CatFood, "");
        Add("\U0001F35C", "steaming bowl", CatFood, "ramen noodles");
        Add("\U0001F35B", "curry rice", CatFood, "");
        Add("\U0001F363", "sushi", CatFood, "");
        Add("\U0001F359", "rice ball", CatFood, "onigiri");
        Add("\U0001F35A", "cooked rice", CatFood, "");
        Add("\U0001F368", "ice cream", CatFood, "dessert");
        Add("\U0001F366", "soft ice cream", CatFood, "");
        Add("\U0001F369", "doughnut", CatFood, "donut");
        Add("\U0001F36A", "cookie", CatFood, "biscuit");
        Add("\U0001F382", "birthday cake", CatFood, "");
        Add("\U0001F370", "shortcake", CatFood, "cake dessert");
        Add("\U0001F36B", "chocolate bar", CatFood, "");
        Add("\U0001F36C", "candy", CatFood, "sweet");
        Add("\U0001F36D", "lollipop", CatFood, "");
        Add("\U0001F36F", "honey pot", CatFood, "");
        Add("\U0001F37F", "popcorn", CatFood, "");
        Add("\U00002615", "hot beverage", CatFood, "coffee tea");
        Add("\U0001F375", "teacup without handle", CatFood, "green tea");
        Add("\U0001F37A", "beer mug", CatFood, "");
        Add("\U0001F37B", "clinking beer mugs", CatFood, "cheers");
        Add("\U0001F377", "wine glass", CatFood, "");
        Add("\U0001F378", "cocktail glass", CatFood, "martini");
        Add("\U0001F379", "tropical drink", CatFood, "");
        Add("\U0001F942", "clinking glasses", CatFood, "cheers");
        Add("\U0001F943", "tumbler glass", CatFood, "whisky");
        Add("\U0001F37E", "bottle with popping cork", CatFood, "champagne");
        Add("\U0001F964", "cup with straw", CatFood, "soda drink");
        Add("\U0001F9CB", "bubble tea", CatFood, "boba");

        // --- Travel & Places ---------------------------------------------------
        Add("\U0001F697", "automobile", CatTravel, "car");
        Add("\U0001F695", "taxi", CatTravel, "cab");
        Add("\U0001F699", "sport utility vehicle", CatTravel, "suv");
        Add("\U0001F68C", "bus", CatTravel, "");
        Add("\U0001F693", "police car", CatTravel, "");
        Add("\U0001F691", "ambulance", CatTravel, "");
        Add("\U0001F692", "fire engine", CatTravel, "");
        Add("\U0001F69A", "delivery truck", CatTravel, "");
        Add("\U0001F69C", "tractor", CatTravel, "farm");
        Add("\U0001F3CD\U0000FE0F", "motorcycle", CatTravel, "");
        Add("\U0001F6B2", "bicycle", CatTravel, "bike");
        Add("\U0001F6F4", "kick scooter", CatTravel, "");
        Add("\U0001F686", "train", CatTravel, "");
        Add("\U0001F684", "high-speed train", CatTravel, "bullet");
        Add("\U0001F687", "metro", CatTravel, "subway");
        Add("\U00002708\U0000FE0F", "airplane", CatTravel, "flight plane");
        Add("\U0001F681", "helicopter", CatTravel, "");
        Add("\U0001F680", "rocket", CatTravel, "space launch");
        Add("\U0001F6F8", "flying saucer", CatTravel, "ufo");
        Add("\U000026F5", "sailboat", CatTravel, "");
        Add("\U0001F6A4", "speedboat", CatTravel, "");
        Add("\U0001F6F3\U0000FE0F", "passenger ship", CatTravel, "cruise");
        Add("\U000026F4\U0000FE0F", "ferry", CatTravel, "");
        Add("\U00002693", "anchor", CatTravel, "");
        Add("\U0001F6A2", "ship", CatTravel, "");
        Add("\U0001F3E0", "house", CatTravel, "home");
        Add("\U0001F3E1", "house with garden", CatTravel, "home");
        Add("\U0001F3E2", "office building", CatTravel, "");
        Add("\U0001F3E5", "hospital", CatTravel, "");
        Add("\U0001F3E6", "bank", CatTravel, "");
        Add("\U0001F3E8", "hotel", CatTravel, "");
        Add("\U0001F3EB", "school", CatTravel, "");
        Add("\U0001F3ED", "factory", CatTravel, "");
        Add("\U0001F3F0", "castle", CatTravel, "");
        Add("\U0001F5FC", "Tokyo tower", CatTravel, "");
        Add("\U0001F5FD", "Statue of Liberty", CatTravel, "");
        Add("\U0001F5FA\U0000FE0F", "world map", CatTravel, "");
        Add("\U0001F5FB", "mount fuji", CatTravel, "mountain");
        Add("\U000026F0\U0000FE0F", "mountain", CatTravel, "");
        Add("\U0001F30B", "volcano", CatTravel, "");
        Add("\U0001F3D5\U0000FE0F", "camping", CatTravel, "tent");
        Add("\U0001F3D6\U0000FE0F", "beach with umbrella", CatTravel, "");
        Add("\U0001F3DC\U0000FE0F", "desert", CatTravel, "");
        Add("\U0001F3DD\U0000FE0F", "desert island", CatTravel, "");
        Add("\U0001F5FF", "moai", CatTravel, "statue");
        Add("\U0001F301", "foggy", CatTravel, "");
        Add("\U0001F303", "night with stars", CatTravel, "city");
        Add("\U0001F305", "sunrise", CatTravel, "");
        Add("\U0001F307", "sunset over buildings", CatTravel, "");
        Add("\U0001F309", "bridge at night", CatTravel, "");
        Add("\U0001F3A1", "ferris wheel", CatTravel, "");
        Add("\U0001F3A2", "roller coaster", CatTravel, "");
        Add("\U0001F3AA", "circus tent", CatTravel, "");
        Add("\U000026F2", "fountain", CatTravel, "");
        Add("\U0001F5FA", "map", CatTravel, "");

        // --- Activities --------------------------------------------------------
        Add("\U000026BD", "soccer ball", CatActivities, "football");
        Add("\U0001F3C0", "basketball", CatActivities, "");
        Add("\U0001F3C8", "american football", CatActivities, "");
        Add("\U000026BE", "baseball", CatActivities, "");
        Add("\U0001F3BE", "tennis", CatActivities, "");
        Add("\U0001F3D0", "volleyball", CatActivities, "");
        Add("\U0001F3C9", "rugby football", CatActivities, "");
        Add("\U0001F3B1", "pool 8 ball", CatActivities, "billiards");
        Add("\U0001F3D3", "ping pong", CatActivities, "table tennis");
        Add("\U0001F3F8", "badminton", CatActivities, "");
        Add("\U0001F3D2", "ice hockey", CatActivities, "");
        Add("\U0001F94A", "boxing glove", CatActivities, "");
        Add("\U0001F945", "goal net", CatActivities, "");
        Add("\U000026F3", "flag in hole", CatActivities, "golf");
        Add("\U0001F3F9", "bow and arrow", CatActivities, "archery");
        Add("\U0001F3A3", "fishing pole", CatActivities, "");
        Add("\U0001F3BF", "skis", CatActivities, "ski");
        Add("\U000026F8\U0000FE0F", "ice skate", CatActivities, "");
        Add("\U0001F6F9", "skateboard", CatActivities, "");
        Add("\U0001F3C6", "trophy", CatActivities, "winner award");
        Add("\U0001F3C5", "sports medal", CatActivities, "");
        Add("\U0001F947", "1st place medal", CatActivities, "gold");
        Add("\U0001F948", "2nd place medal", CatActivities, "silver");
        Add("\U0001F949", "3rd place medal", CatActivities, "bronze");
        Add("\U0001F3AF", "bullseye", CatActivities, "target dart");
        Add("\U0001F3AE", "video game", CatActivities, "controller gaming");
        Add("\U0001F579\U0000FE0F", "joystick", CatActivities, "");
        Add("\U0001F3B2", "game die", CatActivities, "dice");
        Add("\U00002660\U0000FE0F", "spade suit", CatActivities, "cards");
        Add("\U00002665\U0000FE0F", "heart suit", CatActivities, "cards");
        Add("\U00002666\U0000FE0F", "diamond suit", CatActivities, "cards");
        Add("\U00002663\U0000FE0F", "club suit", CatActivities, "cards");
        Add("\U0001F0CF", "joker", CatActivities, "card");
        Add("\U0001F3AD", "performing arts", CatActivities, "theater masks");
        Add("\U0001F3A8", "artist palette", CatActivities, "paint art");
        Add("\U0001F3AC", "clapper board", CatActivities, "movie film");
        Add("\U0001F3A4", "microphone", CatActivities, "sing karaoke");
        Add("\U0001F3A7", "headphone", CatActivities, "music");
        Add("\U0001F3B8", "guitar", CatActivities, "music");
        Add("\U0001F3B9", "musical keyboard", CatActivities, "piano");
        Add("\U0001F3BA", "trumpet", CatActivities, "music");
        Add("\U0001F3BB", "violin", CatActivities, "music");
        Add("\U0001F941", "drum", CatActivities, "music");
        Add("\U0001F3B5", "musical note", CatActivities, "music");
        Add("\U0001F3B6", "musical notes", CatActivities, "music");
        Add("\U0001F386", "fireworks", CatActivities, "celebrate");
        Add("\U0001F387", "sparkler", CatActivities, "");
        Add("\U0001F389", "party popper", CatActivities, "celebrate tada");
        Add("\U0001F38A", "confetti ball", CatActivities, "celebrate");
        Add("\U0001F380", "ribbon", CatActivities, "bow");
        Add("\U0001F381", "wrapped gift", CatActivities, "present");

        // --- Objects -----------------------------------------------------------
        Add("\U0001F4F1", "mobile phone", CatObjects, "smartphone");
        Add("\U0001F4BB", "laptop", CatObjects, "computer");
        Add("\U00002328\U0000FE0F", "keyboard", CatObjects, "");
        Add("\U0001F5A5\U0000FE0F", "desktop computer", CatObjects, "pc");
        Add("\U0001F5A8\U0000FE0F", "printer", CatObjects, "");
        Add("\U0001F5B1\U0000FE0F", "computer mouse", CatObjects, "");
        Add("\U0001F4BE", "floppy disk", CatObjects, "save");
        Add("\U0001F4BF", "optical disk", CatObjects, "cd");
        Add("\U0001F4C0", "dvd", CatObjects, "");
        Add("\U0001F3A5", "movie camera", CatObjects, "");
        Add("\U0001F4F7", "camera", CatObjects, "photo");
        Add("\U0001F4F8", "camera with flash", CatObjects, "");
        Add("\U0001F4FA", "television", CatObjects, "tv");
        Add("\U0001F4FB", "radio", CatObjects, "");
        Add("\U0000260E\U0000FE0F", "telephone", CatObjects, "phone");
        Add("\U0001F50B", "battery", CatObjects, "");
        Add("\U0001F50C", "electric plug", CatObjects, "power");
        Add("\U0001F4A1", "light bulb", CatObjects, "idea");
        Add("\U0001F526", "flashlight", CatObjects, "torch");
        Add("\U0001F56F\U0000FE0F", "candle", CatObjects, "");
        Add("\U0001F4D6", "open book", CatObjects, "read");
        Add("\U0001F4DA", "books", CatObjects, "library");
        Add("\U0001F4DD", "memo", CatObjects, "note write");
        Add("\U0000270F\U0000FE0F", "pencil", CatObjects, "write");
        Add("\U0001F4CE", "paperclip", CatObjects, "attach");
        Add("\U0001F4CC", "pushpin", CatObjects, "pin");
        Add("\U0001F4CD", "round pushpin", CatObjects, "location");
        Add("\U0001F4C5", "calendar", CatObjects, "date");
        Add("\U0001F4C8", "chart increasing", CatObjects, "graph up");
        Add("\U0001F4C9", "chart decreasing", CatObjects, "graph down");
        Add("\U0001F4CA", "bar chart", CatObjects, "graph");
        Add("\U0001F4B0", "money bag", CatObjects, "cash");
        Add("\U0001F4B5", "dollar banknote", CatObjects, "money");
        Add("\U0001F4B3", "credit card", CatObjects, "payment");
        Add("\U0001F4B8", "money with wings", CatObjects, "spend");
        Add("\U0001F48E", "gem stone", CatObjects, "diamond jewel");
        Add("\U0001F511", "key", CatObjects, "unlock");
        Add("\U0001F510", "locked with key", CatObjects, "secure");
        Add("\U0001F512", "locked", CatObjects, "lock secure");
        Add("\U0001F513", "unlocked", CatObjects, "open");
        Add("\U0001F528", "hammer", CatObjects, "tool");
        Add("\U0001F527", "wrench", CatObjects, "tool fix");
        Add("\U0001F529", "nut and bolt", CatObjects, "");
        Add("\U00002699\U0000FE0F", "gear", CatObjects, "settings cog");
        Add("\U0001F517", "link", CatObjects, "chain url");
        Add("\U0001F9F2", "magnet", CatObjects, "");
        Add("\U0001F9EA", "test tube", CatObjects, "science");
        Add("\U0001F52C", "microscope", CatObjects, "science");
        Add("\U0001F489", "syringe", CatObjects, "shot vaccine");
        Add("\U0001F48A", "pill", CatObjects, "medicine");
        Add("\U0001F6BF", "shower", CatObjects, "");
        Add("\U0001F6C1", "bathtub", CatObjects, "");
        Add("\U0001F9F9", "broom", CatObjects, "clean");
        Add("\U0001F6D2", "shopping cart", CatObjects, "trolley");
        Add("\U0001F6CE\U0000FE0F", "bellhop bell", CatObjects, "service");
        Add("\U0001F5D1\U0000FE0F", "wastebasket", CatObjects, "trash bin");
        Add("\U0001F4E6", "package", CatObjects, "box parcel");
        Add("\U0001F4E7", "e-mail", CatObjects, "email");
        Add("\U00002709\U0000FE0F", "envelope", CatObjects, "mail letter");
        Add("\U0001F4EE", "postbox", CatObjects, "mailbox");
        Add("\U0001F514", "bell", CatObjects, "notification");
        Add("\U0001F515", "bell with slash", CatObjects, "mute");
        Add("\U0001F3AB", "ticket", CatObjects, "");
        Add("\U0001F396\U0000FE0F", "military medal", CatObjects, "");
        Add("\U0001F52D", "telescope", CatObjects, "");
        Add("\U0000231B", "hourglass done", CatObjects, "time");
        Add("\U000023F0", "alarm clock", CatObjects, "time");
        Add("\U0000231A", "watch", CatObjects, "time");

        // --- Symbols -----------------------------------------------------------
        Add("\U00002714\U0000FE0F", "check mark", CatSymbols, "tick yes done");
        Add("\U00002705", "check mark button", CatSymbols, "yes done green");
        Add("\U0000274C", "cross mark", CatSymbols, "no x wrong");
        Add("\U0000274E", "cross mark button", CatSymbols, "no x");
        Add("\U00002795", "plus", CatSymbols, "add");
        Add("\U00002796", "minus", CatSymbols, "subtract");
        Add("\U00002797", "divide", CatSymbols, "");
        Add("\U00002716\U0000FE0F", "multiply", CatSymbols, "times x");
        Add("\U0000267E\U0000FE0F", "infinity", CatSymbols, "");
        Add("\U00002753", "question mark", CatSymbols, "help");
        Add("\U00002757", "exclamation mark", CatSymbols, "warning");
        Add("\U0000203C\U0000FE0F", "double exclamation mark", CatSymbols, "");
        Add("\U00002049\U0000FE0F", "exclamation question mark", CatSymbols, "");
        Add("\U000026A0\U0000FE0F", "warning", CatSymbols, "caution alert");
        Add("\U0001F6AB", "prohibited", CatSymbols, "no forbidden");
        Add("\U0001F4AF", "hundred", CatSymbols, "100");
        Add("\U0001F519", "back arrow", CatSymbols, "");
        Add("\U0001F51D", "top arrow", CatSymbols, "");
        Add("\U0001F51C", "soon arrow", CatSymbols, "");
        Add("\U00002B06\U0000FE0F", "up arrow", CatSymbols, "");
        Add("\U00002B07\U0000FE0F", "down arrow", CatSymbols, "");
        Add("\U00002B05\U0000FE0F", "left arrow", CatSymbols, "");
        Add("\U000027A1\U0000FE0F", "right arrow", CatSymbols, "");
        Add("\U0001F504", "counterclockwise arrows", CatSymbols, "refresh reload");
        Add("\U0001F503", "clockwise arrows", CatSymbols, "");
        Add("\U0001F500", "shuffle", CatSymbols, "random");
        Add("\U0001F501", "repeat", CatSymbols, "loop");
        Add("\U000025B6\U0000FE0F", "play button", CatSymbols, "start");
        Add("\U000023F8\U0000FE0F", "pause button", CatSymbols, "");
        Add("\U000023F9\U0000FE0F", "stop button", CatSymbols, "");
        Add("\U000023ED\U0000FE0F", "next track", CatSymbols, "skip");
        Add("\U000023EE\U0000FE0F", "last track", CatSymbols, "previous");
        Add("\U0001F50A", "speaker high volume", CatSymbols, "loud sound");
        Add("\U0001F507", "muted speaker", CatSymbols, "mute silence");
        Add("\U0001F4A2", "anger", CatSymbols, "mad");
        Add("\U0001F4AC", "speech balloon", CatSymbols, "chat talk");
        Add("\U0001F4AD", "thought balloon", CatSymbols, "think");
        Add("\U0001F5EF\U0000FE0F", "right anger bubble", CatSymbols, "");
        Add("\U00002B50", "star symbol", CatSymbols, "favorite");
        Add("\U0001F31F", "glowing star", CatSymbols, "");
        Add("\U00002734\U0000FE0F", "eight-pointed star", CatSymbols, "");
        Add("\U0001F195", "NEW button", CatSymbols, "");
        Add("\U0001F193", "FREE button", CatSymbols, "");
        Add("\U0001F197", "OK button", CatSymbols, "");
        Add("\U0001F51F", "keycap 10", CatSymbols, "ten");
        Add("\U00000023\U0000FE0F\U000020E3", "keycap number sign", CatSymbols, "hash");
        Add("\U0000267B\U0000FE0F", "recycling symbol", CatSymbols, "recycle");
        Add("\U0001F49F", "heart decoration", CatSymbols, "");
        Add("\U00002764\U0000FE0F\U0000200D\U0001F525", "heart on fire", CatSymbols, "burning love");
        Add("\U00002622\U0000FE0F", "radioactive", CatSymbols, "nuclear");
        Add("\U00002623\U0000FE0F", "biohazard", CatSymbols, "");
        Add("\U0000267F", "wheelchair symbol", CatSymbols, "accessible");
        Add("\U0001F6BB", "restroom", CatSymbols, "toilet");
        Add("\U0001F4F6", "antenna bars", CatSymbols, "signal");
        Add("\U0001F191", "SOS button", CatSymbols, "help");

        // --- Flags -------------------------------------------------------------
        Add("\U0001F3C1", "chequered flag", CatFlags, "race finish");
        Add("\U0001F6A9", "triangular flag", CatFlags, "");
        Add("\U0001F3F4", "black flag", CatFlags, "");
        Add("\U0001F3F3\U0000FE0F", "white flag", CatFlags, "surrender");
        Add("\U0001F3F3\U0000FE0F\U0000200D\U0001F308", "rainbow flag", CatFlags, "pride lgbt");
        Add("\U0001F3F4\U0000200D\U00002620\U0000FE0F", "pirate flag", CatFlags, "skull");
        Add("\U0001F1ED\U0001F1F0", "flag Hong Kong", CatFlags, "hk");
        Add("\U0001F1F9\U0001F1FC", "flag Taiwan", CatFlags, "tw");
        Add("\U0001F1E8\U0001F1F3", "flag China", CatFlags, "cn");
        Add("\U0001F1EF\U0001F1F5", "flag Japan", CatFlags, "jp");
        Add("\U0001F1F0\U0001F1F7", "flag South Korea", CatFlags, "kr");
        Add("\U0001F1FA\U0001F1F8", "flag United States", CatFlags, "us usa america");
        Add("\U0001F1EC\U0001F1E7", "flag United Kingdom", CatFlags, "uk gb britain");
        Add("\U0001F1E8\U0001F1E6", "flag Canada", CatFlags, "ca");
        Add("\U0001F1E6\U0001F1FA", "flag Australia", CatFlags, "au");
        Add("\U0001F1EB\U0001F1F7", "flag France", CatFlags, "fr");
        Add("\U0001F1E9\U0001F1EA", "flag Germany", CatFlags, "de");
        Add("\U0001F1EE\U0001F1F9", "flag Italy", CatFlags, "it");
        Add("\U0001F1EA\U0001F1F8", "flag Spain", CatFlags, "es");
        Add("\U0001F1F5\U0001F1F9", "flag Portugal", CatFlags, "pt");
        Add("\U0001F1F3\U0001F1F1", "flag Netherlands", CatFlags, "nl");
        Add("\U0001F1F7\U0001F1FA", "flag Russia", CatFlags, "ru");
        Add("\U0001F1E7\U0001F1F7", "flag Brazil", CatFlags, "br");
        Add("\U0001F1F2\U0001F1FD", "flag Mexico", CatFlags, "mx");
        Add("\U0001F1EE\U0001F1F3", "flag India", CatFlags, "in");
        Add("\U0001F1F8\U0001F1EC", "flag Singapore", CatFlags, "sg");
        Add("\U0001F1F9\U0001F1ED", "flag Thailand", CatFlags, "th");
        Add("\U0001F1FB\U0001F1F3", "flag Vietnam", CatFlags, "vn");
        Add("\U0001F1F5\U0001F1ED", "flag Philippines", CatFlags, "ph");
        Add("\U0001F1F2\U0001F1FE", "flag Malaysia", CatFlags, "my");
        Add("\U0001F1EE\U0001F1E9", "flag Indonesia", CatFlags, "id");

        return list;
    }
}
