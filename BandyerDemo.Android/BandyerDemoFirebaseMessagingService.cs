using System;
using System.Net;
using System.Text;
using Android.App;
using Android.Util;
using Com.Bandyer.Android_sdk.Client;
using Firebase.Messaging;
using Xamarin.Essentials;

namespace BandyerDemo.Droid
{
    [Service]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class BandyerDemoFirebaseMessagingService : FirebaseMessagingService
    {
        const string TAG = "BandyerDemoFirebaseMessagingService";

        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            Log.Debug(TAG, "OnNewToken " + token);

            BandyerSdkForms.SetPushToken(token);
            BandyerSdkForms.RegisterTokenToBandyerAndroid();
        }

        public override void OnMessageReceived(RemoteMessage remoteMessage)
        {
            base.OnMessageReceived(remoteMessage);
            Log.Debug(TAG, "OnMessageReceived " + remoteMessage);

            BandyerSDKClient.Instance.HandleNotification(ApplicationContext, remoteMessage.Data["message"]);
        }

    }
}
