# Changelog
All notable changes to this project are documented in this file.

---

## [2.6.19]
### Performance
- **Incremental UsedByMap patch** — single-asset changes patch reverse edges inline during TimeSlice, skipping full BuildUsedByMap rebuild
- **Skip incremental diff during full scan** — full rebuild path only calls LoadContentFast; no diff, no reverse edge patching (BuildUsedByMap handles everything after)
- **Fix big scene hang** — scan all refs only on selection, not during full scan; initial scan only checks overrides (fast), full component scan on selection only
- **Two-pass prefab scan** — skip deep inspection for prefab instances, only scan overridden properties
- **Batch GetGlobalObjectIdsSlow** — prevent editor hang on large scenes by batching ID resolution (1024 chunks, null-padded, zero GC)
- **Type-cache FilterManagedReferenceObjects** — check each Component type once instead of every instance; eliminates tens of thousands of redundant SerializedObject allocations on large scenes
- **Scene cache incremental scan via ObjectChangeEvents** — replace full-hierarchy walk with targeted scan of only new objects; zero cost when nothing added on 76K+ scenes
- **Streaming scene cache persistence** — streaming read with string interning for faster load/save
- **Checkpoint saves during scan** — incremental disk writes during long scans + eliminate per-frame hierarchy walk
- **Reduce TimeSlice GC** — move progress callback to yield point, remove per-item log allocation
- **Eliminate per-frame GC allocations in draw loop** — cache lambda closures, IconContent textures, CalcSize results, and assetFolderWidth

### Added
- **Project Settings panel** — full SettingsProvider under Edit > Project Settings > Find Reference 2 (General, Performance, Display, Advanced, Cache sections)
- **Skip large selections** — configurable maxSelectionCount (default 100) with info box when exceeded
- **Auto-scan new objects on hierarchy change** — detect root count changes from clone/duplicate/paste and incrementally scan unscanned GameObjects for immediate badge updates

### Fixed
- **Unity 6.5 EntityId compatibility** — centralized all EntityId/InstanceID APIs in FR2_Unity; eliminates CS0619 errors from removed int↔EntityId casts
- **Unity 6.6 AppDomain.GetAssemblies warning** — replaced with CurrentAssemblies.GetLoadedAssemblies() on Unity 6.4+ to avoid UAC0005 warning
- **Editor crash on scenes with deep SerializeReference chains** — skip ManagedReference subtrees during property iteration; skip SerializeReference objects from GetGlobalObjectIdsSlow batch
- **Phantom 'Used By' entries from non-GUID hex strings** — ExtractGuid fallback now validates candidates via GUIDToAssetPath; only real asset GUIDs added as edges
- **Cache stuck after exception in batch operations** — wrap suppressRefresh in try/finally to prevent permanent stale state
- **State machine enforced in release builds** — invalid transitions no longer silently proceed; SetAsReady re-triggers refresh if dirty assets accumulated during read/build cycle
- **TimeSlice.Stop() completion callback still firing** — added _stopped flag to prevent stale callbacks after StopAllScans
- **Dispose SerializedObject cache after scan** — free native allocations in FinalizeScan instead of holding until scene unload
- **Reentrant onReady invocation** — guard partial/dirty scan paths from double-triggering listeners
- **Infinite recursion in FR2_SceneCache.Api getter** — assign instance before deferred init to break recursive construction loop
- **Scene cache lifecycle** — fix runtime scene warnings, autoRefresh toggle, clone/duplicate detection, infinite loop on rescan
- **Scene cache prefab scanning** — scan prefab instances fully (not just overrides) so internal refs like LODGroup→children are detected; load disk cache on create; skip already-scanned objects
- **Guard all subsystems** — nothing runs before cache asset exists or when FR2 is disabled; defer work until editor is idle
- **Selection panel showing (none)** — force GroupMode.None, add skipFilter, allow empty-string group ID
- **Prefab parent shown as direct usage for child objects** — only show source prefab for instance root, not children
- **Scan unsaved scenes** — handle scenes with no path/GUID using temporary cache with runtime key
- **Rescan GO on selection** — always rescan selected GameObject to detect property changes (stale mesh/material overrides)
- **Label width cache unbounded growth** — clear _labelWidthCache on cache ready to prevent memory leak
- **Dead code cleanup** — remove unused cacheImage field, deduplicate _countContentCache, wire SettingChanged/IgnoreChanged events

