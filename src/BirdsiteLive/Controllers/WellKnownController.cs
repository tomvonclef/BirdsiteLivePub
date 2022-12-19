using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.Common.Regexes;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Models.WellKnownModels;
using BirdsiteLive.Twitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Controllers;

[ApiController]
public class WellKnownController : ControllerBase
{
    private readonly IModerationRepository _moderationRepository;
    private readonly ITwitterUserService _twitterUserService;
    private readonly ITwitterUserDal _twitterUserDal;
    private readonly InstanceSettings _settings;
    private readonly ILogger<WellKnownController> _logger;
        
    #region Ctor
    public WellKnownController(InstanceSettings settings, ITwitterUserService twitterUserService, ITwitterUserDal twitterUserDal, IModerationRepository moderationRepository, ILogger<WellKnownController> logger)
    {
        _twitterUserService = twitterUserService;
        _twitterUserDal = twitterUserDal;
        _moderationRepository = moderationRepository;
        _logger = logger;
        _settings = settings;
    }
    #endregion

    [Route("/.well-known/nodeinfo")]
    public IActionResult WellKnownNodeInfo()
    {
        var nodeInfo = new WellKnownNodeInfo
        {
            links = new Link[]
            {
                new()
                {
                    rel = "http://nodeinfo.diaspora.software/ns/schema/2.0",
                    href = $"https://{_settings.Domain}/nodeinfo/2.0.json"
                },
                new()
                {
                    rel = "http://nodeinfo.diaspora.software/ns/schema/2.1",
                    href = $"https://{_settings.Domain}/nodeinfo/2.1.json"
                }
            }
        };
        return new JsonResult(nodeInfo);
    }

    [Route("/nodeinfo/{id}.json")]
    public async Task<IActionResult> NodeInfo(string id)
    {
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        var twitterUsersCount = await _twitterUserDal.GetTwitterUsersCountAsync();
        var isOpenRegistration = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.Follower) != ModerationTypeEnum.WhiteListing;

        switch (id)
        {
            case "2.0":
            {
                var nodeInfo = new NodeInfoV20
                {
                    version = "2.0",
                    usage = new Usage()
                    {
                        localPosts = 0,
                        users = new Users()
                        {
                            total = twitterUsersCount
                        }
                    },
                    software = new Software()
                    {
                        name = "birdsitelive",
                        version = version
                    },
                    protocols = new[]
                    {
                        "activitypub"
                    },
                    openRegistrations = isOpenRegistration,
                    services = new Models.WellKnownModels.Services()
                    {
                        inbound = Array.Empty<object>(),
                        outbound = Array.Empty<object>()
                    },
                    metadata = new Metadata()
                    {
                        email = _settings.AdminEmail
                    }
                };
                return new JsonResult(nodeInfo);
            }
            case "2.1":
            {
                var nodeInfo = new NodeInfoV21
                {
                    version = "2.1",
                    usage = new Usage()
                    {
                        localPosts = 0,
                        users = new Users()
                        {
                            total = twitterUsersCount
                        }
                    },
                    software = new SoftwareV21()
                    {
                        name = "birdsitelive",
                        version = version,
                        repository = "https://github.com/NicolasConstant/BirdsiteLive"
                    },
                    protocols = new[]
                    {
                        "activitypub"
                    },
                    openRegistrations = isOpenRegistration,
                    services = new Models.WellKnownModels.Services()
                    {
                        inbound = Array.Empty<object>(),
                        outbound = Array.Empty<object>()
                    },
                    metadata = new Metadata()
                    {
                        email = _settings.AdminEmail
                    }
                };
                return new JsonResult(nodeInfo);
            }
            default:
                return NotFound();
        }
    }

    [Route("/.well-known/webfinger")]
    public IActionResult Webfinger(string resource = null)
    {
        if (string.IsNullOrWhiteSpace(resource)) 
            return BadRequest();

        string name;
        string domain = null;

        if (resource.StartsWith("acct:"))
        {
            string acct = resource.Split("acct:")[1].Trim();
            string[] splitAcct = acct.Split('@', StringSplitOptions.RemoveEmptyEntries);

            var atCount = acct.Count(x => x == '@');
            switch (atCount)
            {
                case 1 when acct.StartsWith('@'):
                    name = splitAcct[1];
                    break;
                case 1 or 2:
                    name = splitAcct[0];
                    domain = splitAcct[1];
                    break;
                default:
                    return BadRequest();
            }
        }
        else if (resource.StartsWith("https://"))
        {
            try
            {
                name = resource.Split('/').Last().Trim();
                domain = resource.Split("https://", StringSplitOptions.RemoveEmptyEntries)[0]
                    .Split('/')[0].Trim();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing {Resource}", resource);
                throw new NotImplementedException();
            }
        }
        else
        {
            _logger.LogError("Error parsing {Resource}", resource);
            throw new NotImplementedException();
        }

        // Ensure lowercase
        name = name.ToLowerInvariant();
        domain = domain?.ToLowerInvariant();

        // Ensure valid username 
        // https://help.twitter.com/en/managing-your-account/twitter-username-rules
        if (string.IsNullOrWhiteSpace(name) || !UserRegexes.TwitterAccount.IsMatch(name) || name.Length > 15 )
            return NotFound();

        if (!string.IsNullOrWhiteSpace(domain) && domain != _settings.Domain)
            return NotFound();

        try
        {
            _twitterUserService.GetUser(name);
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
        catch (Exception e)
        {
            _logger.LogError(e, "Exception getting {Name}", name);
            throw;
        }

        string actorUrl = UrlFactory.GetActorUrl(_settings.Domain, name);

        var result = new WebFingerResult
        {
            subject = $"acct:{name}@{_settings.Domain}",
            aliases = new[]
            {
                actorUrl
            },
            links = new List<WebFingerLink>
            {
                new()
                {
                    rel = "http://webfinger.net/rel/profile-page",
                    type = "text/html",
                    href = actorUrl,
                },
                new()
                {
                    rel = "self",
                    type = "application/activity+json",
                    href = actorUrl,
                }
            }
        };

        return new JsonResult(result);
    }
}