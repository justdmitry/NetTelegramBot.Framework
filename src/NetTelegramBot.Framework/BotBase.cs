namespace NetTelegramBot.Framework
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NetTelegramBotApi;
    using NetTelegramBotApi.Requests;
    using NetTelegramBotApi.Types;
    using Storage;

    public abstract class BotBase
    {
        private readonly ILogger logger;

        private readonly IStorageService storageService;

        private readonly ICommandParser commandParser;

        private readonly Func<Type, ICommandHandler> commandHandlerFactory;

        private readonly TelegramBot botApi;

        protected BotBase(
            ILogger logger,
            IStorageService storageService,
            ICommandParser commandParser,
            Func<Type, ICommandHandler> commandHandlerFactory,
            string token)
        {
            this.logger = logger;
            this.storageService = storageService;
            this.commandParser = commandParser;
            this.commandHandlerFactory = commandHandlerFactory;
            this.botApi = new TelegramBot(token);

            OnStart();
        }

        /// <summary>
        /// Telegram Id for this Bot
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Telegram Username for this Bot
        /// </summary>
        public string Username { get; private set; }

        public long LastOffset { get; private set; }

        public Dictionary<string, Type> RegisteredCommandHandlers { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public virtual async Task<T> SendAsync<T>(RequestBase<T> message)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"Sending {message.GetType().Name}...");
            }

            var reply = await botApi.MakeRequestAsync(message);

            if (reply is Message replyMessage)
            {
                await storageService.SaveLogAsync(Id, replyMessage);
            }

            return reply;
        }

        /// <summary>
        /// Processes incoming webhook request (deserializes from stream and calls <see cref="ProcessUpdateSafelyAsync(Update)"/>)
        /// </summary>
        /// <param name="stream">Request stream (with Updates)</param>
        /// <returns>Awaitable Task</returns>
        public virtual async Task ProcessIncomingWebhookAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var text = await reader.ReadToEndAsync();
                var update = botApi.DeserializeUpdate(text);
                await ProcessUpdateAsync(update);
            }
        }

        /// <summary>
        /// Processes incoming <see cref="Update"/>
        /// </summary>
        /// <param name="update"><see cref="Update"/> to process</param>
        /// <returns>Awaitable Task</returns>
        public virtual async Task ProcessUpdateAsync(Update update)
        {
            try
            {
                switch (true)
                {
                    case true when update.Message != null:
                        await OnMessageAsync(update.Message);
                        break;
                    case true when update.EditedMessage != null:
                        await OnEditedMessageAsync(update.EditedMessage);
                        break;
                    case true when update.ChannelPost != null:
                        await OnChannelPostAsync(update.ChannelPost);
                        break;
                    case true when update.EditedChannelPost != null:
                        await OnEditedChannelPostAsync(update.EditedChannelPost);
                        break;
                    case true when update.InlineQuery != null:
                        await OnInlineQueryAsync(update.InlineQuery);
                        break;
                    case true when update.ChosenInlineResult != null:
                        await OnChosenInlineResultAsync(update.ChosenInlineResult);
                        break;
                    case true when update.CallbackQuery != null:
                        await OnCallbackQueryAsync(update.CallbackQuery);
                        break;
                    default:
                        logger.LogWarning("Unknown update content, ignored");
                        break;
                }
            }
            catch (Exception ex)
            {
                if (await HandleException(update, ex))
                {
                    logger.LogInformation($"Ignoring {ex.GetType().Name}, override HandleRequest() to control this.");
                }
                else
                {
                    logger.LogError(0, ex, $"Unhandler exception {ex.GetType().Name}, override HandleRequest() to control this.");
                    throw;
                }
            }

            LastOffset = update.UpdateId;
        }

        public virtual Task<bool> HandleException(Update update, Exception exception)
        {
            if (exception is BotRequestException)
            {
                logger.LogWarning(0, exception, $"Ignoring {nameof(BotRequestException)} (some SendAsync failed)");
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public virtual async Task OnMessageAsync(Message message)
        {
            await storageService.SaveLogAsync(Id, message);

            var command = commandParser.TryParse(message.Text);
            if (command != null)
            {
                await OnCommandMessageAsync(message, command);
            }
            else
            {
                await OnTextMessageAsync(message);
            }
        }

        public virtual Task OnCommandMessageAsync(Message message, ICommand command)
        {
            if (RegisteredCommandHandlers.TryGetValue(command.Name, out Type handlerType))
            {
                var handler = commandHandlerFactory(handlerType);
                return handler.ExecuteAsync(command, this, message);
            }
            else
            {
                return OnUnknownCommandMessageAsync(message, command);
            }
        }

        public virtual Task OnUnknownCommandMessageAsync(Message message, ICommand command)
        {
            return SendAsync(new SendMessage(message.Chat.Id, "Unknown command: " + command.Name));
        }

        public virtual Task OnTextMessageAsync(Message message)
        {
            logger.LogInformation("Incoming text message (override OnTextMessageAsync() to handle):" + Environment.NewLine + message.Text);
            return Task.CompletedTask;
        }

        public virtual async Task OnEditedMessageAsync(Message message)
        {
            await storageService.SaveLogAsync(Id, message);

            logger.LogInformation("Edited message (override OnEditedMessageAsync() to handle):" + Environment.NewLine + message.Text);
        }

        public virtual async Task OnChannelPostAsync(Message message)
        {
            await storageService.SaveLogAsync(Id, message);

            logger.LogInformation("Channel post (override OnChannelPostAsync() to handle):" + Environment.NewLine + message.Text);
        }

        public virtual async Task OnEditedChannelPostAsync(Message message)
        {
            await storageService.SaveLogAsync(Id, message);

            logger.LogInformation("Edited Channel post (override OnEditedChannelPostAsync() to handle):" + Environment.NewLine + message.Text);
        }

        public virtual async Task OnInlineQueryAsync(InlineQuery inlineQuery)
        {
            await storageService.SaveLogAsync(Id, inlineQuery);

            logger.LogInformation("Inline query (override OnInlineQueryAsync() to handle):" + Environment.NewLine + inlineQuery.Query);
        }

        public virtual async Task OnChosenInlineResultAsync(ChosenInlineResult chosenInlineResult)
        {
            await storageService.SaveLogAsync(Id, chosenInlineResult);

            logger.LogInformation("Choosen inline result (override OnChosenInlineResultAsync() to handle):" + Environment.NewLine + chosenInlineResult.ResultId);
        }

        public virtual async Task OnCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            await storageService.SaveLogAsync(Id, callbackQuery);

            logger.LogInformation("Callback query (override OnCallbackQueryAsync() to handle):" + Environment.NewLine + callbackQuery.Data);
        }

        /// <summary>
        /// Sends 'getMe' request, fills <see cref="Id"/> and <see cref="Username"/> from response
        /// </summary>
        protected virtual void OnStart()
        {
            var me = SendAsync(new GetMe()).Result;
            if (me == null)
            {
                throw new Exception("Can't get bot user info. Check API Token");
            }

            Id = me.Id;
            Username = me.Username;

            logger.LogInformation($"Bot info refreshed: {Username} (id = {Id})");
        }
    }
}
