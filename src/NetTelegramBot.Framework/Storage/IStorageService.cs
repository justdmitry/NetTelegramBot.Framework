namespace NetTelegramBot.Framework.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NetTelegramBotApi.Types;

    public interface IStorageService
    {
        Task SaveLogAsync(long botId, Message message);

        Task SaveLogAsync(long botId, InlineQuery inlineQuery);

        Task SaveLogAsync(long botId, ChosenInlineResult chosenInlineResult);

        Task SaveLogAsync(long botId, CallbackQuery callbackQuery);

        Task SaveContextAsync<TContext>(long botId, long userOrChatId, TContext userContext)
            where TContext : class, new();

        Task SaveContextAsync<TContext>(long botId, User user, TContext userContext)
            where TContext : class, new();

        Task SaveContextAsync<TContext>(long botId, Chat chat, TContext userContext)
            where TContext : class, new();

        Task<TContext> LoadContextAsync<TContext>(long botId, long userOrChatId)
            where TContext : class, new();

        Task<Tuple<List<TContext>, ISegmentedQueryContinuationToken>>
            LoadAllContextsAsync<TContext>(long botId, ISegmentedQueryContinuationToken token = null)
            where TContext : class, new();

        Task<Tuple<List<TContext>, ISegmentedQueryContinuationToken>>
            LoadAllContextsAsync<TContext>(long botId, ChatType chatType, ISegmentedQueryContinuationToken token = null)
            where TContext : class, new();
    }
}
