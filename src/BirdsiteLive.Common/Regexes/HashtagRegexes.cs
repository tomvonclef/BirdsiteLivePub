using System.Text.RegularExpressions;

namespace BirdsiteLive.Common.Regexes;

public class HashtagRegexes
{
    public static readonly Regex HashtagName = new(@"^[a-zA-Z0-9_]+$");
    public static readonly Regex Hashtag = new(@"(.?)#([a-zA-Z0-9_]+)(\s|$|[\[\]<>.,;:!?/|-])");
}