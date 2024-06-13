namespace Mango.MessageBus
{
    public interface IMessageBus
    {
        Task PublishMessage(BaseMessage baseMessage, string topicname);
    }
}