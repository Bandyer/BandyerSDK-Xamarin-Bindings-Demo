using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        , IBDKCallBannerControllerDelegate
        , IBDKInAppChatNotificationTouchListener
        , IBDKInAppFileShareNotificationTouchListener
    {
        private static BandyerSdkiOS instance = null;
        public BandyerSdkiOS()
        {
            instance = this;
        }

        private BDKCallWindow _callWindow = null;

        private BDKCallWindow CallWindow
        {
            get
            {
                if (_callWindow == null)
                    SetupCallWindow();
                
                return _callWindow;
            }

            set => _callWindow = value;
        }
        
        private void SetupCallWindow()
        {
            var config = new BDKCallViewControllerConfiguration();
            var items = UserInfoFetcherItems();
            var userInfoFetcher = new UserInfoFetcher(items);
            config.UserInfoFetcher = userInfoFetcher;
            config.FakeCapturerFileURL = NSBundle.MainBundle.GetUrlForResource("video", "mp4");
            _callWindow = new BDKCallWindow();
            _callWindow.CallDelegate = this;
            _callWindow.SetConfiguration(config);
        }

        private IBDKIntent _callIntent = null;
        private string _currentUserAlias;
        private BDKCallBannerController _callBannerController = null;
        private NSUrl _webPageUrl;
        private bool _shouldStartWindowCallFromWebPageUrl = false;
        private bool _isSdkInitialized = false;
        private List<BandyerSdkForms.User> _usersDetails;
        private BCXCallRegistryObserver _registryObserver = new RegistryObserver();

        public static void InitSdk()
        {
            instance.InitSdkInt();
        }

        public static bool ContinueUserActivity(NSUserActivity userActivity)
        {
            return instance.ContinueUserActivityInt(userActivity);
        }

        private void InitSdkInt()
        {
            if (_isSdkInitialized)
                return;
            
            _isSdkInitialized = true;
            var config = new BDKConfig();
            config.NotificationPayloadKeyPath = "data";
            config.PushRegistryDelegate = this;

            config.Environment = BDKEnvironment.Sandbox;
            BDKConfig.LogLevel = BDFDDLogLevel.Verbose;

            // CALLKIT, enabled on real device and disabled on simulator because it's not supported yet by it.
            config.CallKitEnabled = Runtime.Arch != Arch.SIMULATOR;
            config.NativeUILocalizedName = "BanyerDemo App";
            //config.NativeUIRingToneSound = "MyRingtoneSound";
            UIImage callKitIconImage = UIImage.FromBundle("bandyer_logo");
            config.NativeUITemplateIconImageData = callKitIconImage.AsPNG();
            config.SupportedHandleTypes = new NSSet<NSNumber>(new NSNumber[] { new NSNumber((long)CXHandleType.EmailAddress), new NSNumber((long)CXHandleType.Generic)});
            config.HandleProvider = new HandleProvider();

            BandyerSDK.Instance().InitializeWithApplicationId(BandyerSdkForms.AppId, config);
        }

        private bool ContinueUserActivityInt(NSUserActivity userActivity)
        {
            if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb)
            {
                this._webPageUrl = userActivity.WebPageUrl;
                if (BandyerSDK.Instance().CallClient.IsRunning)
                {
                    StartWindowCallFromWebPageUrl(_webPageUrl);
                }
                else
                {
                    _shouldStartWindowCallFromWebPageUrl = true;
                }
                return true;
            }
            else if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0) && userActivity.GetInteraction()?.Intent != null)
            {
                return HandleINIntent(userActivity.GetInteraction()?.Intent);
            }

            return false;
        }

        private void StartWindowCallFromWebPageUrl(NSUrl url)
        {
            _shouldStartWindowCallFromWebPageUrl = false;
            
            var intent = BDKJoinURLIntent.IntentWithURL(url);
            CallWindow.PresentCallViewControllerWithCompletion(intent, (error) =>
            {
                if (error == null) return;
                
                var alert = UIAlertController.Create("Warning", "Another call is already in progress.", UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);
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
            CallWindow.HandleINStartVideoCallIntent(intent);
        }

        [Introduced(PlatformName.iOS, 13, 0, PlatformArchitecture.All, null)]
        private void HandleINStartCallIntent(INStartCallIntent intent)
        {
            CallWindow.HandleINStartCallIntent((INStartCallIntent)intent);
        }

        #region IBandyerSdk
        
        public event Action<bool> CallStatus;
        public event Action<bool> ChatStatus;

        public void Init(string userAlias)
        {
            this._currentUserAlias = userAlias;

            BandyerSDK.Instance().CallClient.AddObserver(this, DispatchQueue.MainQueue);
            BandyerSDK.Instance().CallClient.Start(_currentUserAlias);

            BandyerSDK.Instance().ChatClient.AddObserver(this, DispatchQueue.MainQueue);
            BandyerSDK.Instance().ChatClient.Start(_currentUserAlias);

            BandyerSDK.Instance().NotificationsCoordinator.ChatListener = this;
            BandyerSDK.Instance().NotificationsCoordinator.FileShareListener = this;
        }

        public void SetUserDetails(List<BandyerSdkForms.User> usersDetails)
        {
            this._usersDetails = usersDetails;
        }

        public void StartCall(List<string> userAliases, List<BandyerSdkForms.CallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            var callee = userAliases.ToArray();
            if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioVideo))
            {
                _callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioVideoCallType);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioUpgradable))
            {
                _callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioUpgradableCallType);
            }
            else if (callCapabilities.Contains(BandyerSdkForms.CallCapability.AudioOnly))
            {
                _callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioOnlyCallType);
            }
            else
            {
                _callIntent = BDKMakeCallIntent.IntentWithCallee(callee, BDKCallType.AudioVideoCallType);
            }
            StartWindowCall(_callIntent);
        }

        public void StartChat(string userAlias, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions)
        {
            var intent = BCHOpenChatIntent.OpenChatWith(userAlias);
            StartChatController(intent, callCapabilities);
        }

        public void OnPageAppearing()
        {
            BandyerSDK.Instance().NotificationsCoordinator.Start();

            if (_callBannerController == null)
            {
                _callBannerController = new BDKCallBannerController();
                _callBannerController.Delegate = this;
                _callBannerController.ParentViewController = UIApplication.SharedApplication.KeyWindow.RootViewController;
            }
            _callBannerController.Show();
        }

        public void OnPageDisappearing()
        {
            BandyerSDK.Instance().NotificationsCoordinator.Stop();
            
            if (_callBannerController != null)
            {
                _callBannerController.Hide();
            }
        }

        public void Stop()
        {
            BandyerSDK.Instance().CallClient.Stop();
            BandyerSDK.Instance().ChatClient.Stop();
            _callBannerController = null;
            _isSdkInitialized = false;
        }
        
        #endregion

        private class HandleProvider : NSObject, IBCXHandleProvider
        {
            public void HandleForAliases (string[] aliases, Action<CXHandle> completion)
            {
                Debug.Print("HandleForAliases " + aliases);

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
                return new HandleProvider();
            }
        }

        private class UserInfoFetcher : NSObject, IBDKUserInfoFetcher
        {
            public List<BDKUserInfoDisplayItem> Items { get; set; }

            public UserInfoFetcher(List<BDKUserInfoDisplayItem> items)
            {
                this.Items = items;
            }

            [return: Release]
            public NSObject Copy(NSZone zone)
            {
                return new UserInfoFetcher(items: Items);
            }

            public void FetchUsersCompletion(string[] aliases, Action<NSArray<BDKUserInfoDisplayItem>> completion)
            {
                Debug.Print("IBDKUserInfoFetcher FetchUsersCompletion " + aliases + " " + completion);

                var items = Items.Where(i => aliases.Contains(i.Alias)).ToList();

                var arr = NSArray<BDKUserInfoDisplayItem>.FromNSObjects(items.ToArray());
                completion(arr);
            }
        }

        private void StartWindowCall(IBDKIntent intent)
        {
            CallWindow.PresentCallViewControllerWithCompletion(intent, (error) =>
            {
                Debug.Print("PresentCallViewControllerWithCompletion error " + error);
            });
        }

        private void StartChatController(BCHOpenChatIntent intent, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities)
        {
            var rootVC = UIApplication.SharedApplication.KeyWindow.RootViewController;
            var items = UserInfoFetcherItems();
            var userInfoFetcher = new UserInfoFetcher(items);
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

        private List<BDKUserInfoDisplayItem> UserInfoFetcherItems()
        {
            var items = new List<BDKUserInfoDisplayItem>();
            foreach (var userDetail in _usersDetails)
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

        private void HandleIncomingCall()
        {
            _callIntent = new BDKIncomingCallHandlingIntent();
            CallWindow.PresentCallViewControllerWithCompletion(_callIntent, (error) =>
            {
                Debug.Print("PresentCallViewControllerWithCompletion error " + error);
                
                if (error == null) return;
                
                var alert = UIAlertController.Create("Warning", "Another call is already in progress.", UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);
            });
        }

        #region IBCXCallClientObserver

        [Export("callClient:didReceiveIncomingCall:")]
        public void CallClientDidReceiveIncomingCall(IBCXCallClient client, IBCXCall call)
        {
            Debug.Print("CallClientDidReceiveIncomingCall " + client + " " + call);
            HandleIncomingCall();
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
                if (_shouldStartWindowCallFromWebPageUrl)
                {
                    StartWindowCallFromWebPageUrl(this._webPageUrl);
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
                if (_shouldStartWindowCallFromWebPageUrl)
                {
                    StartWindowCallFromWebPageUrl(this._webPageUrl);
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
            BandyerSDK.Instance().CallRegistry.RemoveObserver(_registryObserver);
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
            BandyerSDK.Instance().CallRegistry.AddObserver(_registryObserver, DispatchQueue.MainQueue);
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

        #region BCHChatClientObserver
        
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
            ChatStatus(client.State == BCHChatClientState.Running);
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
            ChatStatus(client.State == BCHChatClientState.Running);
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

            CallWindow.PresentCallViewControllerWithCompletion(_callIntent, (error) => { });
        }
        public void ChannelViewControllerDidTapAudioCallWith(BCHChannelViewController controller, string[] users)
        {
            Debug.Print("ChannelViewControllerDidTapAudioCallWith " + controller + " " + users);
            var intent = BDKMakeCallIntent.IntentWithCallee(users ?? new string[] { "unknown" }, BDKCallType.AudioOnlyCallType);
            StartWindowCall(intent);
        }
        public void ChannelViewControllerDidTapVideoCallWith(BCHChannelViewController controller, string[] users)
        {
            Debug.Print("ChannelViewControllerDidTapVideoCallWith " + controller + " " + users);
            var intent = BDKMakeCallIntent.IntentWithCallee(users ?? new string[] { "unknown" }, BDKCallType.AudioVideoCallType);
            StartWindowCall(intent);
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
            StartChatController(intent, new List<BandyerSdkForms.ChatWithCallCapability>() { BandyerSdkForms.ChatWithCallCapability.AudioVideo });
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

        #region IBDKCallBannerControllerDelegate
        
        public void DidTouch(BDKCallBannerController controller, BDKCallBannerView banner)
        {
            Debug.Print("IBDKCallBannerControllerDelegate DidTouch " + controller + " " + banner);
            
            CallWindow.PresentCallViewControllerWithCompletion(_callIntent, (error) => { });
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
        
        #region Registry Observer

        private class RegistryObserver : BCXCallRegistryObserver
        {
            private Dictionary<NSUuid, BCXCallObserver> _callObservers = new Dictionary<NSUuid, BCXCallObserver>();
            public override void DidAddCall(IBCXCallRegistry registry, IBCXCall call)
            {
                Debug.Print("Added new call with UUID " + call.Uuid + " caller " + call.Participants.Caller.UserId + " callee " + call.Participants.Callees.Aggregate(string.Empty, (s, p) => s + p.UserId) + " to registry");
                
                var observer = new CallObserver();
                _callObservers.Add(call.Uuid, observer);
                call.AddObserver(observer, DispatchQueue.MainQueue);
            }

            public override void DidRemoveCall(IBCXCallRegistry registry, IBCXCall call)
            {
                Debug.Print("Removed call with UUID" + call.Uuid + " from registry");

                if (!_callObservers.ContainsKey(call.Uuid)) return;
                
                var observer = _callObservers[call.Uuid];
                if (observer != null)
                    call.RemoveObserver(observer);
            }
        }

        #endregion

        #region Call Observer

        private class CallObserver : BCXCallObserver
        {
            public override void CallDidChangeState(IBCXCall call, BCXCallState state)
            {
                Debug.Print("Call with UUID " + call.Uuid + " changed state " + state);
            }

            public override void CallDidConnect(IBCXCall call)
            {
                Debug.Print("Call " + call + " connected");
            }

            public override void CallDidEnd(IBCXCall call)
            {
                Debug.Print("Call " + call + " ended");
            }

            public override void CallDidFailWithError(IBCXCall call, NSError error)
            {
                Debug.Print("Call " + call + " failed with error " + error);
            }

            public override void CallDidUpdateOptions(IBCXCall call, BCXCallOptions options)
            {
                Debug.Print("Call " + call + " updated its options " + options);
            }

            public override void CallDidUpdateParticipants(IBCXCall call, IBCXCallParticipants participants)
            {
                Debug.Print("Call " + call + " updated its participants " + participants);
            }

            public override void CallDidUpgradeToVideoCall(IBCXCall call)
            {
                Debug.Print("Call " + call + " upgraded to video call");
            }
        }

        #endregion
        
        #region Notifications Handlers
        
        public void DidTouchChatNotification(BDKChatNotification notification)
        {
            Debug.Print("DidTouchChatNotification");
            var intent = BCHOpenChatIntent.OpenChatFrom(notification);
            StartChatController(intent, new List<BandyerSdkForms.ChatWithCallCapability>() { BandyerSdkForms.ChatWithCallCapability.AudioVideo });
        }

        public void DidTouchFileShareNotification(BDKFileShareNotification notification)
        {
            Debug.Print("DidTouchFileShareNotification");
            CallWindow.PresentCallViewControllerWithCompletion(new BDKOpenDownloadsIntent(), (error) => { });
        }
        
        #endregion
    }
}
