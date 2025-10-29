# OAT Safe Time Sync Plugin for NINA

This plugin provides automatic meridian flip functionality for OpenAstroTracker (OAT) mounts that use a non-standard DEC axis orientation.

## The Problem

OAT mounts have their DEC axis oriented 90° differently from standard German Equatorial Mounts (GEMs):
- **Standard GEM**: DEC axis moves left-right (east-west)
- **OAT Mount**: DEC axis moves up-down (like altitude in Alt-Az)

This means NINA's standard meridian/time logic is not correct for OAT mounts and standard meridian flip triggers may not work as expected.

## The Solution

The plugin queries the mount firmware directly using the OAT-specific `:XGST#` Meade command to read the remaining safe tracking time and performs a controlled meridian flip when that time drops below a configured threshold.

### Features

**OAT Safe Time Flip Trigger:**
- Polls the mount's safe time at configurable intervals (default 15s)
- Displays calculated flip time in UI (matches NINA's standard meridian flip trigger)
- Triggers a meridian flip when safe time drops below a threshold (default 5 min)
- Waits for current exposure to finish before flipping
- Optionally pauses/resumes guiding around the flip
- Optionally forces guide recalibration when guiding is resumed
- Optionally runs autofocus and/or plate-solve recentering after flip
- Slews to the same RA/Dec coordinates — OAT firmware chooses pier side
- Compact UI with settings accessible via expander (⋯) button

## Setup

1. Add the trigger to your sequence:
   - Open your imaging sequence
   - Add a Trigger → `OAT Safe Time Sync` → `OAT Safe Time Flip`
2. Configure settings by clicking the ⋯ button on the trigger

## User Interface

The trigger displays information in two locations:

**Main Sequencer Panel:**
- **⋯ button** (left): Click to access all settings
- **Flip Time** (right): Shows calculated flip time (e.g., "Flip Time: 23:45:30")

**Advanced Sequencer Dockable (Imaging Panel):**
- Shows trigger status with lightning bolt icon
- Displays flip time when calculated
- Updates automatically every polling interval

## Trigger Settings

Click the **⋯** button on the trigger to access these settings:

| Setting | Default | Description |
|---------|---------|-------------|
| Safe Time Threshold | 5.0 min | When remaining safe time ≤ this value, trigger fires |
| Polling Interval | 15 sec | How often to query firmware with `:XGST#` |
| Pause Guiding During Flip | Enabled | Stop guiding before flip, restart after |
| Force Calibration After Flip | Disabled | Request recalibration when restarting guider |
| Auto Focus After Flip | Disabled | Run autofocus after flip (requires focuser) |
| Recenter After Flip | Disabled | Run plate-solve centering (requires solver) |
| Plate Solve Tolerance | 30 arcsec | Max acceptable centering error |
| Max Centering Attempts | 3 | Number of centering attempts |

## Important: Configuring Safe Time Threshold

### Basic Rule
⚠️ **Always set the safe time threshold higher than your exposure time.** This ensures the trigger doesn't fire during an active exposure.

### For Long Exposures (> 15 minutes)

If you need exposures longer than 15 minutes, you must configure the OAT firmware's RA limits to increase the safe tracking time.

**Example Configuration for 44-Minute Exposures:**

1. **Edit your OAT firmware configuration** (`Configuration_local.hpp`):
   ```cpp
   #define RA_LIMIT_LEFT 6.0f
   #define RA_LIMIT_RIGHT 6.0f
   ```

2. **Set the plugin threshold:** 45 minutes (or higher)

3. **Calculate your limits:**
   - Total safe time = 12h - (RA_LIMIT_LEFT + RA_LIMIT_RIGHT)
   - Example: 12h - (6h + 6h) = 0h at meridian
   - Usable time = Safe time - Threshold

**Configuration Examples:**

| RA Limits | Max Safe Time | Threshold | Max Exposure |
|-----------|---------------|-----------|--------------|
| Default | ~15-20 min | 5 min | ~10-15 min |
| 6.0 | 0 min at meridian | 45 min | ~40-44 min |
| 5.5 | 60 min | 55 min | ~50-54 min |
| 5.0 | 120 min | 115 min | ~110-114 min |

**Important Notes:**
- Reducing RA limits increases tracking time but reduces observable sky range
- Leave 5+ minutes safety margin between threshold and exposure time
- Test configuration with shorter exposures first
- Monitor flip time display to verify calculations

## How it Works

### Safe Time Monitoring
1. Polls mount firmware using `:XGST#` and parses safe tracking time (hours)
2. Calculates flip time: `Current Time + (Safe Time - Threshold)`
3. Displays calculated flip time in UI
4. When safe time ≤ threshold, trigger fires

### Meridian Flip Sequence
When safe time drops below threshold:

1. Waits for active exposure to finish (max 5 minutes)
2. If `Pause Guiding During Flip` enabled and guiding active, stops guider
3. Obtains current target coordinates (RA/Dec) from NINA
4. Slews to coordinates (OAT firmware selects correct pier side)
5. If `Auto Focus After Flip` enabled, runs autofocus routine
6. If `Recenter After Flip` enabled, runs plate-solve centering
7. If guiding was active, restarts guiding
   - Passes `Force Calibration After Flip` flag if enabled

> **Note**: Plugin uses NINA's standard mount settle behavior after slews.

## Monitoring and Troubleshooting

### Visual Feedback
- **Flip time display**: Updates every polling interval
- **Main sequencer**: Shows flip time on right side of trigger
- **Advanced sequencer**: Shows compact view with flip time

### Logs
Check NINA logs for messages prefixed with:
- `OAT Safe Time` - Safe time polling and calculation
- `OAT Safe Time Flip` - Flip execution and post-flip operations

### Common Issues

<details>
<summary><b>Trigger doesn't fire</b></summary>

- Verify telescope is connected in NINA
- Test `:XGST#` command using Equipment Console
- Ensure threshold is larger than longest exposure time
- Check logs for safe time values
</details>

<details>
<summary><b>Trigger fires during exposures</b></summary>

- Increase safe time threshold to be higher than exposure time
- For exposures > 15 minutes, adjust OAT firmware RA limits (see above)
</details>

<details>
<summary><b>Recenter fails</b></summary>

- Verify plate solver is configured in NINA Options
- ASTAP recommended for best results
- Simulator cameras won't work (no stars to solve)
- Check camera is connected and functional
</details>

<details>
<summary><b>Guiding doesn't resume</b></summary>

- Verify guider connection
- Try enabling `Force Calibration After Flip`
- Check guider logs for errors
</details>

<details>
<summary><b>Auto focus fails</b></summary>

- Verify focuser is connected
- Check focuser responds to manual commands
- Review auto focus settings in NINA
</details>

## Tested With

- **NINA Version**: 3.1 HF2
- **OpenAstroTracker ASCOM Driver**: V6.6.7.2 Release
- **OpenAstroTracker Firmware**: v1.13.9

## Version History

### v1.2.0 (Current)

**UI Improvements:**
- Added flip time calculation and display (matches NINA's meridian flip trigger)
- Removed threshold display from main view (accessible via ⋯ button)
- Settings now in compact expander menu (⋯ button)
- All settings left-aligned for better readability
- Theme-aware UI matching NINA's design

**Advanced Sequencer Integration:**
- Added MiniTrigger template for Advanced Sequencer dockable
- Flip time displays in Imaging panel's sequencer view
- Lightning bolt icon indicates trigger status

**Code Improvements:**
- Added `EarliestFlipTime` and `LatestFlipTime` properties
- Automatic flip time calculation based on safe time and threshold
- Property change notifications for UI updates

### v1.1.0
- Added Auto Focus After Flip option with IAutoFocusVMFactory support
- Added Recenter After Flip option with IPlateSolverFactory support
- Added Force Calibration After Flip to control guide recalibration
- Improved plate solving integration using profile settings
- Removed settle time (relies on NINA's mount settle behavior)

### v1.0.0
- Initial release
- OAT Safe Time Flip trigger with configurable threshold
- Guiding pause/resume functionality

## License

This plugin follows the same license as NINA (MPL 2.0).