---

## [2.6.18]
### Added
- **Group header icons with click-to-ping** - group headers now show asset type icons; clicking pings the asset in Project window
- **Settings panel reorganization** - settings grouped by category (Display, Behavior, Advanced); dev options hidden behind debug mode toggle

### Fixed
- **Auto-scan unscanned assets on selection** - detects unscanned critical assets on selection change and triggers IncrementalRefresh
- **Refresh button re-reads selected assets** - marks selected assets dirty with LoadFileInfo timestamp check, catches missed import callbacks
- **Crash on nested prefab open** - guard against re-entrant prefab stage callbacks
- **Wrong version check** - fixed `projectWindowItemByEntityIdOnGUI` version gate
- **Ignore filter applied to asset references** - refs pointing to ignored asset paths are now excluded from results
- **Empty title warning in scene ref column** - skip short-form content when there's nothing to abbreviate instead of passing empty string to GUIContent
- **Blank/white icons for scene refs** - fixed scene reference rows showing incorrect or missing icons
- **Scroll wheel bleeding between split panels** - scroll events no longer propagate across panel boundaries
- **Inconsistent toolbar spacing** - unified 1px icon spacing across all panel toolbars

---

## [2.6.17] 
### Added
- **Selection History (Pinned Selections)** - pin current selection via lock icon button; pinned groups persist to `Library/FR2/selection-history.json` across reloads; max 10 groups with auto-discard; click group header to restore selection; × to remove; right-click context menu
- **Unified Row Drawer Foundation** - `RowDrawData` + `FR2_RowDrawer` infrastructure for future unified asset/scene-ref row rendering (Phase 1 of UI revamp, additive only)
- **Shader #include Parser** - re-enabled and simplified shader `#include` directive parsing with relative-to-source path resolution and common URP/HDRP search directories
- **Scene Ref Overflow Indicator** - component groups capped at 3 visible with "+N" overflow badge and tooltip listing hidden groups
- **Duplicate Preferred Selection** - radio button to mark preferred asset per duplicate group, auto-selected by usage count; blue name highlight for preferred
- **Duplicate Merge/Remove Actions** - "Merge Usage" redirects all references to preferred asset; "Remove Duplicated" deletes unused non-preferred duplicates
- **Duplicate Dismiss Groups** - × button to hide unwanted duplicate groups from view
- **GUID Manager Type Mismatch** - detects mixed asset types with warning and row highlighting; per-row Merge button replaces bottom panel

### Changed
- **Lock Icon → Pin Action** - lock button now pins current selection as a history group instead of toggling lock state; selection always syncs with Unity selection

### Fixed
- **Code Quality** - replaced raw `Debug.Log` with `FR2_LOG` in validator, added proper `using UnityEngine` in FR2_Define, removed dead commented-out debug logs
- **Duplicate Group Header** - improved layout with name + count + dimmed file size; `TryGetValue` safety guard
- **GUID Manager Layout** - auto-width GUID column, compact Copy button, combined GUID/fileID field, Paste updates object immediately


---

## [2.6.16] - 2026-04-25
### Added / Dev
- **SpriteAtlas V2 Support** - `.spriteatlasv2` recognized alongside `.spriteatlas`
- **Variant Prefab Tracking** - scene cache tracks both source and variant prefab GUIDs
- **Detailed Scene References** - scene refs now show per-component, per-property detail
- **Indirect Scene References** - textures, shaders, audio, fonts resolve scene usage through UsedBy chain
- **Atlas Reverse Edges** - sprites packed in an atlas now show the atlas as a dependent
- **Centralized Scene Queries** - `FR2_Unity` wrappers for consistent scene enumeration across Unity versions

