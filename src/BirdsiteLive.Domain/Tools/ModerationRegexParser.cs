using System;
using System.Text.RegularExpressions;
using BirdsiteLive.Domain.Repository;
using Org.BouncyCastle.Pkcs;

namespace BirdsiteLive.Domain.Tools
{
    public static class ModerationRegexParser
    {
        public static Regex Parse(ModerationEntityTypeEnum type, string data)
        {
            data = data.ToLowerInvariant().Trim();

            if (type != ModerationEntityTypeEnum.Follower)
                return new Regex($@"^{data}$");

            if (data.StartsWith("@"))
                return new Regex($@"^{data}$");

            if (data.StartsWith("*"))
                data = data.Replace("*", "(.+)");

            return new Regex($@"^@(.+)@{data}$");

        }
    }
}