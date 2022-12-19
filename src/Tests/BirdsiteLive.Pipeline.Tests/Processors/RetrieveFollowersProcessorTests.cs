﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Pipeline.Processors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Pipeline.Tests.Processors;

[TestClass]
public class RetrieveFollowersProcessorTests
{
    [TestMethod]
    public async Task ProcessAsync_Test()
    {
        #region Stubs
        const int userId1 = 1;
        const int userId2 = 2;

        var users = new List<UserWithDataToSync>
        {
            new()
            {
                User = new SyncTwitterUser
                {
                    Id = userId1
                }
            },
            new()
            {
                User = new SyncTwitterUser
                {
                    Id = userId2
                }
            }
        };

        var followersUser1 = new List<Follower>
        {
            new(),
            new(),
        };
        var followersUser2 = new List<Follower>
        {
            new(),
            new(),
            new(),
        };
        #endregion

        #region Mocks
        var followersDalMock = new Mock<IFollowersDal>(MockBehavior.Strict);
        followersDalMock
            .Setup(x => x.GetFollowersAsync(It.Is<int>(y => y == userId1)))
            .ReturnsAsync(followersUser1.ToArray());

        followersDalMock
            .Setup(x => x.GetFollowersAsync(It.Is<int>(y => y == userId2)))
            .ReturnsAsync(followersUser2.ToArray());
        #endregion

        var processor = new RetrieveFollowersProcessor(followersDalMock.Object);
        var result = (await processor.ProcessAsync(users.ToArray(), CancellationToken.None)).ToList();

        #region Validations
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(2, result.First(x => x.User.Id == userId1).Followers.Length);
        Assert.AreEqual(3, result.First(x => x.User.Id == userId2).Followers.Length);

        followersDalMock.VerifyAll();
        #endregion
    }
}