### Fixed
- **Stack Overflow on Prefab Open** - `GetNextSceneFromQueue` no longer calls `GetSceneCacheForScene` (side effects caused infinite recursion)
- **Stack Overflow on Asset Open** - `AssetDatabase.OpenAsset` deferred via `delayCall` to avoid Mono GC crash in deep GUI call stacks
- **PrefabInstance Cast Error** - `TryGetGUIDAndLocalFileIdentifier` guards non-persistent objects
- **Hierarchy/Project GUI Crashes** - try/catch guards prevent FR2 exceptions from destabilizing the editor
- **Folders Not Shown in Selection** - folders selected in Project window now display in Selection panel
- **Stale missingGUIDs** - GUIDs no longer stuck in `missingGUIDs` after assets become available; Used By tab no longer shows empty
- **Scene Reference to Asset** - prefab field references (e.g. serialized prefab fields on MonoBehaviours) no longer missed
- **Internal Prefab Fields Filtered** - `m_CorrespondingSourceObject`, `m_PrefabInstance`, `m_PrefabAsset` excluded from scene ref results
- **Selection Sync After Reload** - selection panel no longer empty after domain reload (play mode, compile)
- **FR2_RefDrawer Null Safety** - `ValidateRefs` guards null dictionary
- **Duplicate File Compare Crash** - missing files removed before sorting duplicate groups
- **Menu Crash on Init** - context menu shows disabled items instead of crashing when cache not ready
- **EditorSceneManager Warnings** - obsolete `loadedSceneCount` warning suppressed on Unity 2022.2+
- **SpriteAtlas bindAsDefault** - read from `.meta` file text instead of loading `SerializedObject`

### Technical Improvements
- **Scene Cache Version** bumped to 6 (forces re-scan with new data format)
- **Scan Queue Safety** - scan completion callbacks use `EditorApplication.delayCall` to prevent synchronous recursion
- **Tests Removed from Package** - test assembly moved out of release package

---

## [2.6.15] - 2026-03-21
### Added
- **VFX Graph Support** - full support for Visual Effect Graph assets (`.vfx`, `.vfxoperator`, `.vfxblock`): meshes, textures, shaders, subgraphs, and prefab overrides
- **Parser Verbose Setting** - new "Parser Verbose (Missed GUID)" option in settings to control parser fallback warnings (off by default)
- **SceneCache2 Persistent Cache** - refactored scene cache with persistent disk storage for faster scene reference lookups across sessions
- **Nested Prefab Usage** - UsedBy panel now shows nested prefab instance usage in scene references
- **Multi-Scene Path Display** - scene references now show `SceneName/Path/Object` in the path when multiple scenes are open simultaneously (`Show Full Path` must be enabled); single-scene and prefab stage unaffected

### Fixed
- **PrefabStage Results** - PrefabStage now returns results properly when querying references
- **Scene Object Checkboxes** - reverted checkboxes in front of scene objects to previous behavior
- **VFX YAML Parsing** - added `m_AssetGuid`, `AssetGUID` patterns and META_FILES for `.vfxoperator`/`.vfxblock`
- **Hierarchy Usage Count** - usage count badges in the Hierarchy window now display correctly for all loaded scenes simultaneously; previously counts only appeared for the focused/selected scene

### Technical Improvements
- **Unity 2021+ Only** - removed legacy code for pre-2021 Unity versions
- **Scene Cache Initialization** - no longer waits for FR2 asset cache to be ready before scene operations
- **Parser Warning Control** - "Missed GUID" and "[FR2] Normalized" logs gated behind Parser Verbose setting

---

## [2.6.14] - 2026-01-19
### Added / Dev
- **Badge Style Display** - badge-style reference count display now enabled by default for hierarchy
- **Test Assembly** - added test assembly infrastructure for improved code quality

### Fixed
- **Scene Cache Refresh** - prevented cascading onReady callbacks triggering unexpected scene cache refresh
- **Nested Refresh Triggers** - prevented nested RefreshCache triggers during OnReady callback
- **Race Condition** - fixed race condition when calling refresh while already refreshing
- **Scene Dirty Marking** - scene now marked as dirty only when actual changes occur
- **Null Title Warning** - resolved null title warning in UI
- **Full Path Display** - full path now properly displayed in scan progress
- **UIToolkit Package Dependencies** - resolved path for UIToolkit assets with Packages dependencies
- **Cache Ready Callbacks** - added Repaint() to cache ready callbacks for proper UI updates
- **Panel Repaint** - FR2 panel now repaints when inactive during compilation/scanning
- **Play Mode Scene Refresh** - scene now auto-refreshes immediately after exiting play mode

