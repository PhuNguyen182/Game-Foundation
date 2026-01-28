using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.MessageBrokers.MessageTypes;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Utils
{
    public struct MessageBrokerUtils<TAsyncMessageData, TAsyncMessage>
        where TAsyncMessageData : IAsyncMessageData
        where TAsyncMessage : AsyncMessageType<TAsyncMessageData>
    {
        public static UniTask<TAsyncMessageData> PublishAsyncMessage(IPublisher<TAsyncMessage> publisher,
            TAsyncMessageData messageData)
        {
            TAsyncMessage message = TypeFactory.Create<TAsyncMessage>();
            message.MessageData = messageData;
            message.CompletionSource = new UniTaskCompletionSource<TAsyncMessageData>();

            publisher.Publish(message);
            return message.CompletionSource.Task;
        }

        public static bool SendBackMessage(AsyncMessageType<TAsyncMessageData> message, TAsyncMessageData data) =>
            message.CompletionSource?.TrySetResult(data) ?? false;
    }
}
