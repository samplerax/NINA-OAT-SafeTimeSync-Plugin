using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using Samplerax.NINA.OatSafeTimeSync.Properties;
using NINA.Equipment.Interfaces.Mediator;
using Settings = Samplerax.NINA.OatSafeTimeSync.Properties.Settings;

namespace Samplerax.NINA.OatSafeTimeSync {
    /// <summary>
    /// Plugin manifest and options host for the OAT Safe Time Sync plugin.
    /// Simplified: removed debug dockable UI and extra dockables so only the trigger remains.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class OatSafeTimeSync : PluginBase, INotifyPropertyChanged {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;

        [ImportingConstructor]
        public OatSafeTimeSync(IProfileService profileService, IOptionsVM options) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            // Minimal plugin - no extra UI or image save hooks
        }

        public override System.Threading.Tasks.Task Teardown() {
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            RaisePropertyChanged(nameof(ProfileSpecificNotificationMessage));
        }

        public string ProfileSpecificNotificationMessage {
            get { return pluginSettings.GetValueString(nameof(ProfileSpecificNotificationMessage), string.Empty); }
            set { pluginSettings.SetValueString(nameof(ProfileSpecificNotificationMessage), value); RaisePropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