### Performance Improvements
- **TimeSlice for UsedBy Builder** - applied TimeSlice to Cache usedBy builder for smoother editor performance
- **Dictionary Access** - direct dictionary access used wherever possible to reduce overhead
- **Extension Caching** - cached lower case extensions of paths to avoid repeated allocations
- **Scene Cache Optimization** - optimized scene cache with component lookup and reduced allocations
- **Incremental Refresh** - incremental refresh now used always for better performance
- **TimeSlice Frame Time** - reduced timeSlice frame time to prevent editor hangs
- **Selection Changes** - simplified selection change handling for better responsiveness

### Technical Improvements
- **Kiro Configuration** - improved Kiro config and settings
- **Subsystem Documentation** - consolidated subsystem documentation for better maintainability
- **Unity MCP Integration** - added Unity MCP support
- **Code Style Compliance** - applied early return patterns and removed unnecessary comments across codebase

---

## [2.6.13] - 2026-01-04
### Added / Dev
- **Project Panel Badge Style** - new badge-style display for reference counts in Unity Project panel
- **String Extensions** - added new string utility extensions for improved text processing
- **Enhanced YAML Parser** - improved YAML parsing with better handling of spriteID and Hash fields

### Fixed
- **Unity 6.3 Compatibility** - replaced deprecated InstanceIDToObject with EntityIdToObject to fix Unity 6.3 warnings
- **YAML File Reference Handling** - better parsing and handling of YAML file references
- **GUID Extraction Logic** - improved ExtractGuid logic to exclude longer hex sequences and prevent false positives
- **Cache Status Reset** - properly reset status when FR2 cache is deleted

### Technical Improvements
- **Memory Optimization** - significant GC reduction in FR2_NavigationHistory through object pooling and reuse
- **GUID/Asset Path Caching** - aggressive caching of GUID and asset path mappings to reduce memory allocations
- **Performance Enhancements** - reduced allocations across 26+ files with improved caching strategies
- **Code Cleanup** - removed obsolete code and simplified logic throughout codebase

---

## [2.6.12] - 2025-12-31
### Added / Dev
- **AssetDatabase Validation Option** - option to disable AssetDatabase validation for performance
- **Performance Improvements** - improved first scan performance and aggressive GUID/asset path caching
- **Enhanced Logging** - improved logging for easier debugging
- **UIToolkit Dependency Validation** - improved dependency validation for UIToolkit assets

### Fixed
- **Unity 6.3 Compatibility** - scene was unloaded after play mode in Unity 6.3
- **FR2_Cache Deletion Handling** - properly handle the case when FR2_Cache.asset being deleted mid-way
- **Hierarchy Icon Cache** - do not refresh hierarchy icon cache when auto refresh is disabled
- **Initialization Process** - simplified FR2 initialize process, wait for FR2_Cache to ready before init subsystems
- **BuildUsedByMap Calls** - allow calling BuildUsedByMap from RefreshDB when dirty flag set with no asset actually changed
- **MiniLabel Style Error** - fixed error getting miniLabel style

### Technical Improvements
- **Content Loader Cleanup** - cleaned up and minor improvements for FR2_Asset content loader
- **Cache Management** - improved cache management and performance optimizations
- **Code Simplification** - cleaned up and simplified logic throughout codebase

---

## [2.6.11] - 2025-12-06
### Added / Dev
- **Instance ID to GUID Mapping** - cached map for instanceId to GUID + LocalFileID conversion
- **Scene Reference Asset Details** - enhanced display of scene references with asset details

### Fixed
- **PingAndHighlight Issues** - ping and highlight not triggered for detail reference (scene -> asset)
- **Exception Handling** - fixed exception after calling ping and highlight
- **Empty Message Logic** - empty message now only generated when result is actually empty
- **FR2 Disable Checks** - added proper checks to prevent processing when FR2 is disabled

### Technical Improvements
- **SceneCache Performance** - improved SceneCache operations performance
- **Code Cleanup** - cleaned up and simplified logic throughout codebase
- **Method Naming** - minor improvements in method naming consistency

---

## [2.6.10] - 2025-11-18
### Added / Dev
- **FR2_Scope System** - new scoping system for code readability and organization
- **Hierarchy Usage Count Display** - show reference counts in Unity hierarchy
- **Scene Reference Grouping** - group similar scene references (same script & target)
- **Hierarchy Path Clipping** - auto-clip hierarchy paths when space limited
- **Selection Change Debouncing** - debounced selection updates for performance

