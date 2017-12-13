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

        public virtual async Task ProcessAsync(Update update)
        {
            try
            {
                var msg = update.Message;
                if (msg != null)
                {
                    await storageService.SaveMessageAsync(msg);

                    var command = commandParser.TryParse(msg.Text);
                    if (command != null)
                    {
                        await OnCommandAsync(msg, command);
                    }
                    else
                    {
                        await OnMessageAsync(msg);
                    }
                }
            }
            catch (BotRequestException ex)
            {
                // Catch BotRequestException and ignore it.
                // Otherwise, incoming mesage will be processed again and again...
                // To avoid - just catch exception yourself (inside OnCommand/OnMessage),
                //     put inside other (AggregateException for example) and re-throw
                logger.LogError(0, ex, "SendAsync-related error during message processing. Ignored.");
            }

            LastOffset = update.UpdateId;
        }

        public virtual Task<T> SendAsync<T>(RequestBase<T> message)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"Sending {message.GetType().Name}...");
            }

            return botApi.MakeRequestAsync(message);
        }

        public virtual async Task ProcessIncomingWebhookAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var text = await reader.ReadToEndAsync();
                var update = botApi.DeserializeUpdate(text);
                await ProcessAsync(update);
            }
        }

        public virtual Task OnCommandAsync(Message message, ICommand command)
        {
            if (RegisteredCommandHandlers.TryGetValue(command.Name, out Type handlerType))
            {
                var handler = commandHandlerFactory(handlerType);
                return handler.Execute(command, this, message);
            }
            else
            {
                return OnUnknownCommandAsync(message, command);
            }
        }

        public abstract Task OnUnknownCommandAsync(Message message, ICommand command);

        public abstract Task OnMessageAsync(Message message);

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
