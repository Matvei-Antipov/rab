namespace Uchat.Server.Data.Repositories.MongoImpl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Enums;
    using DataMessage = Uchat.Server.Data.Models.Message;
    using SharedMessage = Uchat.Shared.Models.Message;

    /// <summary>
    /// MongoDB implementation of the message repository.
    /// </summary>
    public class MongoMessageRepository : IMessageRepository
    {
        private readonly IMongoCollection<DataMessage> collection;

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoMessageRepository"/> class.
        /// </summary>
        /// <param name="mongoClientFactory">The MongoDB client factory.</param>
        public MongoMessageRepository(IMongoClientFactory mongoClientFactory)
        {
            var database = mongoClientFactory.GetDatabase();
            this.collection = database.GetCollection<DataMessage>("messages");
        }

        /// <inheritdoc/>
        public async Task<SharedMessage?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<DataMessage>.Filter.Eq(x => x.Id, id);
            var dataMessage = await this.collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            return MapToShared(dataMessage);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<SharedMessage>> GetByChatIdAsync(string chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            var builder = Builders<DataMessage>.Filter;
            var filter = builder.And(
                builder.Eq(x => x.ChatId, chatId),
                builder.Ne(x => x.IsDeleted, true));

            var sort = Builders<DataMessage>.Sort.Descending(x => x.CreatedAt);

            var dataMessages = await this.collection.Find(filter)
                                                  .Sort(sort)
                                                  .Skip(offset)
                                                  .Limit(limit)
                                                  .ToListAsync(cancellationToken);

            return dataMessages.Select(MapToShared).Where(m => m != null).Cast<SharedMessage>();
        }

        /// <inheritdoc/>
        public async Task CreateAsync(SharedMessage message, CancellationToken cancellationToken = default)
        {
            var dataMessage = MapToData(message);
            await this.collection.InsertOneAsync(dataMessage, null, cancellationToken);
            message.Id = dataMessage.Id!;
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(SharedMessage message, CancellationToken cancellationToken = default)
        {
            var dataMessage = MapToData(message);
            var filter = Builders<DataMessage>.Filter.Eq(x => x.Id, message.Id);
            await this.collection.ReplaceOneAsync(filter, dataMessage, new ReplaceOptions(), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            // Soft delete implementation
            var filter = Builders<DataMessage>.Filter.Eq(x => x.Id, id);
            var update = Builders<DataMessage>.Update.Set(x => x.IsDeleted, true);
            await this.collection.UpdateOneAsync(filter, update, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> GetMessageCountAsync(string chatId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<DataMessage>.Filter.And(
                Builders<DataMessage>.Filter.Eq(x => x.ChatId, chatId),
                Builders<DataMessage>.Filter.Ne(x => x.IsDeleted, true));

            return (int)await this.collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<SharedMessage>> SearchAsync(string chatId, string query, CancellationToken cancellationToken = default)
        {
            var builder = Builders<DataMessage>.Filter;

            var chatFilter = builder.Eq(x => x.ChatId, chatId);

            // Handle legacy documents where IsDeleted might be missing (treat missing as false)
            var deletedFilter = builder.Or(
                builder.Eq(x => x.IsDeleted, false),
                builder.Exists(x => x.IsDeleted, false));

            // Regex for case-insensitive content search
            var contentFilter = builder.Regex(x => x.Content, new BsonRegularExpression(query, "i"));

            var filter = builder.And(chatFilter, deletedFilter, contentFilter);

            var dataMessages = await this.collection.Find(filter)
                                                  .SortByDescending(x => x.CreatedAt)
                                                  .ToListAsync(cancellationToken);

            return dataMessages.Select(MapToShared).Where(m => m != null).Cast<SharedMessage>();
        }

        /// <inheritdoc/>
        public async Task DeleteAllByChatIdAsync(string chatId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<DataMessage>.Filter.Eq(x => x.ChatId, chatId);
            await this.collection.DeleteManyAsync(filter, cancellationToken);
        }

        private static SharedMessage? MapToShared(DataMessage? dataMessage)
        {
            if (dataMessage == null)
            {
                return null;
            }

            MessageStatus status = dataMessage.Status switch
            {
                0 => MessageStatus.Sent,
                1 => MessageStatus.Delivered,
                2 => MessageStatus.Read,
                3 => MessageStatus.Failed,
                _ => MessageStatus.Sent,
            };

            return new SharedMessage
            {
                Id = dataMessage.Id ?? string.Empty,
                ChatId = dataMessage.ChatId,
                SenderId = dataMessage.SenderId,
                Content = dataMessage.Content,
                CreatedAt = dataMessage.CreatedAt,
                EditedAt = dataMessage.EditedAt,
                ReplyToId = dataMessage.ReplyToId,
                Status = status,
                IsDeleted = dataMessage.IsDeleted,
                Attachments = new List<Uchat.Shared.Models.MessageAttachment>(),
            };
        }

        private static DataMessage MapToData(SharedMessage sharedMessage)
        {
            int status = sharedMessage.Status switch
            {
                MessageStatus.Sent => 0,
                MessageStatus.Delivered => 1,
                MessageStatus.Read => 2,
                MessageStatus.Failed => 3,
                _ => 0,
            };

            return new DataMessage
            {
                Id = sharedMessage.Id,
                ChatId = sharedMessage.ChatId,
                SenderId = sharedMessage.SenderId,
                Content = sharedMessage.Content,
                Status = status,
                CreatedAt = sharedMessage.CreatedAt,
                EditedAt = sharedMessage.EditedAt,
                ReplyToId = sharedMessage.ReplyToId,
                IsDeleted = sharedMessage.IsDeleted,
            };
        }
    }
}
