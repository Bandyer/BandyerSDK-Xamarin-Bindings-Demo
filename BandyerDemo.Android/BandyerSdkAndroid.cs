using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Util;
using BandyerDemo.Droid;
using Com.Bandyer.Android_sdk;
using Com.Bandyer.Android_sdk.Call;
using Com.Bandyer.Android_sdk.Call.Model;
using Com.Bandyer.Android_sdk.Call.Notification;
using Com.Bandyer.Android_sdk.Chat;
using Com.Bandyer.Android_sdk.Client;
using Com.Bandyer.Android_sdk.Intent;
using Com.Bandyer.Android_sdk.Intent.Call;
using Com.Bandyer.Android_sdk.Intent.Chat;
using Com.Bandyer.Android_sdk.Module;
using Com.Bandyer.Android_sdk.Utils.Provider;
using Java.Lang;
using Xamarin.Forms;

[assembly: Dependency(typeof(BandyerSdkAndroid))]
namespace BandyerDemo.Droid
{
    public class BandyerSdkAndroid : Java.Lang.Object
        , BandyerSdkForms.IBandyerSdk
        , IBandyerSDKClientObserver
        , IBandyerModuleObserver
        , ICallUIObserver
        , ICallObserver
        , IChatUIObserver
        , IChatObserver
    {
        const string TAG = "BandyerDemo";
        public static Android.App.Application Application;
        private static BandyerSdkUserDetailsProvider userDetailsProvider;
        public static Android.App.Activity MainActivity;
        private static string joinUrlFromIntent;
        private static bool shouldStartCall = false;

        #region IBandyerSDKClientObserver
        public void OnClientError(Throwable throwable)
        {
            Log.Debug(TAG, "OnClientError " + throwable);
        }

        public void OnClientReady()
        {
            Log.Debug(TAG, "OnClientReady");
        }

        public void OnClientStatusChange(BandyerSDKClientState state)
        {
            Log.Debug(TAG, "OnClientStatusChange " + state);
        }

        public void OnClientStopped()
        {
            Log.Debug(TAG, "OnClientStopped");
        }
        #endregion

        #region IBandyerModuleObserver
        public void OnModuleFailed(IBandyerModule module, Throwable throwable)
        {
            Log.Debug(TAG, "OnModuleFailed " + module + " " + throwable);
        }

        public void OnModulePaused(IBandyerModule module)
        {
            Log.Debug(TAG, "OnModulePaused " + module);
        }

        public void OnModuleReady(IBandyerModule module)
        {
            Log.Debug(TAG, "OnModuleReady " + module);
        }

        public void OnModuleStatusChanged(IBandyerModule module, BandyerModuleStatus moduleStatus)
        {
            Log.Debug(TAG, "OnModuleStatusChanged " + module + " " + moduleStatus);

            if (module.Name == "ChatModule" && (
                module.Status == BandyerModuleStatus.Disconnected
                || module.Status == BandyerModuleStatus.Reconnecting
                || module.Status == BandyerModuleStatus.Ready
                || module.Status == BandyerModuleStatus.Connected
                ))
            {
                ChatStatus(true);
            }
            else if (module.Name == "ChatModule")
            {
                ChatStatus(false);
            }

            if (module.Name == "CallModule" && (
                module.Status == BandyerModuleStatus.Ready
                || module.Status == BandyerModuleStatus.Connected
                ))
            {
                CallStatus(true);
            }
            else if (module.Name == "CallModule")
            {
                CallStatus(false);
            }

            if (module.Name == "CallModule" && module.Status == BandyerModuleStatus.Connected)
            {
                if (shouldStartCall)
                {
                    shouldStartCall = false;
                    startCallFromJoinUrl(joinUrlFromIntent);
                }
            }
        }
        #endregion

        public static void InitSdk(Android.App.Application application)
        {
            Application = application;
            userDetailsProvider = new BandyerSdkUserDetailsProvider();
            BandyerSDK.Builder builder = new BandyerSDK.Builder(application, BandyerSdkForms.AppId)
                .SetEnvironment(Com.Bandyer.Android_sdk.Environment.Configuration.Sandbox())
                .WithCallEnabled(new BandyerSdkCallNotificationListener())
                .WithFileSharingEnabled()
                .WithWhiteboardEnabled()
                .WithChatEnabled()
                .WithUserDetailsProvider(userDetailsProvider);
            BandyerSDK.Init(builder);
        }

        public static void SetIntent(Intent intent)
        {
            if (intent == null || intent.Data == null)
            {
                return;
            }
            Log.Debug(TAG, "SetIntent " + intent.Data);
            var url = intent.Data.ToString();
            if (url.StartsWith("https://sandbox.bandyer.com/connect/rest-call-handler/"))
            {
                joinUrlFromIntent = url;
                shouldStartCall = true;
            }
        }

        #region IBandyerSdk
        public event Action<bool> CallStatus;
        public event Action<bool> ChatStatus;

        public void Init(string userAlias)
        {
            if (BandyerSDKClient.Instance.State == BandyerSDKClientState.Uninitialized)
            {
                Log.Debug(TAG, "IBandyerSdk Init " + userAlias);

                BandyerSDKClient.Instance.AddObserver(this);
                BandyerSDKClient.Instance.AddModuleObserver(this);

                BandyerSDKClientOptions options = new BandyerSDKClientOptions.Builder().Build();
                BandyerSDKClient.Instance.Init(userAlias, options);
                BandyerSDKClient.Instance.StartListening();

                BandyerSDKClient.Instance.ChatModule.AddChatUIObserver(this);
                BandyerSDKClient.Instance.ChatModule.AddChatObserver(this);

                BandyerSDKClient.Instance.CallModule.AddCallUIObserver(this);
                BandyerSDKClient.Instance.CallModule.AddCallObserver(this);
            }
        }

        public void StartCall(List<string> userAliases, List<BandyerSdkForms.CallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            startCallWithUserAliases(userAliases, callCapabilities, inCallCapabilities, inCallOptions);
        }

        void startCallWithUserAliases(List<string> userAliases, List<BandyerSdkForms.CallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            CallCapabilities capabilities = new CallCapabilities();
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.Whiteboard))
            {
                capabilities.WithWhiteboard();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.FileSharing))
            {
                capabilities.WithFileSharing();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.Chat))
            {
                capabilities.WithChat();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.ScreenSharing))
            {
                capabilities.WithScreenSharing();
            }

            CallOptions options = new CallOptions();
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.CallRecording))
            {
                options.WithRecordingEnabled(); // if the call started should be recorded
            }
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.BackCameraAsDefault))
            {
                options.WithBackCameraAsDefault(); // if the call should start with back camera
            }
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.DisableProximitySensor))
            {
                options.WithProximitySensorDisabled(); // if the proximity sensor should be disabled during calls
            }

            BandyerIntent.Builder builder = new BandyerIntent.Builder();
            CallIntentBuilder callIntentBuilder;
            if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioVideo))
            {
                callIntentBuilder = builder.StartWithAudioVideoCall(MainActivity.Application /* context */ );
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioUpgradable))
            {
                callIntentBuilder = builder.StartWithAudioUpgradableCall(MainActivity.Application); // audio call that may upgrade into audio&video call
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioOnly))
            {
                callIntentBuilder = builder.StartWithAudioCall(MainActivity.Application);  // audio only call
            }
            else
            {
                callIntentBuilder = builder.StartWithAudioVideoCall(MainActivity.Application /* context */ );
            }
            CallIntentOptions callIntentOptions = callIntentBuilder.With(userAliases);
            callIntentOptions.WithCapabilities(capabilities); // optional
            callIntentOptions.WithOptions(options); // optional
            BandyerIntent bandyerCallIntent = callIntentOptions.Build();

            MainActivity.StartActivity(bandyerCallIntent);
        }

        void startCallFromJoinUrl(string joinUrl)
        {
            CallCapabilities capabilities = new CallCapabilities();
            capabilities.WithWhiteboard();
            capabilities.WithFileSharing();
            capabilities.WithChat();
            capabilities.WithScreenSharing();

            CallOptions options = new CallOptions();
            //options.WithRecordingEnabled(); // if the call started should be recorded
            //options.WithBackCameraAsDefault(); // if the call should start with back camera
            //options.WithProximitySensorDisabled(); // if the proximity sensor should be disabled during calls

            BandyerIntent.Builder builder = new BandyerIntent.Builder();
            CallIntentOptions callIntentOptions = builder.StartFromJoinCallUrl(MainActivity.Application, joinUrl);
            callIntentOptions.WithCapabilities(capabilities); // optional
            callIntentOptions.WithOptions(options); // optional
            BandyerIntent bandyerCallIntent = callIntentOptions.Build();

            MainActivity.StartActivity(bandyerCallIntent);
        }

        public void StartChat(string userAlias, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            CallCapabilities capabilities = new CallCapabilities();
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.Whiteboard))
            {
                capabilities.WithWhiteboard();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.FileSharing))
            {
                capabilities.WithFileSharing();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.Chat))
            {
                capabilities.WithChat();
            }
            if (inCallCapabilities.Contains(BandyerSdkForms.InCallCapability.ScreenSharing))
            {
                capabilities.WithScreenSharing();
            }

            CallOptions options = new CallOptions();
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.CallRecording))
            {
                options.WithRecordingEnabled(); // if the call started should be recorded
            }
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.BackCameraAsDefault))
            {
                options.WithBackCameraAsDefault(); // if the call should start with back camera
            }
            if (inCallOptions.Contains(BandyerSdkForms.InCallOptions.DisableProximitySensor))
            {
                options.WithProximitySensorDisabled(); // if the proximity sensor should be disabled during calls
            }

            BandyerIntent.Builder builder = new BandyerIntent.Builder();
            ChatIntentBuilder chatIntentBuilder = builder.StartWithChat(MainActivity.Application /* context */ );
            ChatIntentOptions chatIntentOptions = chatIntentBuilder.With(userAlias);
            if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioOnly))
            {
                chatIntentOptions.WithAudioCallCapability(capabilities, options); // optional
            }
            if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioUpgradable))
            {
                chatIntentOptions.WithAudioUpgradableCallCapability(capabilities, options); // optionally upgradable to audio video call
            }
            if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioVideo))
            {
                chatIntentOptions.WithAudioVideoCallCapability(capabilities, options); // optional
            }
            BandyerIntent bandyerChatIntent = chatIntentOptions.Build();

            MainActivity.StartActivity(bandyerChatIntent);
        }

        public void OnPageAppearing()
        {
        }

        public void OnPageDisappearing()
        {
        }

        public void SetUserDetails(List<BandyerSdkForms.User> usersDetails)
        {
            userDetailsProvider.usersDetails = usersDetails;
        }

        public void Stop()
        {
            BandyerSDKClient.Instance.StopListening();
            BandyerSDKClient.Instance.ClearUserCache();
            BandyerSDKClient.Instance.Dispose();
        }
        #endregion

        #region ICallUIObserver
        public void OnActivityDestroyed(ICall call, Java.Lang.Ref.WeakReference activity)
        {
            Log.Debug(TAG, "onCallActivityDestroyed: "
               + call.CallInfo.Caller + ", "
               + System.String.Join(", ", call.CallInfo.Callees));
        }

        public void OnActivityError(ICall call, Java.Lang.Ref.WeakReference activity, CallException error)
        {
            Log.Debug(TAG, "onCallActivityDestroyed: "
              + call.CallInfo.Caller + ", "
              + System.String.Join(", ", call.CallInfo.Callees)
              + "\n"
              + "exception: " + error.Message);
        }

        public void OnActivityStarted(ICall call, Java.Lang.Ref.WeakReference activity)
        {
            Log.Debug(TAG, "onCallActivityStarted: "
               + call.CallInfo.Caller + ", "
               + System.String.Join(", ", call.CallInfo.Callees));
        }
        #endregion

        #region ICallObserver
        public void OnCallCreated(ICall call)
        {
            Log.Debug(TAG, "onCallCreated: "
               + call.CallInfo.Caller + ", "
               + System.String.Join(", ", call.CallInfo.Callees));
        }

        public void OnCallEnded(ICall call)
        {
            Log.Debug(TAG, "onCallEnded: "
               + call.CallInfo.Caller + ", "
               + System.String.Join(", ", call.CallInfo.Callees));
        }

        public void OnCallEndedWithError(ICall call, CallException callException)
        {
            Log.Debug(TAG, "onCallEndedWithError: "
              + call.CallInfo.Caller + ", "
              + System.String.Join(", ", call.CallInfo.Callees)
              + "\n"
              + "exception: " + callException.Message);
        }

        public void OnCallStarted(ICall call)
        {
            Log.Debug(TAG, "onCallStarted: "
               + call.CallInfo.Caller + ", "
               + System.String.Join(", ", call.CallInfo.Callees));
        }
        #endregion

        #region IChatUIObserver
        public void OnActivityDestroyed(IChat chat, Java.Lang.Ref.WeakReference activity)
        {
            Log.Debug(TAG, "onChatActivityDestroyed");
        }

        public void OnActivityError(IChat chat, Java.Lang.Ref.WeakReference activity, ChatException error)
        {
            Log.Debug(TAG, "onChatActivityError " + error.Message);
        }

        public void OnActivityStarted(IChat chat, Java.Lang.Ref.WeakReference activity)
        {
            Log.Debug(TAG, "onChatActivityStarted");
        }
        #endregion

        #region IChatObserver
        public void OnChatEnded()
        {
            Log.Debug(TAG, "OnChatEnded");
        }

        public void OnChatEndedWithError(ChatException chatException)
        {
            Log.Debug(TAG, "OnChatEndedWithError " + chatException.Message);
        }

        public void OnChatStarted()
        {
            Log.Debug(TAG, "OnChatStarted");
        }
        #endregion

        class BandyerSdkCallNotificationListener : Java.Lang.Object
            , ICallNotificationListener
        {
            public void OnCreateNotification(ICallInfo callInfo, CallNotificationType type, ICallNotificationStyle notificationStyle)
            {
                notificationStyle.SetNotificationColor(Android.Graphics.Color.Red);
            }

            public void OnIncomingCall(IIncomingCall call, bool isDnd, bool isScreenLocked)
            {
                call.WithCapabilities(GetDefaultCallCapabilities());
                call.WithOptions(GetDefaultIncomingCallOptions());
                if (!isDnd || isScreenLocked)
                {
                    call.Show(Application);
                }
                else
                {
                    call.AsNotification().Show(Application);
                }
            }

            private CallCapabilities GetDefaultCallCapabilities()
            {
                return new CallCapabilities()
                        .WithChat()
                        .WithWhiteboard()
                        .WithScreenSharing()
                        .WithFileSharing();
            }
            private IncomingCallOptions GetDefaultIncomingCallOptions()
            {
                return new IncomingCallOptions();
            }
        }

        class BandyerSdkUserDetailsProvider : Java.Lang.Object
            , IUserDetailsProvider
        {
            internal List<BandyerSdkForms.User> usersDetails;

            public void OnUserDetailsRequested(IList<string> userAliases, IOnUserDetailsListener onUserDetailsListener)
            {
                Java.Util.ArrayList details = new Java.Util.ArrayList();
                foreach (string userAlias in userAliases)
                {
                    var userByAlias = usersDetails.Find(u => u.Alias == userAlias);
                    var builder = new UserDetails.Builder(userAlias);
                    builder.WithNickName(userByAlias.NickName);
                    builder.WithFirstName(userByAlias.FirstName);
                    builder.WithLastName(userByAlias.LastName);
                    builder.WithEmail(userByAlias.Email);
                    if (!System.String.IsNullOrEmpty(userByAlias.ImageUri))
                    {
                        var fileExt = Path.GetExtension(userByAlias.ImageUri);
                        var fileName = userByAlias.ImageUri.Substring(0, userByAlias.ImageUri.Length - fileExt.Length);
                        var resId = (int)typeof(Resource.Drawable).GetField(fileName).GetValue(null);
                        builder.WithImageUri(
                            Android.Net.Uri.Parse(
                                ContentResolver.SchemeAndroidResource
                                + "://" + Application.Resources.GetResourcePackageName(resId)
                                + "/" + Application.Resources.GetResourceTypeName(resId)
                                + "/" + Application.Resources.GetResourceEntryName(resId)));
                    }
                    details.Add(builder.Build());
                }
                onUserDetailsListener.Provide(details);
            }
        }

    }
}
