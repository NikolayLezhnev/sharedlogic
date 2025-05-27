using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Newtonsoft.Json;
using SharedLogic;
using SharedLogicServer;

// Create web application builder
var builder = WebApplication.CreateBuilder(args);

// Load the game plugin. This is a .dll containing game-specific code.
// We don't want to rewrite and rebuild the backend for every game,
// so we simply drop in SpecificGamePlugin.dll with a class that inherits from CommandDispatcher.
var pluginPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "SpecificGamePlugin.dll");

if (!File.Exists(pluginPath))
    throw new FileNotFoundException("Plugin not found", pluginPath);

var pluginAssembly = Assembly.LoadFrom(pluginPath);

// Find the class that inherits from CommandDispatcher
var dispatcherType = pluginAssembly
    .GetTypes()
    .FirstOrDefault(t => typeof(GameCommandProcessor).IsAssignableFrom(t) && !t.IsAbstract);

if (dispatcherType == null)
    throw new Exception("No CommandDispatcher implementation found in plugin assembly.");



//cache all possible command types
var commandTypes = pluginAssembly
    .GetTypes()
    .Where(x => typeof(ICommandData).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract)
    .ToDictionary(x => x.Name, x => x);

builder.Services.AddSingleton<IReadOnlyDictionary<string, Type>>(commandTypes);

// The user profile uses the _id (Guid) field as the user identifier.
// We use MongoDB to store profiles, so tell it how to serialize this Guid.
BsonSerializer.RegisterSerializer(typeof(Guid), new GuidSerializer(GuidRepresentation.Standard));

// Configure the MongoDB driver. It's only used in UserProfileRepository.
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Mongo") ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});

// Instantiate the CommandDispatcher
var dispatcher = (GameCommandProcessor)Activator.CreateInstance(dispatcherType)!;

// Register created instances in the DI container for further use
builder.Services.AddSingleton(dispatcher);
builder.Services.AddSingleton<UserProfileRepository>();

// Configure controllers to use Newtonsoft.JSON:
// - This is the simplest and most illustrative option for demonstration.
// - It works fine with AOT on iOS and in il2cpp.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.TypeNameHandling = TypeNameHandling.None;
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    });

// Swagger is useful for debugging, lets you test requests from the browser without the Unity client.
builder.Services.AddSwaggerGen();

// Start the configured application. From this point, it will listen to HTTP requests and process them.
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
