﻿using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Surging.Core.EventBusKafka.Implementation
{
   public class KafkaConsumerPersistentConnection : KafkaPersistentConnectionBase
    {
        private readonly ILogger<KafkaConsumerPersistentConnection> _logger;
        private Consumer<Null, string> _consumerClient;
        private readonly IDeserializer<string> _stringDeserializer;
        bool _disposed;

        public KafkaConsumerPersistentConnection(ILogger<KafkaConsumerPersistentConnection> logger)
            : base(logger, AppConfig.KafkaConsumerConfig)
        {
            _logger = logger;
            _stringDeserializer = new StringDeserializer(Encoding.UTF8);
        }

        public override bool IsConnected => _consumerClient != null && !_disposed;

        public override Action Connection(IEnumerable<KeyValuePair<string, object>> options)
        {
            return () =>
            {
                _consumerClient = new Consumer<Null, string>(options, null, _stringDeserializer);
                _consumerClient.OnConsumeError +=  OnConsumeError;
                _consumerClient.OnError += OnConnectionException;
            };
        }

        public override object CreateConnect()
        {
            return _consumerClient;
        }

        private void OnConsumeError(object sender, Message e)
        {
            var message = e.Deserialize<Null, string>(null, _stringDeserializer);
            if (_disposed) return;

            _logger.LogWarning($"An error occurred during consume the message; Topic:'{e.Topic}'," +
                $"Message:'{message.Value}', Reason:'{e.Error}'.");

            TryConnect();
        }

        private void OnConnectionException(object sender, Error error)
        {
            if (_disposed) return;

            _logger.LogWarning($"A Kafka connection throw exception.info:{error} ,Trying to re-connect...");

            TryConnect();
        }

        public override void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                _consumerClient.Dispose();
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }
    }
}
