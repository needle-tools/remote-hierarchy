# Remote Hierarchy

View and interact with scenes on connected Unity players. Uses the EditorConnection framework, so works everywhere.  
Supports multiple scenes, shows components on GameObjects, and allows for calling methods on remote components.  

## How to use

1. **Make a Development Build**  
  Check the "Development Build" option in Build Settings.

2. **Run the Build**  

3. **Open the connector window**   
    Open *Window > Analysis > Remote Hierarchy*
    
4. **Connect to the running app**  
  Click on the dropdown that says "Editor" and select your running app.   
  > If Console or Profiler can connect to your build, so can Remote Hierarchy.   
  If your app doesn't show up, it's probably not a development build.   

5. **Get the hierarchy**  
  Click on "Show Hierarchy Snapshot".  
  
> This will send a snapshot of the current hierarchy structure and components on the Player to your Editor.  
  It automatically opens a Preview Stage of that hierarchy.  
  Please note that this is a _snapshot_ – it won't update the remote state if that keeps on changing.  
  
6. **Interact with your scene**  

Currently supported actions:  
- enable/disable GameObjects  
- see which components are on which GameObject and whether they're enabled  
- call ContextMenu methods on components  
- call public methods that have string parameters on components  
- see vertex count from MeshFilters  

## For developers 

There's currently no way to extend the package without modifying it.  
The main script is `RemoteHierarchy.cs`. It contains serializable classes for various things (Scenes, GameObjects, Components). The full hierarchy is collected upon request and sent back to the Editor.  
For simplicity, Unity's built-in JSON serializer is used.  

New components can be added that inherit from `ComponentInfo`; `MeshFilterInfo` is a good minimal example of that.  

Changes to the local hierarchy are sent as JSON back to the Player and then executed (e.g. enabling/disabling objects, calling methods).  

For development, you can toggle dev mode in the ⋮ menu in the Remote Hierarchy window, which lets you take and interact with a snapshot of your local scene in the same fashion as interaction with a remote scene works.

## Contact
<b>[needle — tools for creators](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybridherbst)
