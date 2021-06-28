using System;
using System.Device.Location;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram_TimeSpanBot.Configure;

namespace Telegram_TimeSpanBot
{
    internal static class Program
    {
        private static TelegramBotClient Bot { get; set; }

        private static async Task Main()
        {
            Bot = new TelegramBotClient(Settings.Token);

            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;

            var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            Bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync),
                cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => BotOnMessageReceived(update.Message),
                UpdateType.EditedMessage => BotOnMessageReceived(update.EditedMessage),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
                //UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery),
                //UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult),
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private static async Task BotOnMessageReceived(Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");

            var response = message.Type switch
            {
                MessageType.Text => ReceivedText(message),
                MessageType.Location => ReceivedLocation(message),
                _ => UnknownMessageType()
            };


            await response;
        }

        private static async Task UnknownMessageType()
        {
            return;
        }

        private static async Task ReceivedLocation(Message message)
        {
            var location = message.Location;
            Console.WriteLine(
                $"Receive message Location: {message.MessageId} '{location.Latitude}:{location.Longitude}' live period {location.LivePeriod:# sec}");
            var coord = new GeoCoordinate(location.Latitude, location.Latitude);
            var isUntilAtStartPos = await DbWorker.CheckCoord(message.MessageId, coord);
            if (!isUntilAtStartPos)
            {
                var locationTimeSpanUnit = await DbWorker.GetLocationTimeSpanUnit(message.MessageId);

                await DbWorker.AddTimeSpan(message.Chat.Id, locationTimeSpanUnit.StartTime,
                    locationTimeSpanUnit.StopTime);

                var sentMessage = await Bot.SendTextMessageAsync(message.Chat.Id,
                    $"Add time span {locationTimeSpanUnit.StartTime - locationTimeSpanUnit.StopTime:dd\\.hh\\:mm\\:ss}");
                ;
                Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
            }
        }

        private static async Task ReceivedText(Message message)
        {
            Console.WriteLine($"Receive message text: {message.Text}");
            var action = message.Text.Split(' ').First() switch
            {
                "/begin" => StartTimeSpan(message),
                "/stop" => StopTimeSpan(message),
                "/sum" => Sum(message),
                "/add" => Add(message),
                "/location" => RequestLocation(message),
                "/list" => ListOfTimeSpans(message),
                "/remove" => RemoveTimeSpan(message),
                _ => Usage(message)
            };
            var sentMessage = await action;
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
        }

        private static async Task<Message> RemoveTimeSpan(Message message)
        {
            var input = message.Text.Trim().Split(" ");
            if (input.Length < 2 || !long.TryParse(input[1].Trim(), out var id))
                return await Bot.SendTextMessageAsync(message.Chat.Id, "Wrong input. Try `/remove 20`",
                    ParseMode.Markdown);

            var isRemove = await DbWorker.RemoveTimeSpanUnit(id, message.Chat.Id);

            if (isRemove)
                return await Bot.SendTextMessageAsync(message.Chat.Id, $"Remove by id {id}");
            return await Bot.SendTextMessageAsync(message.Chat.Id, $"Not found by id {id}");
        }

        private static async Task<Message> ListOfTimeSpans(Message message)
        {
            var res = await DbWorker.GetListTimeSpanUnits(message.Chat.Id);

            string outputStr;
            if (res.Count > 0)
            {
                outputStr = "List:\n";
                foreach (var timeSpanUnit in res)
                    if (timeSpanUnit.StartTime.Day ==  timeSpanUnit.StopTime.Day)
                        outputStr +=
                            $"{timeSpanUnit.Id}) {timeSpanUnit.StartTime:F} - {timeSpanUnit.StopTime:T}\n";
                    else 
                        outputStr +=
                            $"{timeSpanUnit.Id}) {timeSpanUnit.StartTime:F} - {timeSpanUnit.StopTime:F}\n";
            }
            else
            {
                outputStr = "List is empty.";
            }
            return await Bot.SendTextMessageAsync(message.Chat.Id, outputStr);
        }

        private static async Task<Message> RequestLocation(Message message)
        {
            return await Bot.SendTextMessageAsync(message.Chat.Id,
                "Send live location",
                replyToMessageId: message.MessageId);
        }

