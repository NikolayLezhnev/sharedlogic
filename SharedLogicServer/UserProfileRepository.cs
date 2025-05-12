using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SharedLogic;
using System.Collections.Concurrent;

namespace SharedLogicServer;

// This repository handles loading and saving user profiles in MongoDB.
public class UserProfileRepository : IDisposable
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly GameCommandProcessor _processor;
    private readonly Type _profileType;
    private readonly ConcurrentDictionary<Guid, UserProfile> _storage = new();
    private readonly ConcurrentDictionary<Guid, long> _lastWriteTicks = new();
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(15);
    private readonly Timer _flushTimer;


    public UserProfileRepository(IMongoClient client, GameCommandProcessor processor)
    {
        _processor = processor;
        _profileType = processor.CreateDefaultProfile(Guid.Empty).GetType();
        _collection = client.GetDatabase("game").GetCollection<BsonDocument>("profiles");
        _flushTimer = new Timer(async _ => await FlushAsync(), null, _flushInterval, _flushInterval);
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushAsync().GetAwaiter().GetResult();
    }

    public Task<UserProfile> GetOrCreateAsync(Guid userId)
    {
        if (_storage.TryGetValue(userId, out var cached))
            return Task.FromResult(cached);

        return LoadAndCacheAsync(userId);
    }

    private async Task<UserProfile> LoadAndCacheAsync(Guid userId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", userId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync();

        UserProfile profile;
        if (doc != null)
        {
            profile = (UserProfile)BsonSerializer.Deserialize(doc, _profileType);
        }
        else
        {
            profile = _processor.CreateDefaultProfile(userId);
        }

        _storage[userId] = profile;
        return profile;
    }

    public Task SaveAsync(UserProfile profile)
    {
        _storage[profile._id] = profile;
        _lastWriteTicks[profile._id] = Environment.TickCount64;
        return Task.CompletedTask;
    }

    private async Task FlushAsync()
    {
        var now = Environment.TickCount64;
        var toFlush = new List<(Guid, UserProfile)>();

        foreach (var kvp in _lastWriteTicks)
        {
            if (_storage.TryGetValue(kvp.Key, out var profile))
            {
                if (now - kvp.Value >= _flushInterval.TotalMilliseconds)
                {
                    toFlush.Add((kvp.Key, profile));
                }
            }
        }

        foreach (var (userId, profile) in toFlush)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", userId);
            var doc = profile.ToBsonDocument();
            await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            _lastWriteTicks.TryRemove(userId, out _);
        }
    }
}
