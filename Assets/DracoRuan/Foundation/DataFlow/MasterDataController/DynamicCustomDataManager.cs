using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using UnityEngine;
using UnityEngine.Pool;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class DynamicCustomDataManager : IDynamicCustomDataManager
    {
        private bool _isDisposed;
        private bool _isInitialized;
        
        private readonly object _lock = new();
        private readonly Dictionary<Type, IDynamicGameDataController> _dynamicDataHandlers = new();
        
        private static readonly string[] AssemblyPrefixesToScan =
        {
            "Assembly-CSharp", // Main game code
            // Không scan Unity, System, etc.
        };

        public async UniTask InitializeDataControllers(IMainDataManager mainDataManager)
        {
            lock (this._lock)
            {
                if (this._isInitialized)
                {
                    Debug.LogWarning("StaticCustomDataManager already initialized");
                    return;
                }
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Chỉ scan assemblies liên quan
            var relevantAssemblies = assemblies.AsValueEnumerable()
                .Where(assembly => AssemblyPrefixesToScan.Any(prefix => assembly.GetName().Name.StartsWith(prefix)));

            List<Type> dataHandlerTypes = new List<Type>();
            foreach (Assembly assembly in relevantAssemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(type => (type.IsClass && !type.IsAbstract) || type.IsValueType)
                        .Where(type => type.GetCustomAttribute<StaticGameDataControllerAttribute>() != null);

                    dataHandlerTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException e)
                {
                    Debug.LogError($"Failed to load types from {assembly.GetName().Name}: {e.Message}");

                    // Log loader exceptions để debug
                    foreach (Exception loaderEx in e.LoaderExceptions)
                    {
                        if (loaderEx != null)
                            Debug.LogError($"  - {loaderEx.Message}");
                    }

                    // Vẫn lấy types load được
                    var loadedTypes = e.Types
                        .Where(type => type != null)
                        .Where(type => (type.IsClass && !type.IsAbstract) || type.IsValueType)
                        .Where(type => type.GetCustomAttribute<StaticGameDataControllerAttribute>() != null);

                    dataHandlerTypes.AddRange(loadedTypes);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unexpected error scanning {assembly.GetName().Name}: {e}");
                }
            }

            Debug.Log($"Found {dataHandlerTypes.Count} data handler types in {stopwatch.ElapsedMilliseconds}ms");
            var initializationErrors = new List<(Type type, Exception error)>();
            foreach (Type dataHandlerType in dataHandlerTypes)
            {
                try
                {
                    if (TypeFactory.Create(dataHandlerType) is not IDynamicGameDataController dataHandler)
                    {
                        Debug.LogWarning($"Failed to create instance of {dataHandlerType.Name}");
                        continue;
                    }

                    dataHandler.InjectDataManager(mainDataManager);
                    dataHandler.RegisterDataService();
                    await dataHandler.LoadData();
                    dataHandler.Initialize();

                    lock (this._lock)
                    {
                        this._dynamicDataHandlers[dataHandlerType] = dataHandler;
                    }

                    Debug.Log($"Initialized {dataHandlerType.Name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to initialize {dataHandlerType.Name}: {e}");
                    initializationErrors.Add((dataHandlerType, e));
                }
            }

            lock (this._lock)
            {
                this._isInitialized = true;
            }

            stopwatch.Stop();
            Debug.Log($"StaticCustomDataManager initialized {_dynamicDataHandlers.Count} handlers " +
                      $"in {stopwatch.ElapsedMilliseconds}ms with {initializationErrors.Count} errors");
            
            if (initializationErrors.Count > 0)
            {
                throw new AggregateException("Failed to initialize some handlers",
                    initializationErrors.Select(e => e.error));
            }
        }

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IDynamicGameDataController
        {
            lock (this._lock)
            {
                if (!this._isInitialized)
                {
                    Debug.LogWarning("GetDataHandler called before initialization");
                    return null;
                }

                Type sourceDataType = typeof(TDataHandler);
                return this._dynamicDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
            }
        }

        public void DeleteSingleData(Type dataType) =>
            this._dynamicDataHandlers.GetValueOrDefault(dataType)?.DeleteData();
        
        public void DeleteAllData()
        {
            foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                dynamicDataHandler.DeleteData();
        }
        
        public async UniTask SaveAllDataAsync()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> saveDataTasks))
            {
                foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                {
                    UniTask saveDataTask = dynamicDataHandler.SaveDataAsync();
                    saveDataTasks.Add(saveDataTask);
                }

                await UniTask.WhenAll(saveDataTasks);
            }
        }

        public void SaveAllData()
        {
            foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                dynamicDataHandler.SaveData();
            PlayerPrefs.Save();
        }

        ~DynamicCustomDataManager() => this.Dispose(false);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;

            if (disposing)
                this.ReleaseManagedResources();

            this._isDisposed = true;
        }

        private void ReleaseManagedResources()
        {
            lock (this._lock)
            {
                foreach (IDynamicGameDataController handler in this._dynamicDataHandlers.Values)
                {
                    if (handler is not IDisposable disposable) 
                        continue;
                        
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error disposing handler: {e}");
                    }
                }

                this._dynamicDataHandlers.Clear();
            }
        }
    }
}
