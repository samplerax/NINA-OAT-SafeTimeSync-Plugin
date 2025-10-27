using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9cdf7665-ceb5-47dd-9e2c-413e5f0a2eee")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("OAT Safe Time Sync")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Automatic meridian flip for OpenAstroTracker mounts using safe time monitoring")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Samplerax")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("OAT Safe Time Sync")]
[assembly: AssemblyCopyright("Copyright © 2025 Samplerax")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.2017")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/samplerax/NINA-OAT-SafeTimeSync-Plugin")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://github.com/samplerax/NINA-OAT-SafeTimeSync-Plugin")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "OpenAstroTracker,OAT,Meridian Flip,Safe Time,Astrophotography")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/samplerax/NINA-OAT-SafeTimeSync-Plugin/blob/main/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"OAT Safe Time Sync provides automatic meridian flip functionality specifically designed for OpenAstroTracker (OAT) mounts.

The plugin uses the OAT-specific :XGST# command to query safe tracking time directly from the firmware, providing accurate flip timing that accounts for the OAT's unique DEC axis orientation.

Features:
• Automatic flip time calculation and display (matching NINA's meridian flip trigger)
• Configurable safe time threshold and polling interval
• Optional guiding pause/resume with force calibration
• Optional autofocus after flip
• Optional plate-solve recentering after flip
• Compact UI with settings in expander menu
• Advanced Sequencer dockable integration

Perfect for OAT users who need reliable automated meridian flips without manual intervention.")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]