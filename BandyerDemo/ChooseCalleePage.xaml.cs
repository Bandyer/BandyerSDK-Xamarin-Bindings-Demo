using System;
using System.Collections.Generic;
using System.Linq;
using Rg.Plugins.Popup.Services;
using Xamarin.Forms;

namespace BandyerDemo
{
    public partial class ChooseCalleePage : ContentPage
    {
        private ChatConfigPopupPage chatPopup;
        private CallConfigPopupPage callPopup;

        public ChooseCalleePage()
        {
            InitializeComponent();
            BandyerSdkForms.Instance.BandyerSdk.ChatStatus += ChatStatus;
            BandyerSdkForms.Instance.BandyerSdk.CallStatus += CallStatus;

            ToolbarItems.Add(new ToolbarItem()
            {
                Text = "Logout",
                Command = new Command(Logout),
            });

            var loggedUserAlias = BandyerSdkForms.GetLoggedUserAlias();
            mainLabel.Text = "Logged as: " + loggedUserAlias + ". Who do you want to call or chat with?";
            userList.ItemsSource = BandyerSdkForms.Instance.Callee;

            chatPopup = new ChatConfigPopupPage();
            callPopup = new CallConfigPopupPage();
        }

        async void Logout()
        {
            var result = await this.DisplayAlert(null, "Logout current user?", "Yes", "No");
            if (result)
            {
                BandyerSdkForms.UnregisterTokenToBandyer();
                BandyerSdkForms.SetLoggedUserAlias("");
                BandyerSdkForms.Instance.BandyerSdk.Stop();
                App.Instance.ResetMainPage();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            registerTokenToBandyer();
            var loggedUserAlias = BandyerSdkForms.GetLoggedUserAlias();
            var allUsersDetails = BandyerSdkForms.Instance.Callers.Concat(BandyerSdkForms.Instance.Callee).ToList();
            BandyerSdkForms.Instance.BandyerSdk.SetUserDetails(allUsersDetails);
            BandyerSdkForms.Instance.BandyerSdk.Init(loggedUserAlias);
            BandyerSdkForms.Instance.BandyerSdk.OnPageAppearing();
        }

        private void registerTokenToBandyer()
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                BandyerSdkForms.RegisterTokenToBandyerIos();
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                BandyerSdkForms.RegisterTokenToBandyerAndroid();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            BandyerSdkForms.Instance.BandyerSdk.OnPageDisappearing();
        }

        void ChatStatus(bool isReady)
        {
            if (isReady)
            {
                ButtonStartChat.IsEnabled = true;
                ButtonStartChat.BackgroundColor = Color.FromHex("#00ff00");
            }
            else
            {
                ButtonStartChat.IsEnabled = false;
                ButtonStartChat.BackgroundColor = Color.FromHex("#ff0000");
            }
        }

        void CallStatus(bool isReady)
        {
            if (isReady)
            {
                ButtonStartCall.IsEnabled = true;
                ButtonStartCall.BackgroundColor = Color.FromHex("#00ff00");
            }
            else
            {
                ButtonStartCall.IsEnabled = false;
                ButtonStartCall.BackgroundColor = Color.FromHex("#ff0000");
            }
        }

        async void Button_StartCall(System.Object sender, System.EventArgs e)
        {
            var users = BandyerSdkForms.Instance.GetSelectedUsersNames();
            if (users.Count == 0)
            {
                await DisplayAlert(null, "Select at least 1 user", "OK");
                return;
            }
            await PopupNavigation.Instance.PushAsync(callPopup);
        }

        async void Button_StartChat(System.Object sender, System.EventArgs e)
        {
            var users = BandyerSdkForms.Instance.GetSelectedUsersNames();
            if (users.Count == 0)
            {
                await DisplayAlert(null, "Select at least 1 user", "OK");
                return;
            }
            if (users.Count > 1)
            {
                await DisplayAlert(null, "Group chats are not yet supported", "OK");
                return;
            }
            await PopupNavigation.Instance.PushAsync(chatPopup);
        }

        void ListView_ItemTapped(System.Object sender, Xamarin.Forms.ItemTappedEventArgs e)
        {
            var obj = e.Item as BandyerSdkForms.User;
            if (obj == null)
                return;
            var index = BandyerSdkForms.Instance.Callee.IndexOf(obj);
            BandyerSdkForms.Instance.Callee[index].Selected = !BandyerSdkForms.Instance.Callee[index].Selected;
            userList.ItemsSource = null;
            userList.ItemsSource = BandyerSdkForms.Instance.Callee;
        }
    }
}
