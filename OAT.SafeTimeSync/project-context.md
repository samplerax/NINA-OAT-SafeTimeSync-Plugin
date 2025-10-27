# OAT Safe Time Sync Plugin - Project Context

## Project Overview
A NINA plugin for OpenAstroTracker (OAT) mounts that provides automatic meridian flip functionality using the OAT-specific `:XGST#` command to query safe tracking time directly from the firmware.

## Workspace Information

### Workspace Directory
`C:\Users\Workshop\source\repos\OAT.SafeTimeSync\`

### Project Structure
- **Project Name**: OAT.SafeTimeSync
- **Project File**: `C:\Users\Workshop\source\repos\OAT.SafeTimeSync\OAT.SafeTimeSync\OAT.SafeTimeSync.csproj`
- **Target Framework**: .NET 8.0-windows
- **Language Version**: C# 13.0
- **Project Type**: WPF Plugin Library

## Current Status: ✅ **Build Successful**

### Latest Version: v1.2.0
**UI Improvements and NINA Integration**
- ✅ Flip time calculation and display (matches NINA's meridian flip trigger)
- ✅ Compact UI with settings in expander (⋯ button)
- ✅ Advanced Sequencer dockable integration (MiniTrigger template)
- ✅ Theme-aware UI matching NINA's design
- ✅ All settings left-aligned for better UX

## Completed Features

### 1. **Core Meridian Flip Trigger** ✅
- Polls mount safe time via `:XGST#` Meade command
- Configurable threshold (default: 5 minutes) and polling interval (default: 15 seconds)
- **Calculates and displays flip time**: `Flip Time = Current Time + (Safe Time - Threshold)`
- Automatically waits for current exposure to finish
- Preserves target coordinates and re-slews (OAT firmware handles side selection)

### 2. **Guiding Integration** ✅
- Optional pause/resume guiding during flip
- Tracks guiding state before flip
- Optional force calibration when resuming guiding
- Uses `StartGuiding(bool recalibrate, ...)` method with `ForceCalibrationAfterFlip` flag

### 3. **Auto Focus After Flip** ✅
- Uses `IAutoFocusVMFactory` to create auto focus VM
- Runs NINA's auto focus routine after flip
- Gracefully handles failures without aborting flip
- Reports focus position on success

### 4. **Recenter After Flip** ✅
- Uses `IPlateSolverFactory` to create centering solver
- Creates proper `CaptureSequence` with plate solve settings
- Gets focal length from `profileService.ActiveProfile.TelescopeSettings.FocalLength`
- Gets pixel size from `profileService.ActiveProfile.CameraSettings.PixelSize`
- Configurable tolerance (default: 30 arcsec) and max attempts (default: 3)
- Gracefully handles failures without aborting flip

### 5. **UI/UX Enhancements** ✅
- **Flip Time Display**:
  - Calculated as `EarliestFlipTime = DateTime.Now + (SafeTime - Threshold)`
  - Displayed in main sequencer: "Flip Time: HH:mm:ss"
  - Displayed in Advanced Sequencer dockable
  - Uses `DateTimeZeroToVisibilityCollapsedConverter` to hide when not calculated
- **Compact Settings UI**:
  - All settings accessible via ⋯ expander button (left side)
  - Settings left-aligned within expander popup
  - No inline threshold display (cleaner look)
  - Theme-aware colors (inherits from NINA)
- **Advanced Sequencer Integration**:
  - MiniTrigger template with TriggerProgressContent
  - Shows lightning bolt icon and flip time
  - Updates every polling interval

## Technical Implementation

### Dependencies (NINA 3.0.0.2017-beta)
```xml
<PackageReference Include="NINA.Plugin" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.WPF.Base" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.Profile" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.Core" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.Equipment" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.Sequencer" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.Astrometry" Version="3.0.0.2017-beta" />
<PackageReference Include="NINA.PlateSolving" Version="3.0.0.2017-beta" />
<PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
```

### Key Classes & Namespaces
- `OatSafeTimeSyncTrigger` - Main trigger implementation
- `MeadeCommunicator` - Handles `:XGST#` command communication
- **Injected Dependencies**:
  - `ITelescopeMediator` ✅
  - `IGuiderMediator` ✅
  - `IImagingMediator` ✅
  - `IPlateSolverFactory` ✅
  - `IAutoFocusVMFactory` ✅
  - `IFilterWheelMediator` ✅
  - `IDomeMediator` ✅
  - `IDomeFollower` ✅
  - `IProfileService` ✅

