using System;
using Acr.UserDialogs;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Xamarin.Forms;
using XLabs.Forms;
using XLabs.Ioc;
using XLabs.Platform.Device;
using XLabs.Platform.Services.Geolocation;
using XLabs.Platform.Services.Media;

namespace CognitiveServicesXamarinTest.Droid
{
    [Activity(Label = "CognitiveServicesXamarinTest", Icon = "@drawable/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);

            UserDialogs.Init(this);
            Microsoft.WindowsAzure.MobileServices.CurrentPlatform.Init();

            DependencyService.Register<IMediaPicker, MediaPicker>();
            DependencyService.Register<IImageResizer, ImageResizer>();

            global::Xamarin.Forms.Forms.Init(this, bundle);
            LoadApplication(new App());
        }
    }
}

