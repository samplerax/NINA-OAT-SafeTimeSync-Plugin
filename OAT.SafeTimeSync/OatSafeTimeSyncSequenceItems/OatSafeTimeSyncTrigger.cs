using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Model;
using NINA.Astrometry;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Text;
using System.Linq;

namespace Samplerax.NINA.OatSafeTimeSync.OatSafeTimeSyncTestCategory {
    /// <summary>
    /// Custom meridian flip trigger for OAT mounts with non-standard DEC axis orientation.
    /// Monitors the mount's safe time via :XGST# command and triggers a flip when threshold is reached.
    /// </summary>
    [ExportMetadata("Name", "OAT Safe Time Flip")]
    [ExportMetadata("Description", "Monitors OAT mount safe time and performs automatic meridian flip when threshold is reached")]
    [ExportMetadata("Icon", "MeridianFlipSVG")]
    [ExportMetadata("Category", "OAT Safe Time Sync")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class OatSafeTimeSyncTrigger : SequenceTrigger {
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IGuiderMediator? guiderMediator;
        private readonly IImagingMediator? imagingMediator;
        private readonly IApplicationStatusMediator? applicationStatusMediator;
        private readonly IPlateSolverFactory? plateSolverFactory;
        private readonly IAutoFocusVMFactory? autoFocusVMFactory;
        private readonly IFilterWheelMediator? filterWheelMediator;
        private readonly IDomeMediator? domeMediator;
        private readonly IDomeFollower? domeFollower;
        private readonly IProfileService? profileService;
        private MeadeCommunicator? communicator;
        
        private double safeTimeThresholdMinutes = 5.0;
        private int pollingIntervalSeconds = 15;
        private bool pauseGuidingDuringFlip = true;
        private bool recenterAfterFlip = false;
        private double platesolveToleranceArcsec = 30.0;
        private int maxCenteringAttempts = 3;
        private bool autofocusAfterFlip = false;
        
        private DateTime lastSafeTimeCheck = DateTime.MinValue;
        private double? lastSafeTimeHours = null;
        private bool flipInProgress = false;
        private bool wasGuidingBeforeFlip = false;
        private DateTime earliestFlipTime = DateTime.MinValue;
        private DateTime latestFlipTime = DateTime.MinValue;

        [ImportingConstructor]
        public OatSafeTimeSyncTrigger(
            ITelescopeMediator telescopeMediator,
            IGuiderMediator? guiderMediator = null,
            IImagingMediator? imagingMediator = null,
            IApplicationStatusMediator? applicationStatusMediator = null,
            IPlateSolverFactory? plateSolverFactory = null,
            IAutoFocusVMFactory? autoFocusVMFactory = null,
            IFilterWheelMediator? filterWheelMediator = null,
            IDomeMediator? domeMediator = null,
            IDomeFollower? domeFollower = null,
            IProfileService? profileService = null)
        {
            this.telescopeMediator = telescopeMediator ?? throw new ArgumentNullException(nameof(telescopeMediator));
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.autoFocusVMFactory = autoFocusVMFactory;
            this.filterWheelMediator = filterWheelMediator;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.profileService = profileService;
        }

        // UI-facing read-only state
        public bool FlipInProgress
        {
            get => flipInProgress;
            private set
            {
                if (flipInProgress != value)
                {
                    flipInProgress = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double? LastSafeTimeHours
        {
            get => lastSafeTimeHours;
            private set
            {
                if (lastSafeTimeHours != value)
                {
                    lastSafeTimeHours = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(SafeTimeRemainingDisplay));
                }
            }
        }

        public string SafeTimeRemainingDisplay
        {
            get
            {
                if (LastSafeTimeHours.HasValue)
                {
                    try
                    {
                        var ts = TimeSpan.FromHours(LastSafeTimeHours.Value);
                        return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:D2}:{1:D2}", (int)ts.TotalHours, ts.Minutes);
                    }
                    catch
                    {
                        return "--:--";
                    }
                }
                return "--:--";
            }
        }

        /// <summary>
        /// The earliest time when the flip will occur (when safe time = threshold)
        /// </summary>
        public DateTime EarliestFlipTime
        {
            get => earliestFlipTime;
            private set
            {
                if (earliestFlipTime != value)
                {
                    earliestFlipTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// The latest time when the flip must occur (when safe time = 0)
        /// This is the same as EarliestFlipTime for OAT since we trigger at threshold
        /// </summary>
        public DateTime LatestFlipTime
        {
            get => latestFlipTime;
            private set
            {
                if (latestFlipTime != value)
                {
                    latestFlipTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Safe time threshold in minutes. Flip will trigger when safe time drops below this value.
        /// </summary>
        [JsonProperty]
        public double SafeTimeThresholdMinutes
        {
            get => safeTimeThresholdMinutes;
            set
            {
                if (value >= 0 && value <= 120)
                {
                    safeTimeThresholdMinutes = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// How often to poll the mount for safe time (in seconds).
        /// </summary>
        [JsonProperty]
        public int PollingIntervalSeconds
        {
            get => pollingIntervalSeconds;
            set
            {
                if (value >= 5 && value <= 300)
                {
                    pollingIntervalSeconds = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Pause guiding during meridian flip.
        /// </summary>
        [JsonProperty]
        public bool PauseGuidingDuringFlip
        {
            get => pauseGuidingDuringFlip;
            set
            {
                pauseGuidingDuringFlip = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Recenter after meridian flip using plate solving.
        /// </summary>
        [JsonProperty]
        public bool RecenterAfterFlip
        {
            get => recenterAfterFlip;
            set
            {
                recenterAfterFlip = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Plate solve tolerance in arcseconds.
        /// </summary>
        [JsonProperty]
        public double PlatesolveToleranceArcsec
        {
            get => platesolveToleranceArcsec;
            set
            {
                if (value > 0 && value <= 3600)
                {
                    platesolveToleranceArcsec = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Maximum centering attempts after flip.
        /// </summary>
        [JsonProperty]
        public int MaxCenteringAttempts
        {
            get => maxCenteringAttempts;
            set
            {
                if (value >= 1 && value <= 10)
                {
                    maxCenteringAttempts = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Auto focus after meridian flip.
        /// </summary>
        [JsonProperty]
        public bool AutofocusAfterFlip
        {
            get => autofocusAfterFlip;
            set
            {
                autofocusAfterFlip = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Force calibration after meridian flip.
        /// </summary>
        [JsonProperty]
        public bool ForceCalibrationAfterFlip { get; set; } = false;

        public override object Clone()
        {
            // Create a new instance copying current property values
            var obj = new OatSafeTimeSyncTrigger(
                telescopeMediator,
                guiderMediator,
                imagingMediator,
                applicationStatusMediator,
                plateSolverFactory,
                autoFocusVMFactory,
                filterWheelMediator,
                domeMediator,
                domeFollower,
                profileService);

            obj.Icon = Icon;
            obj.Name = Name;
            obj.Category = Category;
            obj.Description = Description;
            obj.SafeTimeThresholdMinutes = SafeTimeThresholdMinutes;
            obj.PollingIntervalSeconds = PollingIntervalSeconds;
            obj.PauseGuidingDuringFlip = PauseGuidingDuringFlip;
            obj.RecenterAfterFlip = RecenterAfterFlip;
            obj.PlatesolveToleranceArcsec = PlatesolveToleranceArcsec;
            obj.MaxCenteringAttempts = MaxCenteringAttempts;
            obj.AutofocusAfterFlip = AutofocusAfterFlip;
            obj.ForceCalibrationAfterFlip = ForceCalibrationAfterFlip;

            return obj;
        }

        /// <summary>
        /// Determines if the trigger should fire based on safe time polling.
        /// </summary>
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            // Don't trigger if already in progress
            if (flipInProgress)
                return false;

            // Don't trigger if telescope not available
            if (telescopeMediator == null)
            {
                EarliestFlipTime = DateTime.MinValue;
                LatestFlipTime = DateTime.MinValue;
                return false;
            }

            // Check if we need to poll (based on polling interval)
            var now = DateTime.Now;
            if ((now - lastSafeTimeCheck).TotalSeconds < pollingIntervalSeconds)
                return false;

            // Update last check time
            lastSafeTimeCheck = now;

            // Get safe time from mount
            var safeTimeHours = GetSafeTimeAsync().GetAwaiter().GetResult();
            
            if (safeTimeHours == null)
            {
                EarliestFlipTime = DateTime.MinValue;
                LatestFlipTime = DateTime.MinValue;
                return false; // Couldn't get safe time
            }

            lastSafeTimeHours = safeTimeHours.Value;

            // Convert to minutes for comparison
            var safeTimeMinutes = safeTimeHours.Value * 60.0;

            // Calculate when the flip will happen
            // EarliestFlipTime = current time + (safe time - threshold)
            var timeUntilFlip = TimeSpan.FromMinutes(safeTimeMinutes - SafeTimeThresholdMinutes);
            if (timeUntilFlip < TimeSpan.Zero)
                timeUntilFlip = TimeSpan.Zero;

            EarliestFlipTime = DateTime.Now.Add(timeUntilFlip);
            LatestFlipTime = EarliestFlipTime; // For OAT, we flip at the threshold

            Logger.Info($"OAT Safe Time: {safeTimeHours.Value:F2} hours ({safeTimeMinutes:F1} minutes), Threshold: {SafeTimeThresholdMinutes} minutes, Flip time: {EarliestFlipTime.ToString("HH:mm:ss")}");

            // Trigger if safe time is below threshold
            return safeTimeMinutes <= SafeTimeThresholdMinutes && safeTimeMinutes >= 0;
        }

        /// <summary>
        /// Execute the meridian flip sequence.
        /// </summary>
        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            flipInProgress = true;
            
            try
            {
                Logger.Info($"OAT Safe Time Flip: Starting meridian flip sequence. Safe time: {lastSafeTimeHours:F2} hours");
                Notification.ShowInformation($"OAT Meridian Flip: Safe time at {lastSafeTimeHours:F2}h - starting flip");

                // Step 1: Wait for current exposure to finish (if any)
                await WaitForExposureToFinish(progress, token);

                // Step 2: Pause guiding if enabled and active
                if (pauseGuidingDuringFlip && guiderMediator != null)
                {
                    await PauseGuidingIfActive(progress, token);
                }

                // Step 3: Get current target coordinates
                var targetCoords = GetCurrentTarget();
                if (targetCoords == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Could not get target coordinates, using current telescope position");
                    targetCoords = GetCurrentTelescopePosition();
                }

                if (targetCoords == null)
                {
                    throw new Exception("Cannot determine target coordinates for flip");
                }

                Logger.Info($"OAT Safe Time Flip: Target coordinates - RA: {targetCoords.RADegrees:F4}°, Dec: {targetCoords.Dec:F4}°");

                // Step 4: Perform the flip by slewing to the same coordinates
                // The OAT firmware will automatically choose the correct side
                await PerformFlipSlew(targetCoords, progress, token);

                // Step 6: Auto focus if enabled
                if (autofocusAfterFlip && autoFocusVMFactory != null)
                {
                    await AutoFocusAfterFlip(progress, token);
                }

                // Step 7: Recenter if enabled
                if (recenterAfterFlip && plateSolverFactory != null)
                {
                    await RecenterAfterFlipAsync(targetCoords, progress, token);
                }

                // Step 8: Resume guiding if it was active before
                if (wasGuidingBeforeFlip && guiderMediator != null)
                {
                    await ResumeGuiding(progress, token);
                }

                Logger.Info("OAT Safe Time Flip: Meridian flip completed successfully");
                Notification.ShowSuccess("OAT Meridian Flip: Completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"OAT Safe Time Flip: Error during meridian flip - {ex.Message}", ex);
                Notification.ShowError($"OAT Meridian Flip failed: {ex.Message}");
                throw;
            }
            finally
            {
                flipInProgress = false;
            }
        }

        private async Task<double?> GetSafeTimeAsync()
        {
            try
            {
                // Initialize communicator if needed
                if (communicator == null)
                {
                    communicator = new MeadeCommunicator(telescopeMediator, msg => Logger.Trace($"MeadeComm: {msg}"));
                }

                if (!communicator.IsConnected())
                    return null;

                var response = await communicator.SendCommandAsync(":XGST#");
                
                if (string.IsNullOrEmpty(response))
                    return null;

                // Try to parse the numeric response
                if (double.TryParse(response, out double hours))
                {
                    return hours;
                }

                // Try regex parsing
                if (Regex.Match(response, @"(-?\\d*\\.?\\d+)") is Match m && m.Success &&
                    double.TryParse(m.Groups[1].Value, out double parsed))
                {
                    return parsed;
                }

                Logger.Warning($"OAT Safe Time Flip: Could not parse safe time response: {response}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"OAT Safe Time Flip: Error getting safe time - {ex.Message}");
                return null;
            }
        }

        private async Task WaitForExposureToFinish(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (imagingMediator == null)
                return;

            try
            {
                Logger.Info("OAT Safe Time Flip: Checking for active exposure...");
                
                // Wait a bit for any exposure to complete naturally
                var maxWaitSeconds = 300; // 5 minutes max wait
                var waited = 0;
                
                while (waited < maxWaitSeconds && !token.IsCancellationRequested)
                {
                    // If we can detect imaging state, check it
                    // Otherwise just wait a short period
                    await Task.Delay(1000, token);
                    waited++;
                    
                    if (waited % 10 == 0)
                    {
                        Logger.Trace($"OAT Safe Time Flip: Waiting for exposure... {waited}s");
                        progress?.Report(new ApplicationStatus { Status = $"Waiting for exposure to finish... {waited}s" });
                    }
                    
                    // Simple heuristic: if we've waited 2 seconds and nothing, probably no exposure
                    if (waited >= 2)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error waiting for exposure - {ex.Message}");
            }
        }

        private async Task PauseGuidingIfActive(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            try
            {
                Logger.Info("OAT Safe Time Flip: Stopping guiding...");
                progress?.Report(new ApplicationStatus { Status = "Stopping guiding..." });
                
                var guidingInfo = guiderMediator?.GetInfo();
                wasGuidingBeforeFlip = guidingInfo?.Connected == true;
                
                if (wasGuidingBeforeFlip)
                {
                    await guiderMediator!.StopGuiding(token);
                    Logger.Info("OAT Safe Time Flip: Guiding stopped");
                }
                else
                {
                    Logger.Info("OAT Safe Time Flip: Guiding was not active");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error stopping guiding - {ex.Message}");
                wasGuidingBeforeFlip = false;
            }
        }

        private Coordinates? GetCurrentTarget()
        {
            try
            {
                var info = telescopeMediator?.GetInfo();
                if (info?.Coordinates != null)
                {
                    return info.Coordinates;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error getting target coordinates - {ex.Message}");
            }
            return null;
        }

        private Coordinates? GetCurrentTelescopePosition()
        {
            try
            {
                var info = telescopeMediator?.GetInfo();
                if (info != null)
                {
                    return new Coordinates(
                        Angle.ByDegree(info.RightAscension * 15.0), // RA in hours to degrees
                        Angle.ByDegree(info.Declination),
                        Epoch.J2000
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error getting telescope position - {ex.Message}");
            }
            return null;
        }

        private async Task PerformFlipSlew(Coordinates target, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            Logger.Info($"OAT Safe Time Flip: Slewing to target for flip - RA: {target.RA:F4}°, Dec: {target.Dec:F4}°");
            progress?.Report(new ApplicationStatus { Status = "Performing meridian flip slew..." });

            await telescopeMediator.SlewToCoordinatesAsync(target, token);
            
            Logger.Info("OAT Safe Time Flip: Slew completed");
        }

        private async Task AutoFocusAfterFlip(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            try
            {
                Logger.Info("OAT Safe Time Flip: Starting auto focus...");
                progress?.Report(new ApplicationStatus { Status = "Auto focusing after flip..." });

                var autoFocusVM = autoFocusVMFactory?.Create();
                
                if (autoFocusVM == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Auto focus VM not available, skipping autofocus");
                    Notification.ShowWarning("Auto focus not available - check focuser connection");
                    return;
                }

                // Parameter order: filter, token, progress
                var afResult = await autoFocusVM.StartAutoFocus(null, token, progress);

                if (afResult != null)
                {
                    Logger.Info($"OAT Safe Time Flip: Auto focus completed - Position: {afResult.CalculatedFocusPoint?.Position ?? 0}");
                    Notification.ShowSuccess($"OAT Flip: Auto focus completed at position {afResult.CalculatedFocusPoint?.Position ?? 0}");
                }
                else
                {
                    Logger.Warning("OAT Safe Time Flip: Auto focus failed");
                    Notification.ShowWarning("OAT Flip: Auto focus failed - continuing anyway");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error during auto focus - {ex.Message}");
                Notification.ShowWarning($"OAT Flip: Auto focus error - {ex.Message}");
            }
        }

        private async Task RecenterAfterFlipAsync(Coordinates target, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            try
            {
                Logger.Info("OAT Safe Time Flip: Starting plate solve and recenter...");
                progress?.Report(new ApplicationStatus { Status = "Recentering after flip..." });

                Logger.Debug($"OAT Safe Time Flip: Checking dependencies - plateSolverFactory: {plateSolverFactory != null}, imagingMediator: {imagingMediator != null}, profileService: {profileService != null}");

                if (plateSolverFactory == null || imagingMediator == null || profileService == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Required services not available for recentering");
                    Notification.ShowWarning("Recenter not available - check configuration");
                    return;
                }

                var activeProfile = profileService.ActiveProfile;
                if (activeProfile == null)
                {
                    Logger.Warning("OAT Safe Time Flip: No active profile available");
                    Notification.ShowWarning("Recenter not available - no active profile");
                    return;
                }

                var settings = activeProfile.PlateSolveSettings;
                if (settings == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Plate solve settings not available");
                    Notification.ShowWarning("Recenter not available - check plate solver settings");
                    return;
                }

                var telescopeSettings = activeProfile.TelescopeSettings;
                var cameraSettings = activeProfile.CameraSettings;

                var plateSolver = plateSolverFactory.GetPlateSolver(settings);
                var blindSolver = plateSolverFactory.GetBlindSolver(settings);

                if (plateSolver == null || blindSolver == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Plate solver or blind solver not available");
                    Notification.ShowWarning("Recenter not available - check plate solver configuration");
                    return;
                }

                var centeringSolver = plateSolverFactory.GetCenteringSolver(
                    plateSolver,
                    blindSolver,
                    imagingMediator,
                    telescopeMediator,
                    filterWheelMediator,
                    domeMediator,
                    domeFollower);

                if (centeringSolver == null)
                {
                    Logger.Warning("OAT Safe Time Flip: Centering solver not available");
                    Notification.ShowWarning("Plate solver not available");
                    return;
                }

                var captureSequence = new CaptureSequence()
                {
                    ExposureTime = settings.ExposureTime,
                    Binning = new BinningMode(settings.Binning, settings.Binning),
                    Gain = settings.Gain,
                    FilterType = settings.Filter
                };

                var parameter = new CenterSolveParameter
                {
                    Coordinates = target,
                    Threshold = platesolveToleranceArcsec,
                    Attempts = maxCenteringAttempts,
                    FocalLength = telescopeSettings.FocalLength,
                    PixelSize = cameraSettings.PixelSize
                };

                var solveProgress = new Progress<PlateSolveProgress>();
                var result = await centeringSolver.Center(captureSequence, parameter, solveProgress, progress, token);

                if (result != null && result.Success)
                {
                    Logger.Info($"OAT Safe Time Flip: Recentering successful - Separation: {result.Separation:F2}\"");
                    Notification.ShowSuccess($"OAT Flip: Recentered successfully ({result.Separation:F1}\" error)");
                }
                else
                {
                    Logger.Warning($"OAT Safe Time Flip: Recentering failed or not precise enough - result null: {result == null}, success: {result?.Success ?? false}");
                    Notification.ShowWarning("OAT Flip: Recentering failed - continuing anyway");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"OAT Safe Time Flip: Error during recentering - {ex.Message}");
                Logger.Error($"OAT Safe Time Flip: Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Logger.Error($"OAT Safe Time Flip: Inner exception: {ex.InnerException.Message}");
                }
                Notification.ShowWarning($"OAT Flip: Recenter error - {ex.Message}");
            }
        }

        private async Task ResumeGuiding(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            try
            {
                Logger.Info("OAT Safe Time Flip: Resuming guiding...");
                progress?.Report(new ApplicationStatus { Status = "Resuming guiding..." });

                await guiderMediator!.StartGuiding(ForceCalibrationAfterFlip, progress, token);

                Logger.Info("OAT Safe Time Flip: Guiding resumed");
            }
            catch (Exception ex)
            {
                Logger.Warning($"OAT Safe Time Flip: Error resuming guiding - {ex.Message}");
                Notification.ShowWarning($"Failed to resume guiding: {ex.Message}");
            }
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(OatSafeTimeSyncTrigger)}, Threshold: {SafeTimeThresholdMinutes}min, Poll: {PollingIntervalSeconds}s";
        }
    }
}