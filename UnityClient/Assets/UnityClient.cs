using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SpecificGamePlugin; // Plugin with ConnectFourDispatcher, ConnectFourProfile, etc.
using Newtonsoft.Json;
using SharedLogic;

// Unity client for the Connect Four game.
// For simplicity, everything is in one file and displayed in a primitive way using OnGUI.
public class UnityClient : MonoBehaviour
{
    [Header("API Settings")]
    // When you start the backend, check which port it is running on (not necessarily 5000).
    // Specify the correct port in the inspector.
    public string serverUrl = "http://localhost:5000/api/command";

    private Dictionary<GameResult, string> _resultToStatus = new Dictionary<GameResult, string>
    {
        { GameResult.Defeat , "💀 Defeat."},
        { GameResult.Win, "🏆 Win!"},
        { GameResult.Draw, "Draw"},
        { GameResult.InProgress, "Make your move"}
    };

    // Attempt to retrieve the current user's id from PlayerPrefs.
    // If nothing is saved, generate a new unique Guid (i.e., a new unique user).
    // When requesting a non-existing id, the backend will create a new profile and return it.
    private string UserId
    {
        get
        {
            var uid = PlayerPrefs.GetString("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid))
            {
                uid = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserId", uid);
            }
            return uid;
        }
    }

    // The player profile is needed to display the state and available actions.
    private ConnectFourProfile _profile;

    // The command dispatcher contains handlers for all possible commands.
    private ConnectFourCommandProcessor _dispatcher = new ConnectFourCommandProcessor();
    
    [Header("UI Textures")]
    [Tooltip("Texture used to render player and computer discs on the game board. Without this texture, the discs will not be visible!")]
    public Texture2D circleTexture;

    void Start()
    {
        Debug.Assert(circleTexture != null, 
            "circleTexture is not assigned! Please assign a texture in the inspector. " +
            "Without a texture, player and computer discs will not be visible on the game board."
        );
        
        //in real app we can show some PopUps for those events
        _dispatcher.OnVictory += () => Debug.Log("Victory!");
        _dispatcher.OnDefeat += () => Debug.Log("Defeat!");
        _dispatcher.OnDraw += () => Debug.Log("Draw!");

        // Load the player's profile from the backend and store it in _profile.
        StartCoroutine(LoadProfile());
    }

    void OnGUI()
    {
        // Scale the UI so elements are not too small on phones or 4K monitors
        float scaleX = Screen.width / 400.0f;
        float scaleY = Screen.height / 300.0f;
        float scale = Mathf.Min(scaleX, scaleY);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        // If profile is null, we are still loading it
        if (_profile == null)
        {
            GUILayout.Label("Loading profile...");
            return;
        }

        GUILayout.BeginVertical("box");

        GUILayout.Label(_resultToStatus[_profile.Result]);
        
        // Top row — status and new game button
        GUILayout.BeginHorizontal();

        if (_profile.Result == GameResult.InProgress)
        {
            // Show 7 drop buttons, one for every column
            for (int col = 0; col < 7; col++)
            {
                if (GUILayout.Button("↓", GUILayout.Width(30)))
                {
                    ExecuteCommand(new DropDiscCommand { Column = col });
                }
            }
        }
        else
        {
            if (GUILayout.Button("New Game"))
            {
                ExecuteCommand(new NewGameCommand());
            }
        }
        GUILayout.EndHorizontal();

        // Display game board
        for (int row = 0; row < 6; row++)
        {
            GUILayout.BeginHorizontal();
            for (int col = 0; col < 7; col++)
            {
                var cell = _profile.Board[row, col];
                Color color = cell switch
                {
                    CellState.Player => Color.yellow,
                    CellState.Computer => Color.red,
                    _ => Color.white
                };
                var oldColor = GUI.color;
                GUI.color = color;
                GUILayout.Label(new GUIContent(circleTexture), GUILayout.Width(30), GUILayout.Height(30));
                GUI.color = oldColor;
            }
            GUILayout.EndHorizontal();
        }

        // Statistics: victories / defeats
        GUILayout.Label($"Victory: {_profile.VictoryCount} | Defeat: {_profile.DefeatCount}");

        GUILayout.EndVertical();
    }

    // The code below can be reused for other games:
    // - Loading the user profile from the server
    // - Executing a command locally, calculating the state hash, and sending the command to the server
    IEnumerator LoadProfile()
    {
        string url = serverUrl + "/" + UserId;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var raw = request.downloadHandler.text;
                try
                {
                    _profile = JsonConvert.DeserializeObject<ConnectFourProfile>(raw);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            else
            {
                Debug.LogError("Error loading user profile: " + request.error);
            }
        }
    }

    void ExecuteCommand<TCommandType>(TCommandType command) where TCommandType : ICommandData
    {
        StartCoroutine(ExecuteCommandCoroutine(command));
    }

    IEnumerator ExecuteCommandCoroutine<TCommandType>(TCommandType command) where TCommandType : ICommandData
    {
        // Local execution
        try
        {
            _dispatcher.ExecuteCommand(_profile, command);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            yield break;
        }

        // Send to server
        var requestData = new ExecuteCommandRequestDTO
        {
            CommandType = typeof(TCommandType).Name,
            CommandData = command,
            StateHash = _profile.ComputeHash()
        };

        string json = JsonConvert.SerializeObject(requestData);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-User-Id", UserId);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Command error: " + request.error + ": " + request.downloadHandler.text);
            }
        }
    }
}
