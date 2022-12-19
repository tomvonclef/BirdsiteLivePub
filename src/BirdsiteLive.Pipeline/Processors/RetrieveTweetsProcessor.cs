using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Pipeline.Contracts;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Pipeline.Processors;

public class RetrieveTweetsProcessor : IRetrieveTweetsProcessor
{
    private readonly ITwitterTweetsService _twitterTweetsService;
    private readonly ICachedTwitterUserService _twitterUserService;
    private readonly ITwitterUserDal _twitterUserDal;
    private readonly ILogger<RetrieveTweetsProcessor> _logger;

    #region Ctor
    public RetrieveTweetsProcessor(ITwitterTweetsService twitterTweetsService, ITwitterUserDal twitterUserDal, ICachedTwitterUserService twitterUserService, ILogger<RetrieveTweetsProcessor> logger)
    {
        _twitterTweetsService = twitterTweetsService;
        _twitterUserDal = twitterUserDal;
        _twitterUserService = twitterUserService;
        _logger = logger;
    }
    #endregion

    public async Task<UserWithDataToSync[]> ProcessAsync(UserWithDataToSync[] syncTwitterUsers, CancellationToken ct)
    {
        var usersWtTweets = new List<UserWithDataToSync>();

        //TODO multithread this
        foreach (var userWtData in syncTwitterUsers)
        {
            var user = userWtData.User;
            var tweets = RetrieveNewTweets(user);
            if (tweets.Length > 0 && user.LastTweetPostedId != -1)
            {
                userWtData.Tweets = tweets;
                usersWtTweets.Add(userWtData);
            }
            else if (tweets.Length > 0 && user.LastTweetPostedId == -1)
            {
                long tweetId = tweets.Last().Id;
                var now = DateTime.UtcNow;
                await _twitterUserDal.UpdateTwitterUserAsync(user.Id, tweetId, tweetId, user.FetchingErrorCount, now);
            }
            else
            {
                var now = DateTime.UtcNow;
                await _twitterUserDal.UpdateTwitterUserAsync(user.Id, user.LastTweetPostedId, user.LastTweetSynchronizedForAllFollowersId, user.FetchingErrorCount, now);
            }
        }

        return usersWtTweets.ToArray();
    }

    private ExtractedTweet[] RetrieveNewTweets(SyncTwitterUser user)
    {
        try
        {
            return user.LastTweetPostedId == -1 
                ? _twitterTweetsService.GetTimeline(user.Acct, 1) 
                : _twitterTweetsService.GetTimeline(user.Acct, 200, user.LastTweetSynchronizedForAllFollowersId);
        }
        catch (Exception e)
        {
            var errorMsg = "Error retrieving TL of {Username} from {LastTweetPostedId}, purging user from cache";
            _logger.LogError(e, errorMsg, user.Acct, user.LastTweetPostedId);
            _twitterUserService.PurgeUser(user.Acct);
        }

        return Array.Empty<ExtractedTweet>();
    }
}