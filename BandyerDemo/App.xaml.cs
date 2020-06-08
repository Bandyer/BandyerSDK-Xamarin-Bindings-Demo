using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace BandyerDemo
{
    public partial class App : Application
    {
        public static App Instance;

        public App()
        {
            Instance = this;
            InitializeComponent();

            ResetMainPage();
        }

        public void ResetMainPage()
        {
            ContentPage page;
            if (String.IsNullOrEmpty(BandyerSdkForms.GetLoggedUserAlias()))
            {
                page = new ChooseCallerPage();
            }
            else
            {
                page = new ChooseCalleePage();
            }
            var navPage = new NavigationPage(page);
            navPage.BarTextColor = Color.White;
            navPage.BarBackgroundColor = Color.FromHex("#004c8c");
            MainPage = navPage;
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
