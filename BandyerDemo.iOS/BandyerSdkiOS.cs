using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using Bandyer;
using BandyerDemo.iOS;
using CallKit;
using CoreFoundation;
using Foundation;
using ObjCRuntime;
using PushKit;
using UIKit;
using Xamarin.Forms;
using Intents;
using System.Linq;
using System.IO;

[assembly: Dependency(typeof(BandyerSdkiOS))]
namespace BandyerDemo.iOS
{
    public class BandyerSdkiOS : NSObject
        , BandyerSdkForms.IBandyerSdk
        , IBCXCallClientObserver
        , IBDKCallWindowDelegate
        , IBCHChatClientObserver
        , IBCHChannelViewControllerDelegate
        , IPKPushRegistryDelegate
        , IBCHMessageNotificationControllerDelegate
        , IBDKCallBannerControllerDelegate
    {
        private static BandyerSdkiOS instance = null;
        public BandyerSdkiOS()
        {
            instance = this;
        }

        private BDKCallWindow callWindow = null;
        private IBDKIntent callIntent = null;
        private string currentUserAlias;
        private BCHMessageNotificationController messageNotificationController = null;
        private BDKCallBannerController callBannerController = null;
        private NSUrl webPageUrl;
        private bool shouldStartWindowCallFromWebPageUrl = false;
        private bool isSdkInitialized = false;
        private List<BandyerSdkForms.User> usersDetails;

        public static void InitSdk()
        {
            instance.InitSdkInt();
        }

        public static bool ContinueUserActivity(NSUserActivity userActivity)
        {
            return instance.ContinueUserActivityInt(userActivity);
        }

        void InitSdkInt()
        {
            if (!isSdkInitialized)
            {
                isSdkInitialized = true;
                var config = new BDKConfig();
                config.NotificationPayloadKeyPath = "data";
                config.PushRegistryDelegate = this;

                config.Environment = BDKEnvironment.Sandbox;

                // CALLKIT, enabled on real device and disabled on simulator because it's not supported yet by it.
                config.CallKitEnabled = Runtime.Arch != Arch.SIMULATOR;
                config.NativeUILocalizedName = "BanyerDemo App";
                //config.NativeUIRingToneSound = "MyRingtoneSound";
                UIImage callKitIconImage = UIImage.FromBundle("bandyer_logo");
                config.NativeUITemplateIconImageData = callKitIconImage.AsPNG();
                config.SupportedHandleTypes = new NSSet(new object[] { CXHandleType.EmailAddress, CXHandleType.Generic });
                config.HandleProvider = new BandyerSdkBCXHandleProvider();
                // CALLKIT

                BandyerSDK.Instance().InitializeWithApplicationId(BandyerSdkForms.AppId, config);
            }
        }

        bool ContinueUserActivityInt(NSUserActivity userActivity)
        {
            if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb)
            {
                this.webPageUrl = userActivity.WebPageUrl;
                if (BandyerSDK.Instance().CallClient.IsRunning)
                {
                    startWindowCallFromWebPageUrl(webPageUrl);
                }
                else
                {
                    shouldStartWindowCallFromWebPageUrl = true;
                }
                return true;
            }
            else if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0) && userActivity.GetInteraction()?.Intent != null)
            {
                return HandleINIntent(userActivity.GetInteraction()?.Intent);
            }

            return false;
        }

        void startWindowCallFromWebPageUrl(NSUrl url)
        {
            shouldStartWindowCallFromWebPageUrl = false;
            var intent = BDKJoinURLIntent.IntentWithURL(url);
            initCallWindow();
            callWindow.ShouldPresentCallViewControllerWithIntent(intent, (success) =>
            {
                if (!success)
                {
                    var alert = UIAlertController.Create("Warning", "Another call is already in progress.", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);
                }
            });
        }

        [Introduced(PlatformName.iOS, 10, 0, PlatformArchitecture.All, null)]
        private bool HandleINIntent(INIntent intent)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                if (intent.GetType() == typeof(INStartCallIntent))
                {
                    HandleINStartCallIntent((INStartCallIntent)intent);
                    return true;
                }
                else if (intent.GetType() == typeof(INStartVideoCallIntent))
                {
                    HandleINStartVideoCallIntent((INStartVideoCallIntent)intent);
                    return true;
                }
            }
            else
            {
                if (intent.GetType() == typeof(INStartVideoCallIntent))
                {
                    HandleINStartVideoCallIntent((INStartVideoCallIntent)intent);
                    return true;
                }
            }

            return false;
        }

        [Introduced(PlatformName.iOS, 10, 0, PlatformArchitecture.All, null)]
        private void HandleINStartVideoCallIntent(INStartVideoCallIntent intent)
        {
            initCallWindow();
            callWindow.HandleINStartVideoCallIntent(intent);
        }

        [Introduced(PlatformName.iOS, 13, 0, PlatformArchitecture.All, null)]
        private void HandleINStartCallIntent(INStartCallIntent intent)
        {
            initCallWindow();
            callWindow.HandleINStartCallIntent((INStartCallIntent)intent);
        }

        #region IBandyerSdk
        public event Action<bool> CallStatus;
        public event Action<bool> ChatStatus;

        public void Init(string userAlias)
        {
            this.currentUserAlias = userAlias;

            BandyerSDK.Instance().CallClient.AddObserver(this, DispatchQueue.MainQueue);
            BandyerSDK.Instance().CallClient.Start(currentUserAlias);

            BandyerSDK.Instance().ChatClient.AddObserver(this, DispatchQueue.MainQueue);
            BandyerSDK.Instance().ChatClient.Start(currentUserAlias);
        }

        public void SetUserDetails(List<BandyerSdkForms.User> usersDetails)
        {
            this.usersDetails = usersDetails;
        }

        public void StartCall(List<string> userAliases, List<BandyerSdkForms.CallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            var callee = userAliases.ToArray();
            if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioVideo))
            {
                callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioVideoCallType);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioUpgradable))
            {
                callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioUpgradableCallType);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioOnly))
            {
                callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioOnlyCallType);
            }
            else
            {
                callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioVideoCallType);
            }
            startWindowCall(callIntent);
        }

        public void StartChat(string userAlias, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            var intent = BCHOpenChatIntent.OpenChatWith(userAlias);
            startChatController(intent, callCapabilities);
        }

        public void OnPageAppearing()
        {
            if (messageNotificationController == null)
            {
                messageNotificationController = new BCHMessageNotificationController();
                messageNotificationController.Delegate = this;
                messageNotificationController.ParentViewController = UIApplication.SharedApplication.KeyWindow.RootViewController;
            }
            messageNotificationController.Show();

            if (callBannerController == null)
            {
                callBannerController = new BDKCallBannerController();
                callBannerController.Delegate = this;
                callBannerController.ParentViewController = UIApplication.SharedApplication.KeyWindow.RootViewController;
            }
            callBannerController.Show();
        }

        public void OnPageDisappearing()
        {
            if (messageNotificationController != null)
            {
                messageNotificationController.Hide();
            }
            if (callBannerController != null)
            {
                callBannerController.Hide();
            }
        }

        public void Stop()
        {
            BandyerSDK.Instance().CallClient.Stop();
            BandyerSDK.Instance().ChatClient.Stop();
            messageNotificationController = null;
            callBannerController = null;
            isSdkInitialized = false;
        }
        #endregion

        public class BandyerSdkBCXHandleProvider : NSObject, IBCXHandleProvider
        {
            public void Completion(string[] aliases, Action<CXHandle> completion)
            {
                Debug.Print("IBCXHandleProvider Completion " + aliases + " " + completion);

                CXHandle handle;
                if (aliases != null)
                {
                    handle = new CXHandle(CXHandleType.Generic, String.Join(", ", aliases));
                }
                else
                {
                    handle = new CXHandle(CXHandleType.Generic, "unknown");
                }

                completion(handle);
            }

            [return: Release]
            public NSObject Copy(NSZone zone)
            {
                return new BandyerSdkBCXHandleProvider();
            }
        }

        public class BandyerSdkBDKUserInfoFetcher : NSObject, IBDKUserInfoFetcher
        {
            public List<BDKUserInfoDisplayItem> Items { get; set; }

            public BandyerSdkBDKUserInfoFetcher(List<BDKUserInfoDisplayItem> items)
            {
                this.Items = items;
            }

            [return: Release]
            public NSObject Copy(NSZone zone)
            {
                return new BandyerSdkBDKUserInfoFetcher(items: Items);
            }

            public void FetchUsersCompletion(string[] aliases, Action<NSArray<BDKUserInfoDisplayItem>> completion)
            {
                Debug.Print("IBDKUserInfoFetcher FetchUsersCompletion " + aliases + " " + completion);

                var _items = Items.Where(i => aliases.Contains(i.Alias)).ToList();

                var arr = NSArray<BDKUserInfoDisplayItem>.FromNSObjects(_items.ToArray());
                completion(arr);
            }
        }

        void initCallWindow()
        {
            if (callWindow == null)
            {
                callWindow = new BDKCallWindow();
                callWindow.CallDelegate = this;
                var config = new BDKCallViewControllerConfiguration();
                var items = userInfoFetcherItems();
                var userInfoFetcher = new BandyerSdkBDKUserInfoFetcher(items);
                config.UserInfoFetcher = userInfoFetcher;
                var url = NSBundle.MainBundle.GetUrlForResource("video", "mp4");
                config.FakeCapturerFileURL = url;
                callWindow.SetConfiguration(config);
            }
        }

        void startWindowCall(IBDKIntent intent)
        {
            initCallWindow();
            callWindow.ShouldPresentCallViewControllerWithIntent(intent, (success) =>
            {
                Debug.Print("ShouldPresentCallViewControllerWithIntent success " + success);
            });
        }

        void startChatController(BCHOpenChatIntent intent, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities)
        {
            var rootVC = UIApplication.SharedApplication.KeyWindow.RootViewController;
            var items = userInfoFetcherItems();
            var userInfoFetcher = new BandyerSdkBDKUserInfoFetcher(items);
            BCHChannelViewControllerConfiguration configuration;
            if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioVideo))
            {
                configuration = new BCHChannelViewControllerConfiguration(audioButton: true, videoButton: true, userInfoFetcher: userInfoFetcher);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioUpgradable))
            {
                configuration = new BCHChannelViewControllerConfiguration(audioButton: true, videoButton: false, userInfoFetcher: userInfoFetcher);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.ChatWithCallCapability.AudioOnly))
            {
                configuration = new BCHChannelViewControllerConfiguration(audioButton: true, videoButton: false, userInfoFetcher: userInfoFetcher);
            }
            else
            {
                configuration = new BCHChannelViewControllerConfiguration(audioButton: false, videoButton: false, userInfoFetcher: userInfoFetcher);
            }
            var channelVC = new BCHChannelViewController();
            channelVC.Delegate = this;
            channelVC.Configuration = configuration;
            channelVC.Intent = intent;
            rootVC.PresentViewController(channelVC, true, null);
        }

        List<BDKUserInfoDisplayItem> userInfoFetcherItems()
        {
            var items = new List<BDKUserInfoDisplayItem>();
            foreach (var userDetail in usersDetails)
            {
                var item = new BDKUserInfoDisplayItem(userDetail.Alias);
                item.FirstName = userDetail.FirstName;
                item.LastName = userDetail.LastName;
                item.Email = userDetail.Email;
                if (!String.IsNullOrEmpty(userDetail.ImageUri))
                {
                    var fileExt = Path.GetExtension(userDetail.ImageUri);
                    var fileName = userDetail.ImageUri.Substring(0, userDetail.ImageUri.Length - fileExt.Length);
                    item.ImageURL = NSBundle.MainBundle.GetUrlForResource(fileName, fileExt);
                }
                items.Add(item);
            }
            return items;
        }

        void handleIncomingCall()
        {
            initCallWindow();
            var config = new BDKCallViewControllerConfiguration();
            var url = NSBundle.MainBundle.GetUrlForResource("video", "mp4");
            config.FakeCapturerFileURL = url;
            callWindow.SetConfiguration(config);
            callIntent = new BDKIncomingCallHandlingIntent();
            callWindow.ShouldPresentCallViewControllerWithIntent(callIntent, (success) =>
            {
                Debug.Print("ShouldPresentCallViewControllerWithIntent success " + success);
                if (!success)
                {
                    var alert = UIAlertController.Create("Warning", "Another call is already in progress.", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);
                }
            });
        }

        #region IBCXCallClientObserver

        [Export("callClient:didReceiveIncomingCall:")]
        public void CallClientDidReceiveIncomingCall(IBCXCallClient client, IBCXCall call)
        {
            Debug.Print("CallClientDidReceiveIncomingCall " + client + " " + call);
            handleIncomingCall();
        }

        [Export("callClientDidPause:")]
        public void CallClientDidPause(IBCXCallClient client)
        {
            Debug.Print("CallClientDidPause " + client);
            CallStatus(false);
        }

        [Export("callClientDidResume:")]
        public void CallClientDidResume(IBCXCallClient client)
        {
            Debug.Print("CallClientDidResume " + client);
            if (client.IsRunning)
            {
                CallStatus(true);
                if (shouldStartWindowCallFromWebPageUrl)
                {
                    startWindowCallFromWebPageUrl(this.webPageUrl);
                }
            }
            else
            {
                CallStatus(false);
            }
        }

        [Export("callClientDidStart:")]
        public void CallClientDidStart(IBCXCallClient client)
        {
            Debug.Print("CallClientDidStart " + client);
            if (client.IsRunning)
            {
                CallStatus(true);
                if (shouldStartWindowCallFromWebPageUrl)
                {
                    startWindowCallFromWebPageUrl(this.webPageUrl);
                }
            }
            else
            {
                CallStatus(false);
            }
        }

        [Export("callClientDidStartReconnecting:")]
        public void CallClientDidStartReconnecting(IBCXCallClient client)
        {
            Debug.Print("CallClientDidStartReconnecting " + client);
            CallStatus(false);
        }

        [Export("callClientDidStop:")]
        public void CallClientDidStop(IBCXCallClient client)
        {
            Debug.Print("CallClientDidStop " + client);
            CallStatus(false);
        }

        [Export("callClientWillPause:")]
        public void CallClientWillPause(IBCXCallClient client)
        {
            Debug.Print("CallClientWillPause " + client);
            CallStatus(false);
        }

        [Export("callClientWillResume:")]
        public void CallClientWillResume(IBCXCallClient client)
        {
            Debug.Print("CallClientWillResume " + client);
            CallStatus(false);
        }

        [Export("callClientWillStart:")]
        public void CallClientWillStart(IBCXCallClient client)
        {
            Debug.Print("CallClientWillStart " + client);
            CallStatus(false);
        }

        [Export("callClientWillStop:")]
        public void CallClientWillStop(IBCXCallClient client)
        {
            Debug.Print("CallClientWillStop " + client);
            CallStatus(false);
        }

        [Export("callClient:didFailWithError:")]
        public void CallClientDidFailWithError(IBCXCallClient client, NSError error)
        {
            Debug.Print("CallClientDidFailWithError " + client + " " + error);
            CallStatus(false);
        }

        #endregion

        #region IBCHChatClientObserver
        [Export("chatClientWillStart:")]
        public void ChatClientWillStart(IBCHChatClient client)
        {
            Debug.Print("ChatClientWillStart " + client);
            ChatStatus(false);
        }

        [Export("chatClientDidStart:")]
        public void ChatClientDidStart(IBCHChatClient client)
        {
            Debug.Print("ChatClientDidStart " + client);
            if (client.State == BCHChatClientState.Running)
            {
                ChatStatus(true);
            }
            else
            {
                ChatStatus(false);
            }
        }

        [Export("chatClientWillPause:")]
        public void ChatClientWillPause(IBCHChatClient client)
        {
            Debug.Print("ChatClientWillPause " + client);
            ChatStatus(false);
        }

        [Export("chatClientDidPause:")]
        public void ChatClientDidPause(IBCHChatClient client)
        {
            Debug.Print("ChatClientDidPause " + client);
            ChatStatus(false);
        }

        [Export("chatClientWillStop:")]
        public void ChatClientWillStop(IBCHChatClient client)
        {
            Debug.Print("ChatClientWillStop " + client);
            ChatStatus(false);
        }

        [Export("chatClientDidStop:")]
        public void ChatClientDidStop(IBCHChatClient client)
        {
            Debug.Print("ChatClientDidStop " + client);
            ChatStatus(false);
        }

        [Export("chatClientWillResume:")]
        public void ChatClientWillResume(IBCHChatClient client)
        {
            Debug.Print("ChatClientWillResume " + client);
            ChatStatus(false);
        }

        [Export("chatClientDidResume:")]
        public void ChatClientDidResume(IBCHChatClient client)
        {
            Debug.Print("ChatClientDidResume " + client);
            if (client.State == BCHChatClientState.Running)
            {
                ChatStatus(true);
            }
            else
            {
                ChatStatus(false);
            }
        }

        [Export("chatClient:didFailWithError:")]
        public void ChatClientDidFailWithError(IBCHChatClient client, NSError error)
        {
            Debug.Print("ChatClientDidFailWithError " + client + " " + error);
            ChatStatus(false);
        }
        #endregion

        #region IBCHChannelViewControllerDelegate
        public void ChannelViewControllerDidFinish(BCHChannelViewController controller)
        {
            Debug.Print("ChannelViewControllerDidFinish " + controller);
            UIApplication.SharedApplication.KeyWindow.RootViewController.DismissViewController(true, null);
        }
        public void ChannelViewControllerDidTouchBanner(BCHChannelViewController controller, BDKCallBannerView banner)
        {

            Debug.Print("ChannelViewControllerDidTouchBanner " + controller + " " + banner);
            if (callWindow != null)
            {
                callWindow.ShouldPresentCallViewControllerWithIntent(callIntent, (obj) => { });
            }
        }
        public void ChannelViewControllerDidTapAudioCallWith(BCHChannelViewController controller, string[] users)
        {
            Debug.Print("ChannelViewControllerDidTapAudioCallWith " + controller + " " + users);
            BDKMakeCallIntent intent;
            if (users != null)
            {
                intent = BDKMakeCallIntent.IntentWithCallee(users, BDKCallType.AudioOnlyCallType);
            }
            else
            {
                intent = BDKMakeCallIntent.IntentWithCallee(new string[] { "unknown" }, BDKCallType.AudioOnlyCallType);
            }
            startWindowCall(intent);
        }
        public void ChannelViewControllerDidTapVideoCallWith(BCHChannelViewController controller, string[] users)
        {
            Debug.Print("ChannelViewControllerDidTapVideoCallWith " + controller + " " + users);
            BDKMakeCallIntent intent;
            if (users != null)
            {
                intent = BDKMakeCallIntent.IntentWithCallee(users, BDKCallType.AudioVideoCallType);
            }
            else
            {
                intent = BDKMakeCallIntent.IntentWithCallee(new string[] { "unknown" }, BDKCallType.AudioVideoCallType);
            }
            startWindowCall(intent);
        }
        #endregion

        #region IBDKCallWindowDelegate
        public void CallWindowDidFinish(BDKCallWindow window)
        {
            Debug.Print("CallWindowDidFinish " + window);
            window.DismissCallViewControllerWithCompletion(() => { });
            window.Hidden = true;
        }
        [Export("callWindow:openChatWith:")]
        public void CallWindowOpenChatWith(BDKCallWindow window, BCHOpenChatIntent intent)
        {
            Debug.Print("CallWindowOpenChatWith " + window + " " + intent);
            window.DismissCallViewControllerWithCompletion(() => { });
            window.Hidden = true;
            startChatController(intent, new List<BandyerSdkForms.ChatWithCallCapability>() { BandyerSdkForms.ChatWithCallCapability.AudioVideo });
        }
        #endregion

        #region IPKPushRegistryDelegate
        public void DidUpdatePushCredentials(PKPushRegistry registry, PKPushCredentials credentials, string type)
        {
            Debug.Print("DidUpdatePushCredentials " + registry + " " + credentials + " " + type);
            var tokenStr = credentials.Bcx_tokenAsString();
            BandyerSdkForms.SetPushToken(tokenStr);
            BandyerSdkForms.RegisterTokenToBandyerIos();
        }
        public void DidReceiveIncomingPush(PKPushRegistry registry, PKPushPayload payload, string type)
        {
            Debug.Print("DidReceiveIncomingPush " + registry + " " + payload + " " + type);
        }
        #endregion

        #region IBCHMessageNotificationControllerDelegate
        public void DidTouch(BCHMessageNotificationController controller, BCHChatNotification notification)
        {
            Debug.Print("IBCHMessageNotificationControllerDelegate DidTouch " + controller + " " + notification);
            var intent = BCHOpenChatIntent.OpenChatFrom(notification);
            startChatController(intent, new List<BandyerSdkForms.ChatWithCallCapability>() { BandyerSdkForms.ChatWithCallCapability.AudioVideo });
        }
        #endregion

        #region IBDKCallBannerControllerDelegate
        public void DidTouch(BDKCallBannerController controller, BDKCallBannerView banner)
        {
            Debug.Print("IBDKCallBannerControllerDelegate DidTouch " + controller + " " + banner);
            if (callWindow != null)
            {
                callWindow.ShouldPresentCallViewControllerWithIntent(callIntent, (obj) => { });
            }
        }
        [Export("callBannerController:willHide:")]
        public void WillHide(BDKCallBannerController controller, BDKCallBannerView banner)
        {
            Debug.Print("IBDKCallBannerControllerDelegate WillHide " + controller + " " + banner);
            UIApplication.SharedApplication.StatusBarHidden = false;
        }
        [Export("callBannerController:willShow:")]
        public void WillShow(BDKCallBannerController controller, BDKCallBannerView banner)
        {
            Debug.Print("IBDKCallBannerControllerDelegate WillShow " + controller + " " + banner);
            UIApplication.SharedApplication.StatusBarHidden = true;
        }
        #endregion
    }
}
