using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace jVoteBot
{
    class Program
    {
        const int PrivateChatID = 0x28d07dc;
        public static ManualResetEvent ShouldQuit = new ManualResetEvent(false);
        private static PollManager pollManager = new PollManager();
        static TelegramBotClient bot = new TelegramBotClient(File.ReadAllText(Path.Combine(GetDirectory(), "TelegramApiKey.txt")).Trim());
        internal static Telegram.Bot.Types.User BotInfo = null;

        static string BotUsername => BotInfo.Username;
        
        public static string GetDirectory()
        {
            if (IsRunningOnMono())
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            else
                return Environment.CurrentDirectory;
        }

        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        static async Task Main(string[] args)
        {
            BotInfo = await bot.GetMeAsync();
            bot.OnInlineQuery += PollInlineQuery;
            bot.OnMessage += PollMessage;
            bot.OnCallbackQuery += PollAnswerCallback;
            bot.OnReceiveError += Bot_OnReceiveError;
            bot.OnReceiveGeneralError += Bot_OnReceiveGeneralError;
            await bot.SendTextMessageAsync(PrivateChatID, "Up!");
            Thread.Sleep(10000);
            bot.StartReceiving(new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.CallbackQuery, UpdateType.EditedMessage });
            ShouldQuit.WaitOne();
            Thread.Sleep(5000);
            await bot.SendTextMessageAsync(PrivateChatID, "Down!");
            bot.StopReceiving();
            bot = null;
            pollManager = null;
            Thread.Sleep(5000);
        }

        private static void Bot_OnReceiveGeneralError(object sender, Telegram.Bot.Args.ReceiveGeneralErrorEventArgs e)
        {
            if (!(e.Exception is System.Net.Http.HttpRequestException))
            {
                bot.SendTextMessageAsync(PrivateChatID, e.Exception.ToString());
                bShouldIgnore = true;
            }
        }

        private static void Bot_OnReceiveError(object sender, Telegram.Bot.Args.ReceiveErrorEventArgs e)
        {
            bot.SendTextMessageAsync(PrivateChatID, e.ApiRequestException.ToString());
        }

        private static void PollAnswerCallback(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            if (bShouldIgnore)
            {
                bShouldIgnore = false;
                return;
            }
            var query = e.CallbackQuery;
            var data = query.Data.Split(new[] { '|' });
            if (!string.IsNullOrEmpty(query.InlineMessageId))
            {
                pollManager.SetUser(query.From.Id, UserToString(query.From));
                if(data.Length == 2 && long.TryParse(data[0], out var pId))
                {
                    if (pollManager.GetPoll(pId) is PollData.Poll poll)
                    {
                        poll.AddQuery(query.InlineMessageId);
                        pollManager.Update(poll);
                        if (data[1] == "custom")
                            bot.AnswerCallbackQueryAsync(query.Id, url: $"t.me/{BotUsername}?start={pId}");
                        else if (int.TryParse(data[1], out var oId))
                        {
                            poll.AddVote(query.From.Id, oId);
                            pollManager.Update(poll);
                            bot.EditMessageTextAsync(query.InlineMessageId, BuildPoolMessage(pId, poll.Name), replyMarkup: new InlineKeyboardMarkup(GetPollButtons(poll)));
                            bot.AnswerCallbackQueryAsync(query.Id);
                        }
                    }
                }
            }
            else
            {
                var msg = query.Message;
                if (query.Data == "pollList")
                {
                    var buttons = pollManager.GetPollsByUser(query.From.Id)
                        .Select(p => InlineKeyboardButton.WithCallbackData(p.Name, p.Id.ToString()))
                        .Partition(BOT_MaxButtonLen)
                        .ToArray();
                    bot.EditMessageTextAsync(msg.Chat.Id, msg.MessageId, BOT_PollList, replyMarkup: new InlineKeyboardMarkup(buttons));
                }
                else
                {
                    if (data.Length > 1)
                    {
                        if (long.TryParse(data[1], out var result))
                        {
                            if (data[0] == "pollDelete")
                            {
                                pollManager.DeletePoll(result, query.From.Id);
                                bot.DeleteMessageAsync(msg.Chat.Id, msg.MessageId);
                            }
                            else if (data[0] == "pollCustom" && pollManager.GetPoll(result) is PollData.Poll poll)
                            {
                                poll.CanAddOptions = !poll.CanAddOptions;
                                pollManager.Update(poll);
                            }
                        }
                    }
                    else
                    {
                        if (long.TryParse(data[1], out var result))
                        {
                            var test = new[]
                            {
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("Toggle Custom Options", "pollCustom|" + data[0])
                                },
                                new []
                                {
                                    InlineKeyboardButton.WithSwitchInlineQuery("Share link", pollManager.GetPoll(result)?.Name)
                                },
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("Delete", "pollDelete|" + data[0]),
                                    InlineKeyboardButton.WithCallbackData("Go Back", "pollList")
                                }
                            };
                            bot.EditMessageTextAsync(msg.Chat.Id, msg.MessageId, BOT_Placeholder, replyMarkup: new InlineKeyboardMarkup(test));
                        }
                    }
                }
                bot.AnswerCallbackQueryAsync(query.Id);
            }
        }

        private static void PollMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (bShouldIgnore)
            {
                bShouldIgnore = false;
                return;
            }
            var msg = e.Message;
            if(msg.Chat.Type == ChatType.Private)
            {
                if (msg.EntityValues != null && msg.Entities != null)
                {
                    var entVals = msg.EntityValues.ToList();
                    foreach (var i in Enumerable.Range(0, msg.Entities.Count()))
                    {
                        var ent = entVals[i];
                        var entity = msg.Entities[i];
                        if (msg.Entities[i].Type == MessageEntityType.BotCommand)
                        {
                            switch (ent)
                            {
                                case "/new":
                                case "/start":
                                    // check if payload != string.Empty, if true then user tries to add option to the poll with Id == long.Parse(Payload)
                                    var payload = msg.Text.Replace(ent, null).Trim();
                                    if (payload == string.Empty)
                                    {
                                        // Get current pool, if not exists then 
                                        var poll = pollManager.GetCurrentSetupPoll(msg.From.Id);
                                        if (poll == null)
                                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_PollName, replyMarkup: new ForceReplyMarkup());
                                        else
                                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_FinishLast);
                                    }
                                    else
                                    {
                                        if (long.TryParse(payload, out var pId) && pollManager.GetPoll(pId) is PollData.Poll poll && poll.CanAddOptions)
                                        {
                                            bot.SendTextMessageAsync(msg.Chat.Id, string.Format(BOT_CustomOption, poll.Id), replyMarkup: new ForceReplyMarkup());
                                        }
                                    }
                                    break;
                                case "/list":
                                    var buttons = pollManager.GetPollsByUser(msg.From.Id).Select(p => new[] { InlineKeyboardButton.WithCallbackData(p.Name, p.Id.ToString()) });
                                    bot.SendTextMessageAsync(msg.Chat.Id, BOT_PollList, replyMarkup: new InlineKeyboardMarkup(buttons.ToArray()));
                                    break;
                                case "/hcf": // halt & catch fire
                                    if (msg.From.Id == PrivateChatID)
                                    {
                                        ShouldQuit.Set();
                                        return;
                                    }
                                    break;
                                default:
                                    bot.SendTextMessageAsync(msg.Chat.Id, "Unknown entity: " + ent, replyToMessageId: msg.MessageId);
                                    break;
                            }
                        }
                    }
                }
                var reply = msg.ReplyToMessage;
                if (reply != null)
                {
                    if (pollManager.GetCurrentSetupPoll(msg.From.Id) is PollData.Poll poll)
                    {
                        if (reply.Text == BOT_GetOption2 && msg.Text == "Finish")
                        {
                            var name = poll.Name;
                            poll.IsSetUp = true;
                            // send share links
                            var share = new[] { new[] { InlineKeyboardButton.WithCallbackData("Toggle custom options", $"pollCustom|{poll.Id}") },
                                                new[] { InlineKeyboardButton.WithSwitchInlineQuery("Share poll", name) } };
                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_ShareLink, replyMarkup: new InlineKeyboardMarkup(share));
                        }
                        else
                        {
                            poll.AddOption(msg.From.Id, msg.Text);
                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_GetOption2.Replace("Finish", "`Finish`"), ParseMode.Markdown, replyMarkup: new ForceReplyMarkup());
                        }
                        pollManager.Update(poll);
                    }
                    else if (reply.Text == BOT_PollName)
                    {
                        pollManager.AddPoll(msg.From.Id, msg.Text);
                        bot.SendTextMessageAsync(msg.Chat.Id, BOT_GetOption, replyMarkup: new ForceReplyMarkup());
                    }
                    else if (reply.Text.Split(new[] { '\'' }, StringSplitOptions.RemoveEmptyEntries) is string[] parts && parts.Length > 1)
                    {
                        if (long.TryParse(parts[1], out var pId) && pollManager.GetPoll(pId) is PollData.Poll custom)
                        {
                            if(custom.AddOption(msg.From.Id, msg.Text))
                                custom.AddVote(msg.From.Id, custom.Options.Count - 1);

                            pollManager.Update(custom);
                            custom.InlineQueries.ForEach(id =>  bot.EditMessageTextAsync(id, BuildPoolMessage(pId, custom.Name), replyMarkup: new InlineKeyboardMarkup(GetPollButtons(custom))));
                        }
                    }
                }
            }
        }

        private static void PollInlineQuery(object sender, Telegram.Bot.Args.InlineQueryEventArgs e)
        {
            if (bShouldIgnore)
            {
                bShouldIgnore = false;
                return;
            }
            var query = e.InlineQuery;
            var UserId = query.From.Id;
            var text = query.Query;

            var req = pollManager.GetPollsByUser(UserId)
                .Where(p => p.Name.Contains(text))
                .Select(p =>
                {
                    List<InlineKeyboardButton[]> opt = GetPollButtons(p);
                    var message = BuildPoolMessage(p.Id, p.Name);
                    return new InlineQueryResultArticle
                    {
                        Id = p.Id.ToString(),
                        Title = p.Name,
                        Description = "Send pool to chat",
                        ReplyMarkup = new InlineKeyboardMarkup(opt),
                        InputMessageContent = new InputTextMessageContent
                        {
                            MessageText = message
                        }
                    };
                });
            bot.AnswerInlineQueryAsync(query.Id, req, 0);
        }

        private static List<InlineKeyboardButton[]> GetPollButtons(PollData.Poll p)
        {
            var opt = BuildPoolButtons(p.Id).ToList();
            if (p.CanAddOptions)
                opt.Add(new[] { InlineKeyboardButton.WithCallbackData("Add New Option", $"{p.Id}|custom") });
            return opt;
        }

        private static string BuildPoolMessage(long PollId, string PollName)
        {
            var options = pollManager.GetPoll(PollId).Options;
            var message = PollName + Environment.NewLine;
            for (int i = 0; i < options.Count(); i++)
            {
                var opt = options[i];
                var optVotes = opt.Votes;
                if (optVotes.Any())
                {
                    message += $"{i}. {opt.Text}: " 
                        + Environment.NewLine 
                        + string.Join(", ", optVotes.Select(v => pollManager.GetUsername(v) ?? "Unknown Username"))
                        + Environment.NewLine;
                }
            }
            return message;
        }

        private static IEnumerable<InlineKeyboardButton[]> BuildPoolButtons(long PollId)
        {
            int i = 0;
            return pollManager
                .GetPoll(PollId)
                .Options
                .Select(o => InlineKeyboardButton.WithCallbackData(o.Text, $"{PollId}|{i++}"))
                .Partition(BOT_MaxButtonLen);
        }

        public static string UserToString(Telegram.Bot.Types.User chat)
        {
            if (string.IsNullOrEmpty(chat.Username))
            {
                if (string.IsNullOrEmpty(chat.LastName))
                    return chat.FirstName;
                else
                    return $"{chat.FirstName} {chat.LastName}";
            }
            else
                return $"@{chat.Username}";
        }

        public static async Task<string> GetChatUserNameAsync(int UserId)
        {
            string retn = string.Empty;
            var chat = await bot.GetChatMemberAsync(UserId, UserId);
            return UserToString(chat.User);
        }

        public static string GetChatUserName(int UserId)
        {
            try
            {
                var ret = GetChatUserNameAsync(UserId);
                ret.Wait();
                return ret.Result;
            }
            catch { }
            return string.Empty;
        }

        const string BOT_PollName = "What's the poll name?";
        const string BOT_FinishLast = "Finish last pool first...";
        const string BOT_CustomOption = "What would the option text for the poll '{0}' be?"; // TODO: Replace shit
        const string BOT_Placeholder = "Placeholder text."; // TODO: Replace shit
        const string BOT_GetOption = "Ok, now you can send me poll options.";
        const string BOT_GetOption2 = "Keep them coming, I'll stop listening when you send the text Finish.";
        const string BOT_ShareLink = "Here's the share link you can use to show the poll to people easily:";
        const string BOT_PollList = "Here's the list of all the polls you made:";
        const int BOT_MaxButtonLen = 20;
        static bool bShouldIgnore = false;
    }
}
