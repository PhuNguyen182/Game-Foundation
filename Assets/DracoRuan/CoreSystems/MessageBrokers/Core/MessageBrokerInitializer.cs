using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DracoRuan.CoreSystems.MessageBrokers.MessageTypes;
using MessagePipe;
using ZLinq;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    public class MessageBrokerInitializer
    {
        private readonly BuiltinContainerBuilder _containerBuilder;
        private static readonly Func<Assembly, IEnumerable<Type>> GetTypesOfAssemblyFunc = GetTypesOfAssembly;
        private static readonly Func<Type, bool> TypeIsConcreteClassOrStructFunc = TypeIsConcreteClassOrStruct;
        private static readonly Func<Type, bool> TypeValidation = IsMessageBrokerData;
        private static readonly Type HandlerInterfaceType = typeof(IMessageType);
        
        private static readonly string[] AssemblyPrefixesToScan =
        {
            "Assembly-CSharp",
        };

        public MessageBrokerInitializer()
        {
            this._containerBuilder = new BuiltinContainerBuilder();
            this._containerBuilder.AddMessagePipe();
            this.AddMessageBrokers();
            IServiceProvider serviceProvider = this._containerBuilder.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(serviceProvider);
        }

        private void AddMessageBrokers()
        {
            const string addMessageBrokerMethodName = "AddMessageBroker";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .AsValueEnumerable().Where(assembly =>
                    AssemblyPrefixesToScan.Any(prefix => assembly.GetName().Name.StartsWith(prefix)));

            Debug.Log($"Scanning {assemblies.Count()} assemblies for message types");
            var allMessageTypes = assemblies
                .SelectMany(GetTypesOfAssemblyFunc)
                .Where(TypeIsConcreteClassOrStructFunc)
                .Where(TypeValidation);

            Debug.Log($"Found {allMessageTypes.Count()} message types to register");
            var method = typeof(BuiltinContainerBuilder)
                .GetMethod(addMessageBrokerMethodName, BindingFlags.Public | BindingFlags.Instance);

            if (method == null || !method.IsGenericMethodDefinition)
            {
                throw new InvalidOperationException(
                    "AddMessageBroker<T> method not found on BuiltinContainerBuilder");
            }

            int successCount = 0;
            int failureCount = 0;

            foreach (var messageType in allMessageTypes)
            {
                try
                {
                    var genericMethod = method.MakeGenericMethod(messageType);
                    genericMethod.Invoke(_containerBuilder, null);
                    successCount++;
                    Debug.Log($"Registered: {messageType.FullName}");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Debug.LogError($"Exception: {ex.Message} Failed to register: {messageType.FullName}");
                }
            }

            if (failureCount > 0)
            {
                Debug.LogWarning($"Registration completed with errors: {successCount} succeeded, {failureCount} failed");
            }
            else
            {
                Debug.Log($"All {successCount} message types registered successfully");
            }
        }

        private static IEnumerable<Type> GetTypesOfAssembly(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(typeLoad => typeLoad != null);
            }
        }

        private static bool TypeIsConcreteClassOrStruct(Type type)
            => (type.IsClass || type.IsValueType) && !type.IsAbstract &&
               type.GetCustomAttribute<MessageBrokerAttribute>() != null;

        private static bool IsMessageBrokerData(Type type) => HandlerInterfaceType.IsAssignableFrom(type);
    }
}