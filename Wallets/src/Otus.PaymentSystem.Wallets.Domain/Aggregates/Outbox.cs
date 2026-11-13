using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Domain.Aggregates
{
    public class Outbox
    {
        public Guid Id { get; private set; }
        public string EventType { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public DateTimeOffset OccurredAt { get; private set; }
        public DateTimeOffset? PublishedAt { get; private set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="Outbox"/>.
        /// </summary>
        /// <remarks>
        /// Этот конструктор является приватным и, вероятно, используется для десериализации или ORM.
        /// </remarks>
        private Outbox() { }

        private Outbox(string eventType, string payload)
        {
            Id = Guid.NewGuid();
            EventType = eventType;
            Payload = payload;
            OccurredAt = DateTimeOffset.UtcNow;
        }
    }
}
