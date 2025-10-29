# Quick Start Guide - OAT Safe Time Flip

## Setup

1. Add the trigger to your sequence:
   - Open your imaging sequence
   - Add a Trigger ? `OAT Safe Time Sync` ? `OAT Safe Time Flip`
2. Configure the trigger settings by clicking the **?** button

## What You'll See

Once added, the trigger displays:
- **? button** (left side): Click to access all settings
- **Flip Time** (right side): Shows when the flip will occur (e.g., "Flip Time: 23:45:30")

The flip time also appears in the **Advanced Sequencer** dockable in the Imaging panel.

## Default Settings

Click the **?** button to view and adjust:
- Safe Time Threshold: 5 minutes
- Polling Interval: 15 seconds
- Pause Guiding During Flip: Enabled (recommended)
- Force Calibration After Flip: Disabled
- Auto Focus After Flip: Disabled
- Recenter After Flip: Disabled

## How It Works

Once added, the trigger:

1. **Monitors** - Polls your mount's safe time every 15 seconds (default)
2. **Calculates** - Shows when flip will occur (Current Time + Safe Time - Threshold)
3. **Triggers** - When safe time ? threshold:
   - Waits for any active exposure to finish
   - Pauses guiding (if enabled)
   - Slews to current RA/Dec target (OAT firmware chooses pier side)
   - Runs autofocus/recenter if enabled
   - Resumes guiding (with optional recalibration)

## Recommended Threshold Adjustments

| Exposure Length | Threshold | Polling |
|----------------|-----------|---------|
| < 2 min | 2–3 min | 10 sec |
| 2–5 min | 5 min | 15 sec |
| > 5 min | 8–10 min | 20 sec |

**Tip**: Set threshold larger than your longest exposure to avoid interrupting exposures.

## Troubleshooting Quick Checks

**Not seeing flip time?**
- Check telescope is connected in NINA
- Verify mount firmware supports `:XGST#` (test in Equipment Console)
- Look for safe time values in NINA logs (search "OAT Safe Time")

**Flip not happening?**
- Is threshold larger than current safe time?
- Check logs for "OAT Safe Time Flip" messages

**Post-flip issues?**
- **Recenter fails**: Check plate solver configuration (ASTAP recommended)
- **Guiding fails**: Try enabling "Force Calibration After Flip"
- **Auto focus fails**: Verify focuser connection

## Tested With

- NINA Version 3.1 HF2
- OpenAstroTracker ASCOM Driver V6.6.7.2 Release
- OpenAstroTracker Firmware v1.13.9

---

**For detailed information**, see `README.md` in the plugin folder.
