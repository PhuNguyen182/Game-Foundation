using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.RemoteConfig;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;

namespace DracoRuan.Tracking
{
    public class FirebaseService : IAsyncInitializable, IDisposable
    {
        private readonly IRemoteConfigService _remoteConfigService;
        
        private FirebaseApp _firebaseApp;
        private bool _isInitialized;

        public FirebaseService(IRemoteConfigService remoteConfigService)
        {
            this._isInitialized = false;
            this._remoteConfigService = remoteConfigService;
            this.Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            try
            {
                await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    var dependencyStatus = task.Result;
                    if (dependencyStatus == DependencyStatus.Available)
                    {
                        this._firebaseApp = FirebaseApp.DefaultInstance;
                        Debug.Log($"Firebase initialized successfully!\n" +
                                  $"Firebase app name: {this._firebaseApp.Name} with option: {this._firebaseApp.Options}");
                        this.InitializeServices().Forget();
                    }
                    else
                    {
                        Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Firebase initialization failed: {ex.Message}");
            }
        }

        private async UniTaskVoid InitializeServices()
        {
            this.InitializeAnalytics();
            this.InitializeCrashlytics();
            await this.InitializeRemoteConfig();
            this._isInitialized = true;
        }

        #region Firebase Analytics
        
        private void InitializeAnalytics()
        {
            try
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase Analytics initialized successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Analytics initialization failed: {ex.Message}");
            }
        }
        
        #endregion

        #region Firebase Remote Config
        
        private async UniTask InitializeRemoteConfig()
        {
            await this._remoteConfigService.Initialize();
        }
        
        #endregion
        
        #region Firebase Crashlytics
        
        private void InitializeCrashlytics()
        {
            Crashlytics.ReportUncaughtExceptionsAsFatal = false;
            Debug.Log("Firebase Crashlytics Initialized successfully with ReportUncaughtExceptionsAsFatal set to False!");
        }
        
        #endregion
        
        public bool IsInitialized() => this._isInitialized;
        
        public void Dispose() => this._remoteConfigService?.Dispose();
    }
}
