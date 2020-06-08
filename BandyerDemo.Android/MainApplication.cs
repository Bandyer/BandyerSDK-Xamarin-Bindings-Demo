using System;
using Android.App;
using Android.Gms.Tasks;
using Android.Runtime;
using Com.Bandyer.Android_sdk;
using Firebase.Iid;

namespace BandyerDemo.Droid
{
    [Application]
    public class MainApplication : Application
        , IOnSuccessListener
    {
        public MainApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
        public override void OnCreate()
        {
            base.OnCreate();
            FirebaseInstanceId.Instance.GetInstanceId().AddOnSuccessListener(this);
            BandyerSdkAndroid.InitSdk(this);
        }

        public void OnSuccess(Java.Lang.Object result)
        {
            var token = result.Class.GetMethod("getToken").Invoke(result).ToString();
            BandyerSdkForms.SetPushToken(token);
            BandyerSdkForms.RegisterTokenToBandyerAndroid();
        }
    }
}
