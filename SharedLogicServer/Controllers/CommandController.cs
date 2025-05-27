using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SharedLogic;
using SharedLogicServer;
using Swashbuckle.AspNetCore.Annotations;

// The API is very simple; it has only two endpoints:
// - Load a user profile (or create a new one if not found)
// - Execute a command that updates the user profile
[ApiController]
[Route("api/[controller]")]
public class CommandController : ControllerBase
{
    private readonly IReadOnlyDictionary<string, Type> _commandTypesByName;
    private readonly GameCommandProcessor _processor;
    private readonly UserProfileRepository _repository;

    public CommandController(GameCommandProcessor processor, UserProfileRepository repository, IReadOnlyDictionary<string, Type> commandTypesByName)
    {
        _processor = processor;
        _repository = repository;
        _commandTypesByName = commandTypesByName;
    }

    // Loads a user profile from the database, or creates a new one if the provided Guid does not exist in the database.
    [HttpGet("{userId}")]
    [SwaggerOperation(Summary = "Load or create user profile", Description = "Returns existing or new user state by ID")]
    public async Task<IActionResult> LoadProfile(Guid userId)
    {
        var profile = await _repository.GetOrCreateAsync(userId);
        return Ok(profile);
    }

    // Executes a command. First, it loads the user profile from the database, then executes the command handler,
    // which includes comparing the user's profile hash.
    // Finally, the updated profile is saved to the database and an OK status is returned to the client.
    // If any exception occurs during command execution, the client receives the exception message.
    [HttpPost]
    [SwaggerOperation(Summary = "Execute player command", Description = "Requires header 'X-User-Id: <guid>'")]
    public async Task<IActionResult> Execute(
        [FromHeader(Name = "X-User-Id")] Guid userId,
        [FromBody] ExecuteCommandRequestDTO requestDto)
    {
        var profile = await _repository.GetOrCreateAsync(userId);

        try
        {   
            if (!_commandTypesByName.TryGetValue(requestDto.CommandType, out var commandType))
                throw new Exception($"Unknown command type: {requestDto.CommandType}");

            var json = requestDto.CommandData?.ToString();
            if (string.IsNullOrEmpty(json))
                throw new Exception("Command data is null or empty");

            var obj = JsonConvert.DeserializeObject(json, commandType);
            if (!(obj is ICommandData command))
                throw new Exception($"Failed to deserialize {requestDto.CommandType} as ICommandData.");
         
            _processor.ExecuteCommand(profile, command);
            
            // If the hash is different after applying the command, treat this as a verification failure (except for zero hash, which means hash check is not requested)
            if (profile.ComputeHash() != requestDto.StateHash && requestDto.StateHash != 0)
            {
                throw new Exception($"States are different on client and server after applying command: {requestDto.CommandType} {requestDto.CommandData}");
            }
            
            await _repository.SaveAsync(profile);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
