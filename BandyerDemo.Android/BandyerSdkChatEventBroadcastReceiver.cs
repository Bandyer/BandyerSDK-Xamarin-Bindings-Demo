using System;
using Android.App;
using Android.Content;
using Android.Util;
using Com.Bandyer.Android_sdk.Chat;
using Com.Bandyer.Android_sdk.Chat.Notification;

namespace BandyerDemo.Droid
{
    [BroadcastReceiver(Exported = false)]
    [IntentFilter(new[] { "com.bandyer.android_sdk.CHAT_EVENT_ACTION" })]
    public class BandyerSdkChatEventBroadcastReceiver : ChatEventBroadcastReceiver
    {
        const string TAG = "BandyerSdkChatEventBroadcastReceiver";

        public override void OnChatEnded()
        {
            Log.Debug(TAG, "OnChatStarted");
        }

        public override void OnChatEndedWithError(ChatException chatException)
        {
            Log.Debug(TAG, "OnChatEndedWithError " + chatException);
        }

        public override void OnChatStarted()
        {
            Log.Debug(TAG, "OnChatStarted");
        }
    }
}
