namespace NetTelegramBot.Framework.Model
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using NetTelegramBotApi.Types;

    public class ChatMessage : TableEntity
    {
        private const int CurrentSerializationVersion = 1;

        public ChatMessage()
        {
            // Nothing
        }

        public Message Message { get; private set; }

        public InlineQuery InlineQuery { get; private set; }

        public ChosenInlineResult ChosenInlineResult { get; private set; }

        public CallbackQuery CallbackQuery { get; private set; }

        public object Request { get; private set; }

        public long ChatId { get; private set; }

        public long FromId { get; private set; }

        public static ChatMessage Create(Message message)
        {
            var key = GetKey(message.Chat.Id, message.Date);

            return new ChatMessage()
            {
                PartitionKey = key.PartitionKey,
                RowKey = key.RowKey,

                Message = message,
                ChatId = message.Chat.Id,
                FromId = message.From.Id,
            };
        }

        public static ChatMessage Create(InlineQuery inlineQuery)
        {
            var key = GetKey(0, DateTimeOffset.Now);

            return new ChatMessage()
            {
                PartitionKey = key.PartitionKey,
                RowKey = key.RowKey,

                InlineQuery = inlineQuery,
                ChatId = 0,
                FromId = inlineQuery.From.Id,
            };
        }

        public static ChatMessage Create(ChosenInlineResult chosenInlineResult)
        {
            var key = GetKey(0, DateTimeOffset.Now);

            return new ChatMessage()
            {
                PartitionKey = key.PartitionKey,
                RowKey = key.RowKey,

                ChosenInlineResult = chosenInlineResult,
                ChatId = 0,
                FromId = chosenInlineResult.From.Id,
            };
        }

        public static ChatMessage Create(CallbackQuery callbackQuery)
        {
            var key = GetKey(callbackQuery.Message?.Chat?.Id ?? 0, DateTimeOffset.Now);

            return new ChatMessage()
            {
                PartitionKey = key.PartitionKey,
                RowKey = key.RowKey,

                CallbackQuery = callbackQuery,
                ChatId = callbackQuery.Message?.Chat?.Id ?? 0,
                FromId = callbackQuery.From.Id,
            };
        }

        public static EntityKey<ChatMessage> GetKey(long chatId, DateTimeOffset timestamp)
        {
            return new EntityKey<ChatMessage>
            {
                PartitionKey = chatId.ToString(),
                RowKey = timestamp.GetInvertedTicks()
            };
        }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            //// base.ReadEntity(properties, operationContext); - there is no any 'base' properties

            var sourceEntityType = properties[EntityPropertyExtensions.EntityTypePropertyName].StringValue;
            if (!string.Equals(nameof(ChatMessage), sourceEntityType))
            {
                throw new Exception($"Can't restore from entity type {sourceEntityType}");
            }

            var serializationVersion = properties[EntityPropertyExtensions.SerializationVersionPropertyName].Int32Value.Value;
            if (serializationVersion > CurrentSerializationVersion)
            {
                throw new Exception($"Can't restore from serialization version {serializationVersion}");
            }

            Message = properties.Deserialize<Message>(nameof(Message));
            ChatId = properties[nameof(ChatId)].Int64Value.Value;
            FromId = properties[nameof(FromId)].Int64Value.Value;
        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            //// var dic = base.WriteEntity(operationContext); - there is no any 'base' properties

            var dic = new Dictionary<string, EntityProperty>();
            dic.Add(nameof(ChatId), new EntityProperty(ChatId));
            dic.Add(nameof(FromId), new EntityProperty(FromId));

            if (Message != null)
            {
                dic.AddSerialized(nameof(Message), Message);
                ObjectToDictionary(Message, dic);
            }

            if (InlineQuery != null)
            {
                dic.AddSerialized(nameof(InlineQuery), InlineQuery);
                ObjectToDictionary(InlineQuery, dic);
            }

            if (ChosenInlineResult != null)
            {
                dic.AddSerialized(nameof(ChosenInlineResult), ChosenInlineResult);
                ObjectToDictionary(ChosenInlineResult, dic);
            }

            if (CallbackQuery != null)
            {
                dic.AddSerialized(nameof(CallbackQuery), CallbackQuery);
                ObjectToDictionary(CallbackQuery, dic);
            }

            dic.Add(EntityPropertyExtensions.SerializationVersionPropertyName, new EntityProperty(CurrentSerializationVersion));
            dic.Add(EntityPropertyExtensions.EntityTypePropertyName, new EntityProperty(nameof(ChatMessage)));
            return dic;
        }

        private static void ObjectToDictionary(object value, Dictionary<string, EntityProperty> dictionary)
        {
            if (value == null)
            {
                return;
            }

            foreach (var prop in value.GetType().GetProperties())
            {
                if (prop.PropertyType.IsSubclassOf(typeof(System.IO.Stream)))
                {
                    continue;
                }

                var val = prop.GetValue(value);
                if (val == null)
                {
                    continue;
                }

                if (prop.PropertyType.IsPrimitive)
                {
                    dictionary.Add(prop.Name, new EntityProperty(val.ToString()));
                }
                else if (val is string stringVal)
                {
                    if (!string.IsNullOrEmpty(stringVal))
                    {
                        dictionary.Add(prop.Name, new EntityProperty(stringVal));
                    }
                }
                else
                {
                    dictionary.AddSerialized(prop.Name, val);
                }
            }
        }
    }
}
