using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitTransfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IRabbitProducer
    {
        Task PublishMessage(long matchId, string message);
        Task PublishMessage(string message);
    }
    public class RabbitProducer : IRabbitProducer
    {
        private readonly ILogger<RabbitProducer> _logger;
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string queueName;

        public RabbitProducer(ILogger<RabbitProducer> logger, IConfiguration configuration)
        {
            //_logger = logger;

            ////TODO: Get Queuename and Connection from DI
            //queueName = configuration.GetValue<string>("QUEUE_NAME");
            //connection = RabbitInitializer.GetNewConnection();

            //channel = connection.CreateModel();
            //channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false);
        }

        public async Task PublishMessage(string message)
        {
            //IBasicProperties props = channel.CreateBasicProperties();

            //// Generate CorrelationId
            //props.CorrelationId = new Guid().ToString();

            //// Encode message
            //byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

            //channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: messageBytes);
        }

        public async Task PublishMessage(long matchId, string message)
        {
            //IBasicProperties props = channel.CreateBasicProperties();

            //// Use matchId as CorrelationId
            //props.CorrelationId = matchId.ToString();

            //// Encode message
            //byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

            //channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: messageBytes);
        }
    }
}

