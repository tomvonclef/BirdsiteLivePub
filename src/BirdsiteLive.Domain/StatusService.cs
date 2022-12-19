using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Domain.Statistics;
using BirdsiteLive.Domain.Tools;
using BirdsiteLive.Twitter.Models;
using Tweetinvi.Models;
using Tweetinvi.Models.Entities;

namespace BirdsiteLive.Domain;

public interface IStatusService
{
    Note GetStatus(string username, ExtractedTweet tweet);
}

public class StatusService : IStatusService
{
    private readonly InstanceSettings _instanceSettings;
    private readonly IStatusExtractor _statusExtractor;
    private readonly IExtractionStatisticsHandler _statisticsHandler;
    private readonly IPublicationRepository _publicationRepository;

    #region Ctor
    public StatusService(InstanceSettings instanceSettings, IStatusExtractor statusExtractor, IExtractionStatisticsHandler statisticsHandler, IPublicationRepository publicationRepository)
    {
        _instanceSettings = instanceSettings;
        _statusExtractor = statusExtractor;
        _statisticsHandler = statisticsHandler;
        _publicationRepository = publicationRepository;
    }
    #endregion

    public Note GetStatus(string username, ExtractedTweet tweet)
    {
        var actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, username);
        var noteUrl = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, tweet.Id.ToString());

        var to = $"{actorUrl}/followers";

        var isUnlisted = _publicationRepository.IsUnlisted(username);
        var cc = Array.Empty<string>();
        if (isUnlisted)
            cc = new[] {"https://www.w3.org/ns/activitystreams#Public"};
            
        string summary = null;
        var sensitive = _publicationRepository.IsSensitive(username);
        if (sensitive)
            summary = "Potential Content Warning";

        (string content, Tag[] tags) = _statusExtractor.Extract(tweet.MessageContent);
        _statisticsHandler.ExtractedStatus(tags.Count(x => x.type == "Mention"));

        // Replace RT by a link
        if (content.Contains("{RT}") && tweet.IsRetweet)
        {
            if (string.IsNullOrWhiteSpace(tweet.RetweetUrl))
                content = content.Replace("{RT}", "RT");
            else
            {
                content = content.Replace("{RT}",
                    $@"<a href=""{tweet.RetweetUrl}"" rel=""nofollow noopener noreferrer"" target=""_blank"">RT</a>");
            }
        }

        string inReplyTo = tweet.InReplyToStatusId == default ? null
            : $"https://{_instanceSettings.Domain}/users/{tweet.InReplyToAccount.ToLowerInvariant()
                }/statuses/{tweet.InReplyToStatusId}";

        return new Note
        {
            id = noteUrl,

            published = tweet.CreatedAt.ToString("s") + "Z",
            url = noteUrl,
            attributedTo = actorUrl,

            inReplyTo = inReplyTo,

            to = new[] { to },
            cc = cc,

            sensitive = sensitive,
            summary = summary,
            content = $"<p>{content}</p>",
            attachment = Convert(tweet.Media),
            tag = tags,
        };
    }

    private static Attachment[] Convert(ExtractedMedia[] media) =>
        media == null
            ? Array.Empty<Attachment>()
            : media.Select(x => new Attachment
            {
                type = "Document",
                url = x.Url,
                mediaType = x.MediaType
            }).ToArray();
}