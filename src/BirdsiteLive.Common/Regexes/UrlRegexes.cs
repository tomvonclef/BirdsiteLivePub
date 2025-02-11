﻿using System.Text.RegularExpressions;

namespace BirdsiteLive.Common.Regexes;

public class UrlRegexes
{
    public static readonly Regex Url = new(
        @"(.?)(((http|ftp|https):\/\/)[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)");
}