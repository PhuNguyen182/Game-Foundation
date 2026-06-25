using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using Firebase.Extensions;
using Firebase.RemoteConfig;

namespace DracoRuan.RemoteConfig
{
    public class FirebaseRemoteConfigService : IRemoteConfigService, IAsyncInitializable
    {
        private const string LogTag = "FirebaseRemoteConfig";
        
        private FirebaseRemoteConfig _remoteConfig;
        
        private bool _disposed;
        private bool _isInitialized;
        
        public async UniTask Initialize()
        {
            try
            {
                this._isInitialized = false;
                this._remoteConfig = FirebaseRemoteConfig.DefaultInstance;
                this._remoteConfig.OnConfigUpdateListener += this.OnConfigUpdated;
                var configSettings = new ConfigSettings
                {
                    MinimumFetchIntervalInMilliseconds = 1000
                };

                await this._remoteConfig.SetConfigSettingsAsync(configSettings);
                Debug.Log("Firebase Remote Config initialized successfully!");
                await this.FetchDataAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Remote Config initialization failed: {ex.Message}");
            }
            finally
            {
                this._isInitialized = true;
            }
        }

        private async UniTask FetchDataAsync()
        {
            try
            {
                await this._remoteConfig.FetchAsync(TimeSpan.Zero).ContinueWithOnMainThread(FetchComplete);
                Debug.Log("Remote Config fetched and activated!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Remote Config fetch failed: {ex.Message}");
            }
        }
        
        private void FetchComplete(Task fetchTask)
        {
            if (!fetchTask.IsCompleted)
            {
                Debug.LogError("Retrieval hasn't finished.");
                return;
            }
            
            ConfigInfo configInfo = this._remoteConfig.Info;
            if (configInfo.LastFetchStatus != LastFetchStatus.Success)
            {
                Debug.LogError($"{nameof(FetchComplete)} was unsuccessful\n{nameof(configInfo.LastFetchStatus)}: {configInfo.LastFetchStatus}");
                return;
            }
            
            this._remoteConfig.ActivateAsync().ContinueWithOnMainThread(_ =>
            {
                Debug.Log($"Remote data loaded and ready for use. Last fetch time {configInfo.FetchTime}.");
            });
        }
        
        private void OnConfigUpdated(object sender, ConfigUpdateEventArgs e)
        {
            if (e.Error != RemoteConfigError.None)
            {
                Debug.Log($"Error occurred while listening: {e.Error}");
                return;
            }

            string updatedKey = string.Join(", ", e.UpdatedKeys);
            Debug.Log($"Updated keys: {updatedKey}");

            this._remoteConfig.ActivateAsync().ContinueWithOnMainThread(_ =>
            {
                DisplayWelcomeMessage();
            });
            return;

            void DisplayWelcomeMessage()
            {
                Debug.Log("You are now on the latest version of remote config!");
            }
        }
        
        public int GetIntValue(string key)
        {
            int result = (int)this._remoteConfig.GetValue(key).LongValue;
            Debug.Log($"[{LogTag}] Fetched int value for key {key}: {result}");
            return result;
        }

        public long GetLongValue(string key)
        {
            long result = this._remoteConfig.GetValue(key).LongValue;
            Debug.Log($"[{LogTag}] Fetched long value for key {key}: {result}");
            return result;
        }

        public bool GetBoolValue(string key)
        {
            bool result = this._remoteConfig.GetValue(key).BooleanValue;
            Debug.Log($"[{LogTag}] Fetched bool value for key {key}: {result}");
            return result;
        }

        public double GetDoubleValue(string key)
        {
            double result = this._remoteConfig.GetValue(key).DoubleValue;
            Debug.Log($"[{LogTag}] Fetched double value for key {key}: {result}");
            return result;
        }

        public string GetStringValue(string key)
        {
            string result = this._remoteConfig.GetValue(key).StringValue;
            Debug.Log($"[{LogTag}] Fetched string value for key {key}: {result}");
            return result;
        }

        public bool IsInitialized() => this._isInitialized;
        
        private void ReleaseUnmanagedResources()
        {
            if (this._remoteConfig != null)
                this._remoteConfig.OnConfigUpdateListener -= this.OnConfigUpdated;
        }

        private void Dispose(bool disposing)
        {
            if (this._disposed)
                return;
            
            if (disposing)
            {
                this.ReleaseUnmanagedResources();
            }
            
            this._disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FirebaseRemoteConfigService() => this.Dispose(false);
    }
}