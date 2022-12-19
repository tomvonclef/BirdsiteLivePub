using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BirdsiteLive.Twitter.Models;
using Tweetinvi.Models;
using Tweetinvi.Models.Entities;

namespace BirdsiteLive.Twitter.Extractors
{
    public interface ITweetExtractor
    {
        ExtractedTweet Extract(ITweet tweet);
    }

    public class TweetExtractor : ITweetExtractor
    {
        public ExtractedTweet Extract(ITweet tweet)
        {
            var extractedTweet = new ExtractedTweet
            {
                Id = tweet.Id,
                InReplyToStatusId = tweet.InReplyToStatusId,
                InReplyToAccount = tweet.InReplyToScreenName,
                MessageContent = ExtractMessage(tweet),
                Media = ExtractMedia(tweet),
                CreatedAt = tweet.CreatedAt.ToUniversalTime(),
                IsReply = tweet.InReplyToUserId != null,
                IsThread = tweet.InReplyToUserId != null && tweet.InReplyToUserId == tweet.CreatedBy.Id,
                IsRetweet = tweet.IsRetweet || tweet.QuotedStatusId != null,
                RetweetUrl = ExtractRetweetUrl(tweet)
            };

            return extractedTweet;
        }

        private string ExtractRetweetUrl(ITweet tweet)
        {
            if (!tweet.IsRetweet)
                return null;
            if (tweet.RetweetedTweet != null)
                return tweet.RetweetedTweet.Url;
            if (!tweet.FullText.Contains("https://t.co/"))
                return null;
            var retweetId = tweet.FullText.Split(new[] { "https://t.co/" },
                StringSplitOptions.RemoveEmptyEntries).Last();
            return $"https://t.co/{retweetId}";

        }

        private static string ExtractMessage(ITweet tweet)
        {
            string message = tweet.FullText;
            var tweetUrls = tweet.Media.Select(x => x.URL).Distinct();
            
            if (tweet.IsRetweet && message.StartsWith("RT") && tweet.RetweetedTweet != null)
            {
                message = tweet.RetweetedTweet.FullText;
                tweetUrls = tweet.RetweetedTweet.Media.Select(x => x.URL).Distinct();
            }

            foreach (string tweetUrl in tweetUrls)
            {
                message = tweet.IsRetweet 
                    ? tweet.RetweetedTweet?.FullText.Replace(tweetUrl, string.Empty).Trim() 
                    : message?.Replace(tweetUrl, string.Empty).Trim();
            }

            if (tweet.QuotedTweet != null) message = $"[Quote {{RT}}]{Environment.NewLine}{message}";
            if (tweet.IsRetweet)
            {
                if (tweet.RetweetedTweet != null && message != null && !message.StartsWith("RT"))
                    message = $"[{{RT}} @{tweet.RetweetedTweet.CreatedBy.ScreenName}]{Environment.NewLine}{message}";
                else if (tweet.RetweetedTweet != null && message != null && message.StartsWith($"RT @{tweet.RetweetedTweet.CreatedBy.ScreenName}:"))
                    message = message.Replace($"RT @{tweet.RetweetedTweet.CreatedBy.ScreenName}:", $"[{{RT}} @{tweet.RetweetedTweet.CreatedBy.ScreenName}]{Environment.NewLine}");
                else
                    message = message?.Replace("RT", "[{{RT}}]");
            }

            // Expand URLs
            foreach (var url in tweet.Urls.OrderByDescending(x => x.URL.Length))
                message = message?.Replace(url.URL, url.ExpandedURL);

            return message;
        }

        private static ExtractedMedia[] ExtractMedia(ITweet tweet)
        {
            var media = tweet.Media;
            if (tweet.IsRetweet && tweet.RetweetedTweet != null)
                media = tweet.RetweetedTweet.Media;

            var result = new List<ExtractedMedia>();
            foreach (var m in media)
            {
                string mediaUrl = GetMediaUrl(m);
                string mediaType = GetMediaType(m.MediaType, mediaUrl);
                if (mediaType == null) continue;

                var att = new ExtractedMedia
                {
                    MediaType = mediaType,
                    Url = mediaUrl,
                };
                result.Add(att);
            }

            return result.ToArray();
        }

        private static string GetMediaUrl(IMediaEntity media) =>
            media.MediaType switch
            {
                "photo" => media.MediaURLHttps,
                "animated_gif" => media.VideoDetails.Variants[0].URL,
                "video" => media.VideoDetails.Variants.OrderByDescending(x => x.Bitrate).First().URL,
                _ => null
            };

        private static string GetMediaType(string mediaType, string mediaUrl) =>
            mediaType switch
            {
                "photo" => Path.GetExtension(mediaUrl) switch
                {
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => null
                },
                "animated_gif" => Path.GetExtension(mediaUrl) switch
                {
                    ".gif" => "image/gif",
                    ".mp4" => "video/mp4",
                    _ => "image/gif"
                },
                "video" => "video/mp4",
                _ => null
            };
    }
}