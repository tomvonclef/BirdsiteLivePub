using System.Collections.Generic;
using System.Linq;
using BirdsiteLive.DAL.Models;

namespace BSLManager.Domain;

public class FollowersListState
{
    private readonly List<string> _filteredDisplayableUserList = new();

    private List<Follower> _sourceUserList = new();
    private List<Follower> _filteredSourceUserList = new();
        
    public void Load(IEnumerable<Follower> followers)
    {
        _sourceUserList = followers.OrderByDescending(x => x.Followings.Count).ToList();
        ResetLists();
    }

    private void ResetLists()
    {
        _filteredSourceUserList = _sourceUserList.ToList();
        _filteredDisplayableUserList.Clear();

        string FormatFollower(Follower follower) =>
            $"{GetFullHandle(follower)}   ({follower.Followings.Count})  (err:{follower.PostingErrorCount})";

        foreach (string displayedUser in _sourceUserList.Select(FormatFollower))
        {
            _filteredDisplayableUserList.Add(displayedUser);
        }
    }

    public List<string> GetDisplayableList() => _filteredDisplayableUserList;

    public void FilterBy(string pattern)
    {
        ResetLists();

        if (string.IsNullOrWhiteSpace(pattern)) return;
        var elToRemove = _filteredSourceUserList
            .Where(x => !GetFullHandle(x).Contains(pattern))
            .Select(x => x)
            .ToList();

        foreach (var el in elToRemove)
        {
            _filteredSourceUserList.Remove(el);
                    
            var dElToRemove = _filteredDisplayableUserList.First(x => x.Contains(GetFullHandle(el)));
            _filteredDisplayableUserList.Remove(dElToRemove);
        }
    }

    private static string GetFullHandle(Follower follower) => $"@{follower.Acct}@{follower.Host}";

    public void RemoveAt(int index)
    {
        string displayableUser = _filteredDisplayableUserList[index];
        Follower sourceUser = _filteredSourceUserList[index];

        _filteredDisplayableUserList.Remove(displayableUser);

        _filteredSourceUserList.Remove(sourceUser);
        _sourceUserList.Remove(sourceUser);
    }

    public Follower GetElementAt(int index) => _filteredSourceUserList[index];
}