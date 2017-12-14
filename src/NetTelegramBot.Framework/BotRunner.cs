namespace NetTelegramBot.Framework
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NetTelegramBotApi.Requests;
    using RecurrentTasks;

    public class BotRunner<T> : IRunnable
        where T : BotBase
    {
        private T bot;

        public BotRunner(T bot)
        {
            this.bot = bot;
        }

        public void Run(ITask currentTask, CancellationToken cancellationToken)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        public async Task RunAsync()
        {
            while (true)
            {
                var updates = await bot.SendAsync(new GetUpdates { Offset = bot.LastOffset + 1 });

                if (updates == null || updates.Length == 0)
                {
                    break;
                }

                foreach (var update in updates)
                {
                    await bot.ProcessUpdateAsync(update);
                }
            }
        }
    }
}
