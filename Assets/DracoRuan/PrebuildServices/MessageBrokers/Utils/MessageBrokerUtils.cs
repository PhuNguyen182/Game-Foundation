using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MessageBrokers.MessageTypes;
using MessagePipe;

namespace DracoRuan.PrebuildServices.MessageBrokers.Utils
{
    public struct MessageBrokerUtils<TAsyncMessageData, TAsyncMessage>
        where TAsyncMessageData : IAsyncMessageData
        where TAsyncMessage : AsyncMessageType<TAsyncMessageData>, new()
    {
        public static UniTask<TAsyncMessageData> PublishAsyncMessage(IPublisher<TAsyncMessage> publisher,
            TAsyncMessageData messageData)
        {
            TAsyncMessage message = new()
            {
                MessageData = messageData,
                CompletionSource = new UniTaskCompletionSource<TAsyncMessageData>()
            };

            publisher.Publish(message);
            return message.CompletionSource.Task;
        }

        public static bool SendBackMessage(AsyncMessageType<TAsyncMessageData> message, TAsyncMessageData data) =>
            message.CompletionSource?.TrySetResult(data) ?? false;
    }
}