### Fixed
- **Model Asset Scanning** - model assets constantly added/scanned & removed
- **FileInfo Timestamp Issues** - critical FileInfo timestamp handling
- **Scene Cache Performance** - multiple scene cache improvements

### Technical Improvements
- **Memory Optimization** - reduced GC allocation, reused arrays, eliminated LINQ
- **Performance Enhancements** - capacity checks, cached selection, optimized rendering
- **Code Organization** - applied FR2_Scope throughout codebase
- **Settings Layout** - reorganized settings panel

---

## [2.6.9] - 2025-11-09
### Added / Dev
- **Enhanced Cache Management** - FR2_Cache lifecycle and project management
- **Performance Monitoring** - profiler logic for performance tracking

### Technical Improvements
- **Cache Architecture** - cleaned up and reorganized FR2_Cache structure
- **Performance Optimizations** - check capacity before operations for speed
- **Code Cleanup** - removed obsolete cache management code

---

## [2.6.8] - 2025-11-07
### Added / Dev
- **Prefab Reference Filtering** - filter prefab asset references from children GameObjects & components
- **Enhanced Profiler Integration** - comprehensive profiler logic for performance analysis

### Fixed
- **Cache Initialization** - refactored FR2_Cache initialization flow for reliability
- **Scene Cache Performance** - FR2_SceneCache operations performance

### Technical Improvements
- **Content Loader Enhancements** - asset content loading mechanisms
- **Async Processing** - async processor handling in FR2_Cache
- **Various Stability Fixes** - multiple stability improvements

---

## [2.6.7] - 2025-10-10
### Added / Dev
- **SerializedObject Extensions** - extension methods for Unity integration
- **GUI Improvements** - refactored GUI system with extension method support

### Technical Improvements
- **Scene Cache Performance** - initial SceneCache performance improvements
- **Extension Method Architecture** - refactored GUI & extension methods for maintainability
- **Performance Profiling** - initial profiler logic for performance monitoring

---

## [2.6.6] - 2025-09-28
### Added / Dev
- **ClippedLabel System** - new component for handling long asset paths with proper text clipping
- **Scene Reference Info Area** - enhanced SceneRef UI with reference information display
- **Sibling Action Support** - apply same action (select/unselect + expand/collapse) to sibling items
- **Light Theme Icon Improvements** - better icon colors and theming for light theme users

### Fixed
- **SplitView Width Persistence** - panels now properly preserve width after Unity reloads
- **SplitView Refresh Issues** - corrected refresh behavior and layout calculations
- **Sort Mode Functionality** - fixed sorting not working properly in reference trees
- **Bookmark Panel Issues** - removed inappropriate "show detail" button and scene object support
- **Addressable Label Layout** - improved layout and spacing for addressable asset labels
- **Asset Drawing Performance** - optimized drawing with new caching system
- **Usage Count Display** - fixed messed up usage count rendering
- **Detail & Properties Buttons** - corrected button drawing and interaction
- **Prefab Editing** - now uses PrefabUtility to maintain prefab connections
- **Enum Selector Mouse Events** - fixed selector not working due to swallowed mouse events
- **Themed Color Consistency** - unified color usage across asset folders and extensions
- **Available Space Handling** - improved layout calculations for long asset paths
- **Inconsistent UI Coloring** - standardized colors across different UI components

### Technical Improvements
- **Asset Draw Cache System** - new caching mechanism for improved drawing performance
- **Enhanced Theme System** - expanded theme support with better color management
- **Code Organization** - restructured drawing logic for better maintainability
- **Performance Optimizations** - reduced redundant calculations and improved rendering speed

---

## [2.6.5] - 2025-09-04
### Added / Dev
- **Delayed Auto Refresh** - auto refresh now waits until FR2 panel is in focus for better performance
- **Enhanced Scene Refresh** - scene refresh is now delayed until focus for improved responsiveness

### Fixed
- **Flexible Splits** - FR2_Split now properly respects minPixel constraints
- **UI Spacing Adjustments** - improved layout consistency across panels
- **Detail Panel Issues** - fixed detail panel not working sometimes for scene to asset navigation
- **Layout Clicking Issues** - fixed popping layout that made it hard to click properties and detail buttons

