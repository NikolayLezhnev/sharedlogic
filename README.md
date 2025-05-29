# Shared Logic Library

## Introduction

SharedLogic is an example of a hybrid client-server architecture for games, where player commands are executed instantly on the client for immediate feedback, but are always replayed on the server for verification. This approach protects against cheating, saves server resources, and speeds up development. All game rules and commands are implemented once and used both on the client (Unity) and the server.

> 💡 You can find a more detailed explanation in the [article](https://nikolaylezhnev.github.io/sharedlogic/).
## Solution Structure

- **SharedLogic** — Core library: defines base interfaces, command handlers, and the user profile.
- **SharedLogicServer** — Backend: .NET 8 Web API for storing user profiles and verifying commands.
- **SpecificGamePlugin** — Example game plugin: implements rules for a specific game (e.g., Connect Four).
- **UnityClient** — Sample Unity client that interacts with the server and executes commands locally.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed
- **MongoDB** service running locally (default: `mongodb://localhost:27017`)
- **Unity** installed (any version 2021+)

---

## Quick Start

You can run the server either via IDE (Rider/Visual Studio) or from the command line.

### Building and Running from IDE (Rider / Visual Studio)

1. Open the `SharedLogic.sln` file in Rider or Visual Studio.
2. Set **SharedLogicServer** as the startup project.
3. Run the server (`F5` or "Run").
4. After launch, Swagger UI should open at an address like:
   ```
   http://localhost:5114/swagger
   ```
   > ⚠️ The port may vary: check your console output or browser address bar.
  - In Visual Studio, it’s usually 5114; from the command line, it's often 5000.

**Example console output:**

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5114
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\Projects\SharedLogic\SharedLogicServer\bin\Debug\net8.0
```

Check the `Now listening on:` line to find the actual port and URL of your server.

---

### Building and Running from Command Line

1. Make sure you have [.NET 8+](https://dotnet.microsoft.com/download/dotnet/8.0) installed:
   ```sh
   dotnet --version
   ```
2. Open a terminal, navigate to the solution folder:
   ```sh
   cd path/to/SharedLogic
   ```
3. Build the solution:
   ```sh
   dotnet build
   ```
4. After successful build, run the server directly using the built executable:
  - **On Windows:**
    ```
    SharedLogicServer\bin\Debug\net8.0\SharedLogicServer.exe
    ```
  - **On Linux/Mac:**
    ```
    dotnet SharedLogicServer/bin/Debug/net8.0/SharedLogicServer.dll
    ```
5. Swagger UI will be available at a URL like `http://localhost:5000/swagger` or another port shown in the console.

---

## Testing the API in Swagger

1. Go to the Swagger UI (e.g., `http://localhost:5114/swagger` or `http://localhost:5000/swagger`).
2. Find the endpoint:
   ```
   GET /api/command/{userId}
   ```
3. Enter any GUID (generate one at [guidgenerator.com](https://www.guidgenerator.com/)) and click **Execute**.
4. If the profile with that ID doesn’t exist, the server will create a new one.

---

## Running the Unity Client

1. Open the Unity project from the `UnityClient` folder. You can use any Unity version newer than 2021.3.13f1
2. In the Inspector, ensure the active scene contains the `UnityClient` script and the **Server Url** field points to the correct server port (e.g., `http://localhost:5114/api/command`).
3. Make sure the backend server is running.
4. Press Play in Unity to start the client.

If everything is set up correctly, the Unity client will connect to the server, load a user profile, and let you interact with the game (e.g., play Connect Four).

---

## Integrating SharedLogic with Your Existing Project

### The simplest way (copy) 
**Create fork or copy of this repository**, modify **/UnityProject** and **/GamePlugin**  for your project needs.  

### The alternative way (submodule)
You can connect the SharedLogic library (or your fork of it) to multiple projects as a submodule, making it easy to reuse and update in all of them.
[Learn how to connect the library as a submodule →](https://nikolaylezhnev.github.io/sharedlogic/submodule)

## Useful Links

- [MongoDB Community Edition](https://www.mongodb.com/try/download/community)
- [Unity Hub](https://unity.com/download)
- [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)
