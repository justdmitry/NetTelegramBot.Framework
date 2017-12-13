namespace NetTelegramBot.Framework
{
    using System;
    using System.Threading.Tasks;
    using NetTelegramBotApi.Types;

    public interface ICommandHandler
    {
        Task ExecuteAsync(ICommand command, BotBase bot, Message message);
    }
}
