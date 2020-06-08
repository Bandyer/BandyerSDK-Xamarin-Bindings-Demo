using System;
using Android.App;
using Android.Content;
using Android.Util;
using Com.Bandyer.Android_sdk.Call;
using Com.Bandyer.Android_sdk.Call.Notification;
using Com.Bandyer.Android_sdk.Intent.Call;

namespace BandyerDemo.Droid
{
    [BroadcastReceiver(Exported = false)]
    [IntentFilter(new[] { "com.bandyer.android_sdk.CALL_EVENT_ACTION" })]
    public class BandyerSdkCallEventBroadcastReceiver : CallEventBroadcastReceiver
    {
        const string TAG = "BandyerSdkCallEventBroadcastReceiver";

        public override void OnCallCreated(ICall call)
        {
            Log.Debug(TAG, "OnCallCreated " + call);
        }

        public override void OnCallEnded(ICall call)
        {
            Log.Debug(TAG, "OnCallEnded " + call);
        }

        public override void OnCallEndedWithError(ICall call, CallException callException)
        {
            Log.Debug(TAG, "OnCallEndedWithError " + call);
        }

        public override void OnCallStarted(ICall call)
        {
            Log.Debug(TAG, "OnCallStarted " + call);
        }
    }
}
