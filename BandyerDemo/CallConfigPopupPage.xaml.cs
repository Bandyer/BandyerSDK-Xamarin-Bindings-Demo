using System;
using System.Collections.Generic;
using System.Linq;
using Rg.Plugins.Popup.Pages;
using Rg.Plugins.Popup.Services;
using Xamarin.Forms;

namespace BandyerDemo
{
    public partial class CallConfigPopupPage : PopupPage
    {
        public CallConfigPopupPage()
        {
            InitializeComponent();
            if (Device.RuntimePlatform == Device.iOS)
            {
                checkboxWhiteboard.IsEnabled = false;
                checkboxFilesharing.IsEnabled = false;
                checkboxChat.IsEnabled = false;
                checkboxScreensharing.IsEnabled = false;
                checkboxCallRecording.IsEnabled = false;
                checkboxBackCamera.IsEnabled = false;
                checkboxDisableProximitySensor.IsEnabled = false;
                layoutBackCamera.IsVisible = false;
                layoutDisableProximitySensor.IsVisible = false;
                labelIos.IsVisible = true;
            }
        }

        async void Button_Cancel(System.Object sender, System.EventArgs e)
        {
            await PopupNavigation.Instance.PopAllAsync();
        }

        async void Button_Action(System.Object sender, System.EventArgs e)
        {
            await PopupNavigation.Instance.PopAllAsync(); // call before because rootViewController is busy with the popup
            var users = BandyerSdkForms.Instance.GetSelectedUsersNames();
            BandyerSdkForms.Instance.BandyerSdk.StartCall(users, GetCallCapabilities(), GetInCallCapabilities(), GetInCallOptions());
        }

        List<BandyerSdkForms.CallCapability> GetCallCapabilities()
        {
            var l = new List<BandyerSdkForms.CallCapability>();
            if (checkboxCallAudioOnly.IsChecked)
            {
                l.Add(BandyerSdkForms.CallCapability.AudioOnly);
            }
            if (checkboxCallAudioUpgradable.IsChecked)
            {
                l.Add(BandyerSdkForms.CallCapability.AudioUpgradable);
            }
            if (checkboxCallAudioVideo.IsChecked)
            {
                l.Add(BandyerSdkForms.CallCapability.AudioVideo);
            }
            return l;
        }

        List<BandyerSdkForms.InCallCapability> GetInCallCapabilities()
        {
            var l = new List<BandyerSdkForms.InCallCapability>();
            if (checkboxWhiteboard.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallCapability.Whiteboard);
            }
            if (checkboxFilesharing.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallCapability.FileSharing);
            }
            if (checkboxChat.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallCapability.Chat);
            }
            if (checkboxScreensharing.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallCapability.ScreenSharing);
            }
            return l;
        }


        List<BandyerSdkForms.InCallOptions> GetInCallOptions()
        {
            var l = new List<BandyerSdkForms.InCallOptions>();
            if (checkboxCallRecording.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallOptions.CallRecording);
            }
            if (checkboxBackCamera.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallOptions.BackCameraAsDefault);
            }
            if (checkboxDisableProximitySensor.IsChecked)
            {
                l.Add(BandyerSdkForms.InCallOptions.DisableProximitySensor);
            }
            return l;
        }
    }
}
