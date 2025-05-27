using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SharedLogic;

namespace SharedLogicServer;

// This repository handles loading and saving user profiles in MongoDB.
public class UserProfileRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly GameCommandProcessor _processor;
    private readonly Type _profileType;

    public UserProfileRepository(IMongoClient client, GameCommandProcessor processor)
    {
        _processor = processor;
        // The profile type is defined by the dispatcher (plugin).
        _profileType = processor.CreateDefaultProfile(Guid.Empty).GetType();
        _collection = client.GetDatabase("game").GetCollection<BsonDocument>("profiles");
    }

    // Loads the user profile from the database by userId,
    // or creates a new default profile if none is found.
    public async Task<UserProfile> GetOrCreateAsync(Guid userId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", userId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync();
        if (doc != null)
        {
            var raw = new RawBsonDocument(doc.ToBson());
            return (UserProfile)BsonSerializer.Deserialize(raw, _profileType);
        }

        // Create a new profile using the dispatcher (plugin)
        return _processor.CreateDefaultProfile(userId);
    }

    // Saves the user profile to the database (upsert).
    public async Task SaveAsync(UserProfile profile)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", profile._id);
        var doc = profile.ToBsonDocument();
        await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
    }
}