using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace BandyerDemo
{
    public class BandyerSdkForms
    {
        public const string AppId = "mAppId_xxx";

        public enum CallCapability
        {
            AudioOnly,
            AudioUpgradable,
            AudioVideo,
        }

        public enum ChatWithCallCapability
        {
            AudioOnly,
            AudioUpgradable,
            AudioVideo,
        }

        public enum InCallCapability
        {
            Whiteboard,
            FileSharing,
            Chat,
            ScreenSharing,
        }

        public enum InCallOptions
        {
            CallRecording,
            BackCameraAsDefault,
            DisableProximitySensor,
        }

        public class User
        {
            public String Alias { get; set; }
            public String NickName { get; set; }
            public String FirstName { get; set; }
            public String LastName { get; set; }
            public String Email { get; set; }
            public String ImageUri { get; set; }
            public bool Selected { get; set; } = false;
        }

        public interface IBandyerSdk
        {
            event Action<bool> CallStatus;
            event Action<bool> ChatStatus;
            void Init(string userAlias);
            void SetUserDetails(List<User> usersDetails);
            void StartCall(List<string> userAliases, List<BandyerSdkForms.CallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions);
            void StartChat(string userAlias, List<BandyerSdkForms.ChatWithCallCapability> callCapabilities, List<BandyerSdkForms.InCallCapability> inCallCapabilities, List<BandyerSdkForms.InCallOptions> inCallOptions);
            void OnPageAppearing();
            void OnPageDisappearing();
            void Stop();
        }

        private static BandyerSdkForms _instance = null;
        public static BandyerSdkForms Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BandyerSdkForms();
                }
                return _instance;
            }
        }

        private BandyerSdkForms()
        {
            BandyerSdk = DependencyService.Get<IBandyerSdk>();
        }

        public IBandyerSdk BandyerSdk { get; private set; }

        public List<User> Callers = new List<User>() {
            new User()
            {
                Alias = "client",
                NickName = "ClientUser",
                FirstName = "John",
                LastName = "Liu",
                Email = "client@client.com",
                ImageUri = "man_0.jpg",
            },
            new User()
            {
                Alias = "client2",
                NickName = "Client2User",
                FirstName = "Mark",
                LastName = "Mendoza",
                Email = "client2@client.com",
                ImageUri = "man_1.jpg",
            },
            new User()
            {
                Alias = "web",
                NickName = "WebUser",
                FirstName = "Jack",
                LastName = "Beck",
                Email = "web@web.com",
                ImageUri = "man_2.jpg",
            },
        };
        public List<User> Callee = new List<User>() {
            new User()
            {
                Alias = "client3",
                NickName = "Client3User",
                FirstName = "Paul",
                LastName = "Milner",
                Email = "client3@client.com",
                ImageUri = "man_3.jpg",
            },
            new User()
            {
                Alias = "web2",
                NickName = "Web2User",
                FirstName = "Herbert",
                LastName = "Sanchez",
                Email = "web2@web.com",
                ImageUri = "man_4.jpg",
            },
            new User()
            {
                Alias = "web3",
                NickName = "Web3User",
                FirstName = "Phil",
                LastName = "Wiley",
                Email = "web3@web.com",
                ImageUri = "man_5.jpg",
            },
        };

        public List<String> GetSelectedUsersNames()
        {
            return Callee.Where(u => u.Selected).Select(u => u.Alias).ToList();
        }

        public static void SetPushToken(string pushToken)
        {
            Preferences.Set("bandyer_push_token", pushToken);
        }

        public static void SetLoggedUserAlias(string userAlias)
        {
            Preferences.Set("bandyer_user_alias", userAlias);
        }

        public static string GetLoggedUserAlias()
        {
            return Preferences.Get("bandyer_user_alias", "");
        }

        public static void RegisterTokenToBandyerAndroid()
        {
            RegisterTokenToBandyer("android");
        }

        public static void RegisterTokenToBandyerIos()
        {
            RegisterTokenToBandyer("ios");
        }

        public static void RegisterTokenToBandyer(string callPlatform)
        {
            var user_alias = Preferences.Get("bandyer_user_alias", "");
            var push_token = Preferences.Get("bandyer_push_token", "");
            if (String.IsNullOrEmpty(user_alias) || String.IsNullOrEmpty(push_token))
            {
                return;
            }

            var push_provider = "";
            var platform = "";
            if (callPlatform == "ios")
            {
                push_provider = "";
                platform = "ios";
            }
            else if (callPlatform == "android")
            {
                push_provider = "";
                platform = "android";
            }

            var urlStr = "https://sandbox.bandyer.com/mobile_push_notifications/rest/device";
            var jsonStr = "{" +
                "\"user_alias\":\"" + user_alias + "\"" +
                ",\"app_id\":\"" + BandyerSdkForms.AppId + "\"" +
                ",\"push_token\":\"" + push_token + "\"" +
                ",\"push_provider\":\"" + push_provider + "\"" +
                ",\"platform\":\"" + platform + "\"" +
                "}";

            try
            {
                WebClient wc = new WebClient();
                wc.Headers.Add(HttpRequestHeader.ContentType, "application/json; charset=utf-8");
                byte[] dataBytes = Encoding.UTF8.GetBytes(jsonStr);
                byte[] responseBytes = wc.UploadData(new Uri(urlStr), "POST", dataBytes);
                string responseString = Encoding.UTF8.GetString(responseBytes);

                Debug.WriteLine("RegisterTokenToBandyer " + responseString);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static void UnregisterTokenToBandyer()
        {
            var user_alias = Preferences.Get("bandyer_user_alias", "");
            if (String.IsNullOrEmpty(user_alias))
            {
                return;
            }

            var push_token = Preferences.Get("bandyer_push_token", "");

            var urlStr = "/mobile_push_notifications/rest/device/" + user_alias + "/" + BandyerSdkForms.AppId + "/" + push_token;

            try
            {
                WebClient wc = new WebClient();
                byte[] responseBytes = wc.UploadData(new Uri(urlStr), "DELETE", null);
                string responseString = Encoding.UTF8.GetString(responseBytes);

                Debug.WriteLine("UnregisterTokenToBandyer " + responseString);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }
    }
}
