using System.Text.RegularExpressions;

namespace BirdsiteLive.Common.Regexes;

public class UserRegexes
{
    public static readonly Regex TwitterAccount = new(@"^[a-zA-Z0-9_]+$");
    public static readonly Regex Mention = new(@"(.?)@([a-zA-Z0-9_]+)(\s|$|[\[\]<>,;:!?/|-]|(. ))");
}