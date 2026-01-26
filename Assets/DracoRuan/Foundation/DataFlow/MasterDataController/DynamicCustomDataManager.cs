using System;
using System.Collections.Generic;
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
        private readonly Dictionary<Type, IDynamicGameDataController> _dynamicDataHandlers = new();
        
        private static readonly Func<Type, bool> IsNotNull = IsTypeNotNull;
        private static readonly Func<Type, bool> IsDataController = IsDynamicDataController;
        private static readonly Func<Assembly, IEnumerable<Type>> GetTypesDelegate = GetTypesOfAssembly;
        private static readonly Func<Type, bool> TypeIsConcrete = TypeIsConcreteClassOrStruct;
        
        public async UniTask InitializeDataHandlers(IMainDataManager mainDataManager)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var allDataHandlerTypes = assemblies
                .AsValueEnumerable()
                .SelectMany(GetTypesDelegate)
                .Where(TypeIsConcrete)
                .Where(IsDataController);
            
            foreach (Type dataHandlerType in allDataHandlerTypes)
            {
                if (TypeFactory.Create(dataHandlerType) is not IDynamicGameDataController dataHandler)
                    continue;
                
                dataHandler.InjectDataManager(mainDataManager);
                await dataHandler.LoadData();
                dataHandler.Initialize();
                this._dynamicDataHandlers.Add(dataHandlerType, dataHandler);
            }
        }
        
        private static bool IsTypeNotNull(Type type) => type != null;

        private static IEnumerable<Type> GetTypesOfAssembly(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Debug.LogError($"ReflectionTypeLoadException: {e.Message}");
                return e.Types.Where(IsNotNull);
            }
        }

        private static bool IsDynamicDataController(Type type) =>
            type.GetCustomAttribute(typeof(DynamicGameDataControllerAttribute)) != null;
        
        private static bool TypeIsConcreteClassOrStruct(Type type)
            => (type.IsClass && !type.IsAbstract) || type.IsValueType;

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IDynamicGameDataController
        {
            Type sourceDataType = typeof(TDataHandler);
            return this._dynamicDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
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
            using var listPool = ListPool<UniTask>.Get(out List<UniTask> saveDataTasks);
            foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
            {
                UniTask saveDataTask = dynamicDataHandler.SaveDataAsync();
                saveDataTasks.Add(saveDataTask);
            }
            
            await UniTask.WhenAll(saveDataTasks);
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
            {
                this.SaveAllData();
                this._dynamicDataHandlers.Clear();
            }

            this._isDisposed = true;
        }
    }
}