        private static async Task<Message> Add(Message message)
        {
            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            var trimMessage = message.Text.Trim();
            if (!trimMessage.Contains(' ') || trimMessage.Split(' ').Length < 2)
                return await BedAddArgumentsMessage();

            var firstSpace = trimMessage.IndexOf(' ') + 1;
            var input = trimMessage[firstSpace..].Split(';');


            DateTime start, end;
            TimeSpan timeSpan;
            switch (input.Length)
            {
                case 1:
                    if (!TimeSpan.TryParse(input[0].Trim(), out timeSpan))
                        return await BedAddArgumentsMessage();
                    start = DateTime.Today;
                    end = start + timeSpan;
                    break;
                case 2:
                    if (!DateTime.TryParse(input[0].Trim(), out start))
                        return await BedAddArgumentsMessage();
                    if (!TimeSpan.TryParse(input[1].Trim(), out timeSpan))
                        return await BedAddArgumentsMessage();
                    end = start + timeSpan;
                    break;
                default:
                    return await BedAddArgumentsMessage();
            }

            await DbWorker.AddTimeSpan(message.Chat.Id, start, end);
            return await Bot.SendTextMessageAsync(message.Chat.Id,
                $"Add timeSpan {start:s} to {end:s}");

            async Task<Message> BedAddArgumentsMessage()
            {
                return await Bot.SendTextMessageAsync(message.Chat.Id,
                    @"Wrong input. Try `/add 4:50` or `/add 20.06.21 10:00;4:52`",
                    ParseMode.Markdown);
            }
        }

        private static async Task<Message> StartTimeSpan(Message message)
        {
            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            var timeStart = DateTime.Now;


            InlineKeyboardMarkup inlineKeyboard = new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Stop timeSpan", "/stop")
                }
            });

            var sendTextMessageAsync = await Bot.SendTextMessageAsync(message.Chat.Id,
                $"Start time span at {timeStart:s}",
                replyMarkup: inlineKeyboard);
            await DbWorker.SaveTimeStart(message.Chat.Id, sendTextMessageAsync.MessageId, timeStart);

            return sendTextMessageAsync;
        }

        private static async Task<Message> StopTimeSpan(Message message)
        {
            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            var timeStop = DateTime.Now;
            await DbWorker.SaveTimeStop(message.Chat.Id, message.MessageId, timeStop);
            var timeSpan = await DbWorker.GetTimeSpan(message.Chat.Id, message.MessageId);
            return await Bot.SendTextMessageAsync(message.Chat.Id,
                $"Stop time span at {timeStop:s}\n" +
                $"TimeSpan is {timeSpan:dd\\.hh\\:mm\\:ss}");
        }

        private static async Task<Message> Sum(Message message)
        {
            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            var trimMessage = message.Text.Trim();
            if (!trimMessage.Contains(' ') || trimMessage.Split(' ').Length < 2)
                return await BedSumArgumentsMessage();

            var firstSpace = trimMessage.IndexOf(' ');
            var input = trimMessage[firstSpace..].Split(';');


            DateTime start, end;
            switch (input.Length)
            {
                case 1:
                    if (!DateTime.TryParse(input[0], out start))
                        return await BedSumArgumentsMessage();
                    end = DateTime.Now;
                    break;
                case 2:
                    if (!DateTime.TryParse(input[0], out start))
                        return await BedSumArgumentsMessage();
                    if (!DateTime.TryParse(input[1], out end))
                        return await BedSumArgumentsMessage();
                    break;
                default:
                    return await BedSumArgumentsMessage();
            }

            var totalTimeSpan = await DbWorker.GetTimeSpanAtInterval(start, end);
            return await Bot.SendTextMessageAsync(message.Chat.Id,
                $"Total timeSpan {totalTimeSpan:dd\\.hh\\:mm\\:ss}");

            async Task<Message> BedSumArgumentsMessage()
            {
                return await Bot.SendTextMessageAsync(message.Chat.Id,
                    @"Wrong input. Try `/sum 20.06.21 10:00` or `/sum 20.06.21 10:00;25.06.21 21:00`",
                    ParseMode.Markdown);
            }
        }

        private static async Task<Message> Usage(Message message)
        {
            var commands = await Bot.GetMyCommandsAsync();

            var commandsStr = commands.Aggregate("",
                (current, botCommand) => current + $"/{botCommand.Command} - {botCommand.Description}\n");

            return await Bot.SendTextMessageAsync(message.Chat.Id,
                commandsStr,
                replyMarkup: new ReplyKeyboardRemove());
        }

        private static async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            await Bot.AnswerCallbackQueryAsync(callbackQuery.Id,
                $"Received {callbackQuery.Data}");

            var action = callbackQuery.Data switch
            {
                "/stop" => StopTimeSpan(callbackQuery.Message),
                _ => Usage(callbackQuery.Message)
            };
            var sentMessage = await action;
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
        }

        private static Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
}