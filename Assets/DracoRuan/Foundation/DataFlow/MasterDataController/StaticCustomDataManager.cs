using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class StaticCustomDataManager : IStaticCustomDataManager
    {
        private bool _isDisposed;
        private readonly Dictionary<Type, IStaticGameDataController> _staticDataHandlers = new();
        
        private static readonly Func<Type, bool> IsNotNull = IsTypeNotNull;
        private static readonly Func<Type, bool> IsDataController = IsDataControllerType;
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
                if (TypeFactory.Create(dataHandlerType) is not IStaticGameDataController dataHandler)
                    continue;
                
                dataHandler.InjectDataManager(mainDataManager);
                await dataHandler.Initialize();
                this._staticDataHandlers.Add(dataHandlerType, dataHandler);
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

        private static bool IsDataControllerType(Type type) =>
            type.GetCustomAttribute(typeof(StaticGameDataControllerAttribute)) != null;
        
        private static bool TypeIsConcreteClassOrStruct(Type type)
            => (type.IsClass && !type.IsAbstract) || type.IsValueType;

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IStaticGameDataController
        {
            Type sourceDataType = typeof(TDataHandler);
            return this._staticDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
        }
        
        ~StaticCustomDataManager() => this.Dispose(false);
        
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
                this._staticDataHandlers.Clear();
            
            this._isDisposed = true;
        }
    }
}
