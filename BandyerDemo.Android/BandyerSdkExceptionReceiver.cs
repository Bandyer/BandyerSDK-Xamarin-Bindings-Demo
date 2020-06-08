using System;
using Android.App;
using Android.Content;
using Android.Util;
using Com.Bandyer.Android_sdk.Exceptions;
using Java.Lang;

namespace BandyerDemo.Droid
{
    [BroadcastReceiver(Exported = false)]
    [IntentFilter(new[] { "com.bandyer.android_sdk.BANDYER_UNHANDLED_EXCEPTION" })]
    public class BandyerSdkExceptionReceiver : BandyerUnhandledExceptionBroadcastReceiver
    {
        const string TAG = "BandyerSdkExceptionReceiver";

        public override void OnException(Throwable e)
        {
            Log.Debug(TAG, "OnException " + e);
        }
    }
}
