using System;
using System.Collections.Generic;
using Xamarin.Forms;

namespace BandyerDemo
{
    public partial class ChooseCallerPage : ContentPage
    {
        public ChooseCallerPage()
        {
            InitializeComponent();
            userList.ItemsSource = BandyerSdkForms.Instance.Callers;
        }

        void ListView_ItemTapped(System.Object sender, Xamarin.Forms.ItemTappedEventArgs e)
        {
            var obj = e.Item as BandyerSdkForms.User;
            if (obj == null)
                return;
            BandyerSdkForms.SetLoggedUserAlias(obj.Alias);
            App.Instance.ResetMainPage();
        }
    }
}