### UI Properties (Data Binding)
**Public Properties for UI Binding**:
- `EarliestFlipTime` (DateTime) - Calculated flip time
- `LatestFlipTime` (DateTime) - Same as earliest for OAT
- `FlipInProgress` (bool) - Indicates flip is executing
- `LastSafeTimeHours` (double?) - Most recent safe time from mount
- `SafeTimeRemainingDisplay` (string) - Formatted safe time (HH:mm)

**Serialized Settings** (JsonProperty):
- `SafeTimeThresholdMinutes` (double, default 5.0)
- `PollingIntervalSeconds` (int, default 15)
- `PauseGuidingDuringFlip` (bool, default true)
- `ForceCalibrationAfterFlip` (bool, default false)
- `AutofocusAfterFlip` (bool, default false)
- `RecenterAfterFlip` (bool, default false)
- `PlatesolveToleranceArcsec` (double, default 30)
- `MaxCenteringAttempts` (int, default 3)

### XAML Templates
**Main Template** (`SequenceBlockView`):
- `SequenceItemProgressContent`: Displays flip time on right side
- `SequenceItemContent`: Contains ⋯ expander with all settings
- Settings grid with left-aligned labels and controls

**Mini Template** (`MiniTrigger`):
- Used in Advanced Sequencer dockable
- `TriggerProgressContent`: Shows flip time
- Includes lightning bolt icon automatically

## Bug Fix History

### Issue 1: IPlateSolverFactory Not Available ✅ RESOLVED
- **Problem**: Missing `NINA.PlateSolving` package
- **Solution**: Added package reference and imported interfaces

### Issue 2: Version Mismatch ✅ RESOLVED
- **Problem**: Plugin used 3.2.0-rc, NINA was 3.0.0
- **Solution**: Reverted all packages to 3.0.0.2017-beta

### Issue 3: Null Reference in Recenter ✅ RESOLVED
- **Problem**: Passing `null` as `CaptureSequence` to `centeringSolver.Center()`
- **Solution**: Created proper `CaptureSequence` object with plate solve settings

### Issue 4: Focal Length and Pixel Size Retrieval ✅ RESOLVED
- **Problem**: Attempted to use non-existent `IImagingMediator.GetInfo()` and `TelescopeInfo.FocalLength`
- **Solution**: Use profile settings from `IProfileService.ActiveProfile`
```csharp
var telescopeSettings = activeProfile.TelescopeSettings;
var cameraSettings = activeProfile.CameraSettings;
var parameter = new CenterSolveParameter
{
    FocalLength = telescopeSettings.FocalLength,
    PixelSize = cameraSettings.PixelSize
};
```

### Issue 5: XAML Duplicate Elements ✅ RESOLVED
- **Problem**: Merge conflicts created duplicate elements in templates
- **Solution**: Clean XAML structure with single template definitions

## Execution Sequence

When safe time drops below threshold:
1. Wait for current exposure (max 5 min) ✅
2. Stop guiding (if enabled and active) ✅
3. Get target coordinates from telescope mediator ✅
4. Perform flip slew to same coordinates ✅
5. Mount settles using NINA's standard behavior ✅
6. **Auto focus** (if enabled) ✅
7. **Recenter** (if enabled) ✅
   - Creates capture sequence with plate solve settings
   - Gets focal length/pixel size from profile
   - Plate solves and centers mount
8. Resume guiding (if was active, with optional recalibration) ✅

## Debug Logging
Extensive logging with prefixes:
- `OAT Safe Time` - Safe time polling and calculation
- `OAT Safe Time Flip` - Flip execution and post-flip operations
- Full exception stack traces for troubleshooting

## Documentation
- ✅ README.md - Complete setup and usage guide with UI screenshots
- ✅ QUICKSTART.md - Quick reference with visual examples
- ✅ Troubleshooting sections for all features
- ✅ Version history and changelog

