using Azure.Messaging.ServiceBus;
using Mango.MessageBus;
using Mango.Service.OrderAPI.Messages;
using Mango.Service.OrderAPI.Models;
using Mango.Service.OrderAPI.Repository;
using Mango.Services.OrderAPI.Messages;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Mango.Service.OrderAPI.Messaging
{
    public class AzureServiceBusConsumer: IAzureServiceBusConsumer
    {
        private readonly string _serviceBusConnectionString;
        private readonly string _checkoutMessageTopic;
        private readonly string _subscriptionCheckOut;
        private readonly string _orderPaymentProcessTopic;
        private readonly string _orderUpdatePaymentResultTopic;
        private readonly OrderRepository _orderRepository;
        private readonly IConfiguration _configuration;
        private ServiceBusProcessor _checkOutProcessor;
        private ServiceBusProcessor _orderUpdatePaymentStatusProcessor;
        private readonly IMessageBus _messageBus;

        public AzureServiceBusConsumer(OrderRepository orderRepository, IConfiguration configuration, IMessageBus messageBus)
        {
            _orderRepository = orderRepository;
            _configuration = configuration;
            _serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            _checkoutMessageTopic = _configuration.GetValue<string>("CheckoutMessageTopic");
            _subscriptionCheckOut = _configuration.GetValue<string>("SubscriptionCheckOut");
            _orderPaymentProcessTopic = _configuration.GetValue<string>("OrderPaymentProcessTopic");
            _orderUpdatePaymentResultTopic = _configuration.GetValue<string>("OrderUpdatePaymentResultTopic");

            var client = new ServiceBusClient(_serviceBusConnectionString);
            _checkOutProcessor = client.CreateProcessor(_checkoutMessageTopic, _subscriptionCheckOut);
            _messageBus = messageBus;
            _orderUpdatePaymentStatusProcessor = client.CreateProcessor(_orderUpdatePaymentResultTopic, _subscriptionCheckOut);
        }

        public async Task Start()
        {
            _checkOutProcessor.ProcessMessageAsync += OnCheckOutMessageReceived;
            _checkOutProcessor.ProcessErrorAsync += ErrorHandler;
            await _checkOutProcessor.StartProcessingAsync();

            _orderUpdatePaymentStatusProcessor.ProcessMessageAsync += OnOrderPaymentUpdateReceived;
            _orderUpdatePaymentStatusProcessor.ProcessErrorAsync += ErrorHandler;
            await _orderUpdatePaymentStatusProcessor.StartProcessingAsync();
        }

        public async Task Stop()
        {
            await _checkOutProcessor.StopProcessingAsync();
            await _checkOutProcessor.DisposeAsync();

            await _orderUpdatePaymentStatusProcessor.StopProcessingAsync();
            await _orderUpdatePaymentStatusProcessor.DisposeAsync();
        }

        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

        private async Task OnCheckOutMessageReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            CheckoutHeaderDto checkoutHeaderDto = JsonConvert.DeserializeObject<CheckoutHeaderDto>(body);

            OrderHeader orderHeader = new()
            {
                UserId = checkoutHeaderDto.UserId,
                CouponCode = checkoutHeaderDto.CouponCode,
                OrderTotal = checkoutHeaderDto.OrderTotal,
                DiscountTotal = checkoutHeaderDto.DiscountTotal,
                FirstName = checkoutHeaderDto.FirstName,
                LastName = checkoutHeaderDto.LastName,
                PickupDateTime = checkoutHeaderDto.PickupDateTime,
                OrderTime = DateTime.Now,
                Phone = checkoutHeaderDto.Phone,
                Email = checkoutHeaderDto.Email,
                CardNumber = checkoutHeaderDto.CardNumber,
                CVV = checkoutHeaderDto.CVV,
                ExpiryMonthYear = checkoutHeaderDto.ExpiryMonthYear,
                PaymentStatus = false,
                OrderDetails = new List<OrderDetails>()
            };

            foreach (var detaillist in checkoutHeaderDto.CartDetails)
            {
                OrderDetails orderDetails = new()
                {
                    ProductId = detaillist.ProductId,
                    ProductName = detaillist.Product.Name,
                    Price = detaillist.Product.Price,
                    Count = detaillist.Count
                };
                orderHeader.CartTotalItems += detaillist.Count;
                orderHeader.OrderDetails.Add(orderDetails);
            }
            await _orderRepository.AddOrder(orderHeader);

            PaymentRequestMessage paymentRequestMessage = new()
            {
                Name = orderHeader.FirstName + " " + orderHeader.LastName,
                CardNumber = orderHeader.CardNumber,
                CVV = orderHeader.CVV,
                ExpiryMonthYear = orderHeader.ExpiryMonthYear,
                OrderId = orderHeader.OrderHeaderId,
                OrderTotal = orderHeader.OrderTotal,
                Email = orderHeader.Email
            };

            try
            {
                await _messageBus.PublishMessage(paymentRequestMessage, _orderPaymentProcessTopic);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        private async Task OnOrderPaymentUpdateReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            UpdatePaymentResultMessage updatePaymentResultMessage = JsonConvert.DeserializeObject<UpdatePaymentResultMessage>(body);

            await _orderRepository.UpdateOrderPaymentStatus(updatePaymentResultMessage.OrderId, updatePaymentResultMessage.Status);
            await args.CompleteMessageAsync(args.Message);
        }
    }
}
