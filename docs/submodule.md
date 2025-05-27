### The alternative way (submodule)
You can connect the SharedLogic library (or your fork of it) to multiple projects as a submodule, making it easy to reuse and update in all of them.

> ⚠️ **Note:** This setup is not required for everyone and involves some extra steps for configuration. Consider whether you really need this flexibility, or if simply maintaining your own fork is enough for your workflow.

Below, we describe how to add SharedLogic as a submodule to your project and keep it up to date.

> ⚠️ **Note:** We can't use Unity UPM (Package manager from Unity) to connect everything, because the SharedLogic library has backend project, it isn't for UPM.

#### Recommended Directory Structure

Your repository should be organized as follows:

```
/Root
  /UnityProject                 <-- your main Unity project
  /SharedLogic                  <-- submodule of the SharedLogic library (this repository)
  /GamePlugin                   <-- your own game plugin project
```

#### Create Unity project
First you should create folder **/Root** and init empty git repository here, then create new Unity project called
`UnityProject` (or put in folder `UnityProject` your existing project)

#### Adding SharedLogic as submodule
Go to **/Root** folder. Run command:
```sh
git submodule add git@github.com:NikolayLezhnev/sharedlogic.git SharedLogic
```
#### Creating a GamePlugin Project

1. **Unpack the Template**

   Unzip `/Root/SharedLogic/docs/GamePluginTemplate.zip` into the `/Root/GamePlugin/` folder.


2. **Open the Solution**

   Open `/Root/GamePlugin/GamePlugin.sln` in Visual Studio or JetBrains Rider. All projects should be recognized automatically.


3. **Configure UnityProject**

   In your `/Root/UnityProject/Packages/manifest.json`, add the following two entries to the `"dependencies"` section:

   ```
   "com.nlezhnev.sharedlogic": "file:../../SharedLogic/SharedLogic/Runtime",
   "com.nlezhnev.gameplugin": "file:../../GamePlugin/Runtime"
   ```

   Example dependencies block:

   ```
   {
     "dependencies": {
          ...
       "com.unity.modules.xr": "1.0.0",
       "com.nlezhnev.sharedlogic": "file:../../SharedLogic/SharedLogic/Runtime",
       "com.nlezhnev.gameplugin": "file:../../GamePlugin/Runtime"
     }
   }
   ```

   > **Note:** This type of GamePlugin linkage gives you opportunity to keep GamePlugin source code in one
   > folder and use it on both server and client

4. **Develop Your Plugin**

    - Write and edit your game plugin logic in the `GamePlugin/` folder.
    - Do not edit the code inside `SharedLogic/` unless you are maintaining the shared library itself.
    - The Unity project will see both libraries as local packages and can reference their code.


5. **Integrate in Unity**

   Copy the file from **/Root/SharedLogic/UnityClient/Assets/UnityClient.cs** to  **Root/UnityProject/Assets/UnityClient.cs**.
   Edit it to fit your needs. Don't forget to specify the correct server port in the Inspector.


6. **Debugging**

    - Open **/Root/GamePlugin/GamePlugin.sln**
    - Build solution
    - Run project **SharedLogicServer**
    - Connect to server from **/Root/UnityProject**

Example of full resulting structure:
```
Root/
  /.git
  /UnityProject/                 <-- your main Unity project
    /Assets/
    /Packages/
    /ProjectSettings/
    /Library/
  /SharedLogic/                  <-- submodule of the SharedLogic library (this repository)
    /Benchmark/
    /SharedLogic/
    /SharedLogicServer/
    /SpecificGamePlugin/
    /UnityClient/
    /SharedLogic.sln
  /GamePlugin/                   <-- your own game plugin project
    /Runtime/
      /GamePlugin.cs
      /GamePlugin.asmdef
      /package.json
    /GamePlugin.csproj
    /GamePlugin.sln
```

7. **Updating the SharedLogic Submodule**

If the upstream SharedLogic library is updated, you can update your local submodule with:

```sh
git submodule update --remote SharedLogic
```

### Useful Links
- [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)