## Installation Path
- Target: `%localappdata%\NINA\Plugins\3.0.0\Samplerax.NINA.OatSafeTimeSync\`
- Requires NINA restart after installation
- Trigger appears in: **OAT Safe Time Sync** category

## Known Limitations
1. ⚠️ Recenter requires plate solver configuration (ASTAP recommended)
2. ⚠️ Auto focus requires connected focuser
3. ⚠️ Plate solving fails with simulator camera (no stars)
4. ✅ All error handling graceful - failures don't abort flip
5. ✅ Comprehensive logging for troubleshooting

## Version History
- **v1.2.0** - UI improvements, flip time display, Advanced Sequencer integration ✅ **CURRENT**
- **v1.1.0** - Auto focus and recenter features, removed settle time ✅
- **v1.0.0** - Initial release with basic flip functionality ✅

## Test Environment
- **NINA Version**: 3.1 HF2
- **Target NINA API**: 3.0.0.2017-beta
- **OpenAstroTracker ASCOM Driver**: V6.6.7.2 Release
- **OpenAstroTracker Firmware**: v1.13.9

## External Source Code References
- **NINA Source**: `C:\Users\Workshop\source\repos\nina`
- **OAT ASCOM Driver**: `C:\Users\Workshop\source\repos\OpenAstroTracker-Desktop\ASCOM.Driver`

## Key Learnings

### 1. Accessing Equipment Settings in NINA 3.0.0
**❌ WRONG** (these don't exist):
```csharp
var cameraInfo = imagingMediator?.GetInfo();  // Method doesn't exist
var focalLength = telescopeInfo?.FocalLength;  // Property doesn't exist
```

**✅ CORRECT** (use profile service):
```csharp
var activeProfile = profileService.ActiveProfile;
var focalLength = activeProfile.TelescopeSettings.FocalLength;
var pixelSize = activeProfile.CameraSettings.PixelSize;
```

### 2. UI Template Patterns
**Main Sequencer** (`SequenceBlockView`):
- Use `SequenceItemProgressContent` for right-side info (flip time)
- Use `SequenceItemContent` for main content (settings expander)

**Mini Sequencer** (`MiniTrigger` for triggers):
- Use `TriggerProgressContent` for right-side info
- Automatically includes lightning bolt icon
- Different from `MiniSequenceItem` (for non-trigger items)

### 3. Property Change Notifications
When updating properties that affect UI:
```csharp
private set
{
    if (field != value)
    {
        field = value;
        RaisePropertyChanged();
        RaisePropertyChanged(nameof(DependentProperty)); // Update dependent properties
    }
}
```

### 4. XAML Alignment Best Practices
- Set `HorizontalAlignment="Left"` on Grid and all child controls for left-aligned settings
- Use `DockPanel.Dock="Left"` or `"Right"` for positioning elements
- Add `VerticalAlignment="Center"` for proper vertical centering

## Recent Changes Summary (v1.2.0)

### UI/UX Improvements
- ✅ Added flip time calculation using `EarliestFlipTime` property
- ✅ Removed inline threshold display from trigger block
- ✅ Moved all settings to ⋯ expander button (left side)
- ✅ Left-aligned all settings within expander for better readability
- ✅ Theme-aware UI (inherits NINA colors)

### Advanced Sequencer Integration
- ✅ Created `MiniTrigger` template with `TriggerProgressContent`
- ✅ Flip time displays in Imaging panel's Advanced Sequencer dockable
- ✅ Lightning bolt icon automatically added by NINA

### Code Improvements
- ✅ Added `EarliestFlipTime` and `LatestFlipTime` DateTime properties
- ✅ Flip time calculation: `DateTime.Now + TimeSpan.FromMinutes(SafeTime - Threshold)`
- ✅ Property change notifications trigger UI updates
- ✅ Clean XAML without duplicates or merge artifacts

### Files Modified
- `OatSafeTimeSyncTrigger.cs` - Added flip time properties and calculations
- `OatSafeTimeSyncTemplates.xaml` - New UI layout with expander and flip time display
- `README.md` - Updated with v1.2.0 features and UI documentation
- `QUICKSTART.md` - Updated with new UI behavior and visual examples
- `project-context.md` - This file, updated with v1.2.0 documentation

---

**Build Status**: ✅ Successful  
**All Features**: ✅ Complete and tested  
**Documentation**: ✅ Up to date