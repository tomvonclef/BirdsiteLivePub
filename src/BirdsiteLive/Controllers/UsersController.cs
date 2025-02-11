﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Regexes;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain;
using BirdsiteLive.Models;
using BirdsiteLive.Tools;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace BirdsiteLive.Controllers;

public class UsersController : Controller
{
    private readonly ITwitterUserService _twitterUserService;
    private readonly ITwitterTweetsService _twitterTweetService;
    private readonly IUserService _userService;
    private readonly IStatusService _statusService;
    private readonly InstanceSettings _instanceSettings;
    private readonly ILogger<UsersController> _logger;

    #region Ctor
    public UsersController(ITwitterUserService twitterUserService, IUserService userService, IStatusService statusService, InstanceSettings instanceSettings, ITwitterTweetsService twitterTweetService, ILogger<UsersController> logger)
    {
        _twitterUserService = twitterUserService;
        _userService = userService;
        _statusService = statusService;
        _instanceSettings = instanceSettings;
        _twitterTweetService = twitterTweetService;
        _logger = logger;
    }
    #endregion

    [Route("/users")]
    public IActionResult Index()
    {
        var acceptHeaders = Request.Headers["Accept"];
        if (!acceptHeaders.Any()) return View("UserNotFound");
        if (acceptHeaders.First().Contains("application/activity+json")) return NotFound();
        return View("UserNotFound");
    }

    [Route("/@{id}")]
    [Route("/users/{id}")]
    [Route("/users/{id}/remote_follow")]
    public IActionResult Index(string id)
    {
        _logger.LogTrace("User Index: {Id}", id);

        id = id.Trim(' ', '@').ToLowerInvariant();

        TwitterUser user = null;
        var isSaturated = false;
        var notFound = false;

        // Ensure valid username 
        // https://help.twitter.com/en/managing-your-account/twitter-username-rules
        if (!string.IsNullOrWhiteSpace(id) && UserRegexes.TwitterAccount.IsMatch(id) && id.Length <= 15)
        {
            try
            {
                user = _twitterUserService.GetUser(id);
            }
            catch (UserNotFoundException)
            {
                notFound = true;
            }
            catch (UserHasBeenSuspendedException)
            {
                notFound = true;
            }
            catch (RateLimitExceededException)
            {
                isSaturated = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception getting {Id}", id);
                throw;
            }
        }
        else
        {
            notFound = true;
        }

        //var isSaturated = _twitterUserService.IsUserApiRateLimited();

        var acceptHeaders = Request.Headers["Accept"];
        if (acceptHeaders.Any())
        {
            var r = acceptHeaders.First();
            if (r.Contains("application/activity+json"))
            {
                if (isSaturated) return new ObjectResult("Too Many Requests") { StatusCode = 429 };
                if (notFound) return NotFound();
                var apUser = _userService.GetUser(user);
                var jsonApUser = JsonConvert.SerializeObject(apUser);
                return Content(jsonApUser, "application/activity+json; charset=utf-8");
            }
        }

        if (isSaturated) return View("ApiSaturated");
        if (notFound) return View("UserNotFound");

        var displayableUser = new DisplayTwitterUser
        {
            Name = user.Name,
            Description = user.Description,
            Acct = user.Acct.ToLowerInvariant(),
            Url = user.Url,
            ProfileImageUrl = user.ProfileImageUrl,
            Protected = user.Protected,

            InstanceHandle = $"@{user.Acct.ToLowerInvariant()}@{_instanceSettings.Domain}"
        };
        return View(displayableUser);
    }

    [Route("/@{id}/{statusId}")]
    [Route("/users/{id}/statuses/{statusId}")]
    public IActionResult Tweet(string id, string statusId)
    {
        StringValues acceptHeaders = Request.Headers["Accept"];

        if (!acceptHeaders.Any())
            return Redirect($"https://twitter.com/{id}/status/{statusId}");

        if (!acceptHeaders.First().Contains("application/activity+json"))
            return Redirect($"https://twitter.com/{id}/status/{statusId}");

        if (!long.TryParse(statusId, out long parsedStatusId))
            return NotFound();

        ExtractedTweet tweet = _twitterTweetService.GetTweet(parsedStatusId);
        if (tweet == null)
            return NotFound();

        //var user = _twitterService.GetUser(id);
        //if (user == null) return NotFound();

        Note status = _statusService.GetStatus(id, tweet);
        string jsonApUser = JsonConvert.SerializeObject(status);
        return Content(jsonApUser, "application/activity+json; charset=utf-8");

    }

    [Route("/users/{id}/inbox")]
    [HttpPost]
    public async Task<IActionResult> Inbox()
    {
        try
        {
            HttpRequest r = Request;
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogTrace("User Inbox: {Body}", body);
            //System.IO.File.WriteAllText($@"C:\apdebug\{Guid.NewGuid()}.json", body);

            var activity = ApDeserializer.ProcessActivity(body);
            var signature = r.Headers["Signature"].First();

            bool succeeded;
            switch (activity?.type)
            {
                case "Follow":
                {
                    succeeded = await _userService.FollowRequestedAsync(signature, r.Method, r.Path,
                        r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                        activity as ActivityFollow, body);
                    if (succeeded)
                        return Accepted();
                    return Unauthorized();
                }
                case "Undo":
                    if (activity is not ActivityUndoFollow) return Accepted();
                    succeeded = await _userService.UndoFollowRequestedAsync(signature, r.Method, r.Path,
                        r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                        activity as ActivityUndoFollow, body);
                    if (succeeded)
                        return Accepted();
                    return Unauthorized();

                case "Delete":
                {
                    succeeded = await _userService.DeleteRequestedAsync(signature, r.Method, r.Path,
                        r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                        activity as ActivityDelete, body);
                    if (succeeded)
                        return Accepted();
                    return Unauthorized();
                }
                default:
                    return Accepted();
            }
        }
        catch (FollowerIsGoneException)  //TODO: check if user in DB
        {
            return Accepted();
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
        catch (UserHasBeenSuspendedException)
        {
            return NotFound();
        }
        catch (RateLimitExceededException)
        {
            return new ObjectResult("Too Many Requests") { StatusCode = 429 };
        }
    }

    [Route("/users/{id}/followers")]
    [HttpGet]
    public IActionResult Followers(string id)
    {
        var r = Request.Headers["Accept"].First();
        if (!r.Contains("application/activity+json")) return NotFound();

        var followers = new Followers
        {
            id = $"https://{_instanceSettings.Domain}/users/{id}/followers"
        };
        var jsonApUser = JsonConvert.SerializeObject(followers);
        return Content(jsonApUser, "application/activity+json; charset=utf-8");
    }
}