### Technical Improvements
- **Performance Optimization** - improved performance by caching dirty status
- **Code Clean Up** - various code improvements and optimizations

---

## [2.6.4] - 2025-01-25
### Added / Dev
- **Persistent Panel Size Settings** to remember user preferences
- **Selection Shortcuts** for easy multi-selection workflows
- **Enhanced Bookmark UX** with improved navigation and management

### Fixed
- **SplitView Resize Error** when some panels are hidden
- **RefDrawer Context Issues** with improved configuration handling

### Technical Improvements
- **Various Fixes and Clean Up** for better stability
- **Improved Configuration Management** for RefDrawer components

---

## [2.6.3] - 2025-01-24
### Added / Dev
- **Properties Panel** with detail button for enhanced asset inspection
- **Assembly Reload Support** for better development workflow
- **Asset IncrementalRefresh Improvements** for better performance

### Fixed
- **UIToolkit Relative Path Dependencies** now properly supported
- **Debug Logging** cleanup and FR2_Dev.NoLog improvements

### Technical Improvements
- **Enhanced Properties Display** with better UI integration
- **Improved Development Tools** for easier debugging

---

## [2.6.2] - 2025-01-23
### Added / Dev
- **Enhanced Asset Cache Management** - only critical assets with references are saved
- **Improved Empty Result Messages** with better user guidance
- **Auto-Expand Asset Info** in FR2_CacheEditor for easier inspection
- **Package Assets Reading** - no longer excludes packages assets by default

### Fixed
- **FR2_Define Logic** errors resolved
- **Ignore List Management** - can now properly remove items
- **Scene Incremental Refresh** in prefab mode
- **Force Refresh Issues** for first-time use and menu selection
- **EOL Handling** for FR2_TreeUI and missing FR2_Readme.pdf
- **Various Null Checks** for better stability

### Technical Improvements
- **PingRow Configuration** removal and theme color adjustments
- **Initial Refresh Logic** improvements
- **UI/UX Enhancements** for non-scanned or dirty assets

---

## [2.6.1] - 2025-07-31
### Added / Dev
- **Incremental Refresh System** for Assets panel - only processes dirty/unscanned assets instead of full refresh
- **Enhanced Visual Feedback** with improved dirty state indicators and status messages
- **FR2_Theme System** with centralized UI constants for Light/Dark themes
- **Conditional Debug Logging** with FR2_LOG class for cleaner release builds
- **Persistent Dirty State** across Unity recompiles for better UX
- **Smart Asset Scanning Status** detection to distinguish never-scanned vs. no-references
- **Toggle FR2 Debug** option for development builds

### Fixed
- **Selection Panel** refresh button incorrectly showing (now hidden as intended)
- **Asset Panel Tooltips** not matching visual state (yellow title but "ready" tooltip)
- **Confusing Status Messages** improved to be more actionable ("hit Refresh for complete results")
- **HasChanged Flag** now properly serialized and persistent across recompiles
- **FR2_Define Logic** errors after modifying csc.rsp files
- **FR2_SelectionManager** initialization issues in some scenarios
- **Scene Scan Stuck** issue with better state management

### Technical Improvements
- **Message Type Detection** for warning vs info boxes based on content
- **Refresh Button Sizing** increased width for better text visibility
- **Asset Dirty State Logic** enhanced to include unscanned assets
- **Conditional Compilation** setup for debug/development builds
- **AssetDatabase Validation** integration for better accuracy

---

## [2.6.0] - 2025-01-20
### Added / Dev
- **Smart Lock** system for better selection handling  
- **Selection Navigation History** with back/forward buttons  
- **Unified Selection Manager** for centralized selection state  
- **Scene Reference UI revamp** with improved drawing & interaction  
- **Unity 6 compatibility** with new window focus API  
- **Basic shader references** support  
- **Highlight for out-of-sync selection** status  
- **UIToolkit resources** reference finding improvements  
- **Recursive unused asset checking** option in Tools panel  
- **Improved empty result messages** with contextual feedback  
- **Lock icon visibility** improvements when selection is locked  

