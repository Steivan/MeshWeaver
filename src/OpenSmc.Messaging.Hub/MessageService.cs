﻿using Microsoft.Extensions.Logging;
using OpenSmc.Serialization;
using System.Reflection;
using System.Threading.Tasks.Dataflow;

namespace OpenSmc.Messaging.Hub;

public class MessageService : IMessageService
{
    private readonly List<IMessageHandler> registeredHandlers = new();
    private readonly ISerializationService serializationService;
    private readonly ILogger<MessageService> logger;
    private ActionBlock<IMessageDelivery> executionQueueAction;
    private bool isDisposing;
    private readonly BufferBlock<IMessageDelivery> buffer = new();
    private ActionBlock<IMessageDelivery> deliveryAction;


    public void Schedule(Func<Task> action) => topQueue.Schedule(action);
    public Task<bool> FlushAsync() => topQueue.Flush();
    private readonly DeferralContainer deferralContainer;


    private ExecutionQueue topQueue;

    public MessageService(object address, ISerializationService serializationService, ILogger<MessageService> logger)
    {
        Address = address;
        this.serializationService = serializationService;
        this.logger = logger;
        topQueue = new(logger);
        executionQueueAction = new(d => topQueue.Schedule(() =>
        {
            try
            {
                return NotifyAsync(d);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when calling NotifyAsync");
                return Task.CompletedTask;
            }
        }));

        deferralContainer = new DeferralContainer(executionQueueAction);
    }

    private bool IsStarted;
    void IMessageService.Start()
    {
        if (IsStarted)
            return;
        IsStarted = true;
        topQueue.InstantiateActionBlock();
        deliveryAction = new(d =>
        {
            try
            {
                deferralContainer.DeferMessage(d);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when calling DeferMessage");
            }
        });
        buffer.LinkTo(deliveryAction, new DataflowLinkOptions { PropagateCompletion = true });
    }


    public object Address { get; }

    void IMessageService.AddHandler(IMessageHandler handler)
    {
        registeredHandlers.Add(handler);
        handler.Connect(this, Address);

    }
    void IMessageService.RemoveHandler(IMessageHandler handler)
    {
        registeredHandlers.Remove(handler);
    }

    public IDisposable Defer(Predicate<IMessageDelivery> deferredFilter)
    {
        return deferralContainer.Defer(deferredFilter);
    }

    IMessageDelivery IMessageService.IncomingMessage(IMessageDelivery delivery)
    {
        return ScheduleNotify(delivery);
    }

    private IMessageDelivery ScheduleNotify(IMessageDelivery delivery)
    {
        if (Address.Equals(delivery.Target))
            delivery = UnpackIfNecessary(delivery);

        buffer.Post(delivery);
        return delivery;
    }

    // TODO V10: This is needed only when coming from outside physical boundries (2023/07/16, Roland Buergi)
    private IMessageDelivery UnpackIfNecessary(IMessageDelivery delivery)
    {
        try
        {
            delivery = serializationService.DeserializeDelivery(delivery);
        }
        catch
        {
            logger.LogWarning("Failed to deserialize delivery {PackedDelivery}", delivery);
            // failed unpack delivery, returning original delivery with message type RawJson
        }

        return delivery;
    }



    private async Task<IMessageDelivery> NotifyAsync(IMessageDelivery delivery)
    {
        foreach (var handler in registeredHandlers.ToArray()) // we run into collection modified exception
            delivery = await handler.HandleMessageAsync(delivery);
        return delivery;
    }

    public IMessageDelivery Post<TMessage>(TMessage message, Func<PostOptions, PostOptions> configure)
    {
        return PostImplGeneric<TMessage>(message, configure);
    }

    private IMessageDelivery PostImpl(object message, Func<PostOptions, PostOptions> configure)
    => (IMessageDelivery)PostImplMethod.MakeGenericMethod(message.GetType()).Invoke(this, new[] { message, configure });


    private static readonly MethodInfo PostImplMethod = typeof(MessageService).GetMethod(nameof(PostImplGeneric), BindingFlags.Instance | BindingFlags.NonPublic);

    private IMessageDelivery<TMessage> PostImplGeneric<TMessage>(TMessage message, Func<PostOptions, PostOptions> configure)
    {
        if (typeof(TMessage) != message.GetType())
            return (IMessageDelivery<TMessage>)PostImplMethod.MakeGenericMethod(message.GetType()).Invoke(this, new object[] { message, configure });

        var opt = new PostOptions(Address);
        if (configure != null)
            opt = configure(opt);

        var delivery = new MessageDelivery<TMessage>(message, opt);
        ScheduleNotify(delivery);
        return delivery;
    }


    public async ValueTask DisposeAsync()
    {
        if (isDisposing)
            return;
        isDisposing = true;
        do
        {
            await topQueue.Flush();
        } while (topQueue.NeedsFlush);


        buffer.Complete();
        await deliveryAction.Completion;

        executionQueueAction.Complete();
        await executionQueueAction.Completion;

        foreach (var handler in registeredHandlers.Where(x => x is not IMessageHub).ToArray())
            await handler.DisposeAsync();

        await topQueue.DisposeAsync();

        deferralContainer.Dispose();
    }
}