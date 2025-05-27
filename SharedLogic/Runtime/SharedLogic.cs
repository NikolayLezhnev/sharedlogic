using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace SharedLogic
{
    // Abstract user profile. Does not contain game-specific data.
    public abstract class UserProfile
    {
        public Guid _id { get; set; }

        // After a command is executed on the client, the state hash is computed
        // and sent to the server.
        // The simplest way: serialize the entire state
        // and compute the hash from the serialized state.
        public virtual int ComputeHash()
        {
            var json = JsonConvert.SerializeObject(this);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(bytes);
            // Take the first 4 bytes as int
            return BitConverter.ToInt32(hash, 0);
        }
    }

    // Marker interface for all command data types.
    public interface ICommandData
    {
    }
    
    // Abstract command processor.
    // IMPORTANT: Do not add instance fields (state) here.
    // The class must be stateless — only store command handlers.
    // All fields must be static, const, or readonly.
    public abstract class GameCommandProcessor
    {
        // Delegate for command handling
        private delegate void CommandHandler(UserProfile profile, ICommandData command);
        
        // For each command type, store a reference to the handler delegate for that type
        private readonly Dictionary<Type, CommandHandler> _handlers = new Dictionary<Type, CommandHandler>();

        // Registers a handler for the specified command type.
        // Each handler receives arguments: player profile, command, list of game events.
        // This method is generic to simplify handler implementation — thus, concrete types
        // like ConnectFourProfile and DropDiscCommand come into the handler.
        // Descendants of GameCommandProcessor register all their handlers in their constructor
        // by calling this method.
        protected void RegisterHandler<TProfile, TCommand>(Action<TProfile, TCommand> handler)
            where TCommand : ICommandData
            where TProfile : UserProfile
        {
            var type = typeof(TCommand);
            _handlers[type] = (profile, cmd) => handler((TProfile)profile, (TCommand)cmd);
        }

        public void ExecuteCommand(UserProfile profile, ICommandData command)
        {
            var commandType = command.GetType();
            if (_handlers.TryGetValue(commandType, out var handler))
            {
                handler(profile, command);
            }
            else
            {
                throw new Exception($"No handler registered for command type: {commandType.Name}");
            }
        }

        // This method initializes and returns a default user profile
        public abstract UserProfile CreateDefaultProfile(Guid userId);
    }
    
    // Client request for command execution.
    public class ExecuteCommandRequestDTO
    {
        // The command can be any type, but must implement ICommandData.
        // To deserialize it, we need to know the command type — we store it in a separate field.
        public string CommandType { get; set; } = default!;
        // The command itself is here
        public object CommandData { get; set; } = default!;
        // The user's profile hash immediately after executing this command
        public int StateHash { get; set; }
    }
}