### Fixed 
- **Folder usage visibility** in FR2 panel  
- **SetEditorsExpanded workaround** & serialized property path expansion  
- **Extensions normalization** before grouping  
- **Font assets reference** detection  
- **UsedBy panel refresh** issues  
- **Unity 2019.4 compatibility** fixes  
- **Selection cache improvements** & synchronization  

### Technical Improvements
- **Restructured selection logic** with proper separation of concerns  
- **Enhanced scene cache system** with intelligent change tracking  
- **Better Unity version compatibility** handling  
- **Improved performance** for selection operations  

---

## [2.5.13] - 2025-06-16
### Added / Dev
- Project-architecture docs & cursor rules  
- Refactor and code formatting  
- **Remove Missing Scripts** tool  
- Hide tool warning banner  
- Recursive *unused-asset* checking  
- Git integration  
- Duplicate-tab improvements  
- **AssetOrganizer** & *Delete Empty Folders*

### Fixed
- Scene did not finish refreshing in Play Mode  
- Unknown assets wrongly marked *unused*  
- Parser crash when reference was missing or empty

---

## [2.5.12] - 2025-05-04
### Added / Dev
- Option to write **FR2 Import Log** to file (only for assets ≥ 1 MB)  
- More aggressive RAM clean-up after load  
- `AssetDatabase` references to speed up **LoadContent**  
- Buffered file reading for faster I/O  
- Serialize only *critical* (referenced) assets  
- Import-process UX tweaks

---

## [2.5.11] - 2024-12-06
### Added / Dev
- Remove *Scan Priority* GUI  
- Extra view-customisation options in *Usage / Used-By* tree  
- README & FR2 version update

### Fixed
- “Ping asset” could fire twice

---

## [2.5.10] - 2024-11-10
### Added / Dev
- Hide-ignore-root grouping & layout tweaks  
- Customisable **Show Full Path**

### Fixed
- FR2 inactive in Play Mode even when *Enable = true*  
- Grouping for files without extensions

---

## [2.5.9] - 2024-10-13
### Added / Dev
- Basic support for built-in assets  
- “+” indicator for Sprite Atlases included in build  
- **Show Full Path** toggle  
- Hide-ignore-root grouping

### Fixed
- Sprite Atlas *Force Include* handling  
- Sprite Atlases with all sprites unused now marked *unused*  
- More generous GUID detection  
- Wrong GUID extracted for `.asmdef`  
- Various fixes in `FR2_Asset` & `FR2_Addressable`

---

## [2.5.8] - 2024-09-01
### Added / Dev
- Addressables support  
- Item-count on groups  
- Even spacing in **TabView**  
- Light-map support  
- Classes marked `internal` for tighter API

### Fixed
- `TabRect` calculation  
- UI spacing in extension  
- Crash on re-import / save in Unity 2020.x

---

## [2.5.7] - 2024-08-11
### Added / Dev
- UIToolkit (`.uss`, `.uxml`, `.tss`) support  
- `.spriteLib` support  
- Asset-extension column  
- Cleaned YAML/JSON parser

### Fixed
- GUID/FileID replacement no longer alters line endings (Windows)  
- Unity 2019.4 compatibility fixes

---

## [2.5.6] - 2024-07-04
### Added / Dev
- Exclude *Packages/* assets by default  
- `.shadergraph` & `.playable` support  
- 64-bit (`long`) `LocalFileID`  
- LazyInit **FR2_Unity** & faster start-up  
- Hide sub-asset Local IDs by default  
- Play-Mode optimisations  
- Icon update

### Fixed
- Null exceptions (tool focus, `TerrainTextureData`)  
- Layout error when exiting Play Mode  
- Various null checks & missing namespaces

---

## [2.5.5] - 2024-06-26
### Added / Dev
- All tools moved to **Tool** tab  
- Selection History  
- Layout improvements  
- Draw *use count* only on main asset  
- Separate **Group Mode** for tools  
- Smarter auto show/hide (asset + scene + detail)

### Fixed
- Light-map assets no longer listed as *unused*  
- Misc. null-reference fixes

---

## [2.5.4] - 2024-05-25
### Added / Dev
- Initial public release of FR2 (core functionality)

---

_This file follows [Keep a Changelog](https://keepachangelog.com) and [SemVer](https://semver.org/)._