using System;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using Windows.UI.Popups;

namespace Signal_Windows.ViewModels
{
    public class FinishRegistrationPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<FinishRegistrationPageViewModel>();
        public FinishRegistrationPage View { get; set; }

        internal async Task OnNavigatedTo()
        {
            try
            {
                CancellationTokenSource cancelSource = new CancellationTokenSource();
                await Task.Run(async () =>
                {
                    string SignalingKey = Base64.EncodeBytes(Util.GetSecretBytes(52));
                    SignalServiceAccountManager accountManager = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.AccountManager;

                    Guid ownGuid = await accountManager.VerifyAccountWithCodeAsync(
                        App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.VerificationCode.Replace("-", ""),
                        SignalingKey,
                        App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.SignalRegistrationId,
                        true,
                        null,
                        null,
                        false,
                        cancelSource.Token);
                    SignalStore store = new SignalStore()
                    {
                        DeviceId = 1,
                        IdentityKeyPair = Base64.EncodeBytes(App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.IdentityKeyPair.serialize()),
                        NextSignedPreKeyId = 1,
                        Password = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.Password,
                        PreKeyIdOffset = 1,
                        Registered = true,
                        RegistrationId = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterFinalizationPageInstance.SignalRegistrationId,
                        SignalingKey = SignalingKey,
                        Username = App.CurrentSignalWindowsFrontend(App.MainViewId).Locator.RegisterPageInstance.FinalNumber,
                        OwnGuid = ownGuid
                    };
                    LibsignalDBContext.SaveOrUpdateSignalStore(store);
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Handle.Store = store;
                    }).AsTask().Wait();

                    /* create prekeys */
                    await LibsignalDBContext.RefreshPreKeysAsync(new SignalServiceAccountManager(
                        LibUtils.ServiceConfiguration, store.OwnGuid, store.Username, store.Password, (int)store.DeviceId,
                            LibUtils.USER_AGENT, LibUtils.HttpClient),
                        cancelSource.Token);

                    /* reload again with prekeys and their offsets */
                    store = LibsignalDBContext.GetSignalStore();
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        App.Handle.Store = store;
                    }).AsTask().Wait();
                });
                var frontend = App.CurrentSignalWindowsFrontend(App.MainViewId);
                await App.Handle.Reacquire();
                View.Frame.Navigate(typeof(MainPage));
            }
            catch (Exception e)
            {
                Logger.LogError("OnNavigatedTo() failed: {0}\n{1}", e.Message, e.StackTrace);
                var title = "Verification failed";
                var content = "Please enter the correct verification code.";
                MessageDialog dialog = new MessageDialog(content, title);
                var result = dialog.ShowAsync();
                View.Frame.Navigate(typeof(RegisterPage));
            }
        }
    }
}
