using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.ReplyMarkups;

namespace jVoteBot
{
    class Program
    {
        const int PrivateChatID = 0x28d07dc;
        public static ManualResetEvent ShouldQuit = new ManualResetEvent(false);
        private static PollManager pollManager = new PollManager();
        static TelegramBotClient bot = new TelegramBotClient(File.ReadAllText(Path.Combine(GetDirectory(), "TelegramApiKey.txt")));

        static public bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }
        public static string GetDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        }

        static void Main(string[] args)
        {
            bot.OnInlineQuery += PoolInlineQuery;
            bot.OnMessage += PoolMessageSetup;
            bot.OnCallbackQuery += PoolAnswerCallback;
            bot.OnReceiveError += Bot_OnReceiveError;
            bot.OnReceiveGeneralError += Bot_OnReceiveGeneralError;
            bot.StartReceiving(new[] { UpdateType.MessageUpdate, UpdateType.InlineQueryUpdate, UpdateType.CallbackQueryUpdate });
            ShouldQuit.WaitOne();
            Thread.Sleep(5000);
            bot.StopReceiving();
            bot = null;
            pollManager = null;
            Thread.Sleep(5000);
        }

        private static void Bot_OnReceiveGeneralError(object sender, Telegram.Bot.Args.ReceiveGeneralErrorEventArgs e)
        {
            bot.SendTextMessageAsync(PrivateChatID, e.Exception.ToString());
            bShouldIgnore = true;
        }

        private static void Bot_OnReceiveError(object sender, Telegram.Bot.Args.ReceiveErrorEventArgs e)
        {
            bot.SendTextMessageAsync(PrivateChatID, e.ApiRequestException.ToString());
        }

        private static void PoolAnswerCallback(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
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
                if(data.Length == 2)
                {
                    if(long.TryParse(data[0], out long pId) && long.TryParse(data[1], out long oId))
                    {
                        var poll = pollManager.GetPoll(pId);
                        if (poll != null)
                        {
                            pollManager.AddDeleteVote(pId, query.From, oId);
                            bot.EditInlineMessageTextAsync(query.InlineMessageId, BuildPoolMessage(pId, poll.Name), replyMarkup: new InlineKeyboardMarkup(BuildPoolButtons(pId).ToArray()));
                        }
                    }
                }
                bot.AnswerCallbackQueryAsync(query.InlineMessageId);
            }
            else
            {
                var msg = query.Message;
                if (data[0] == "pollDelete")
                {
                    var id = data[1];
                    if (long.TryParse(id, out long result))
                    {
                        pollManager.DeletePoll(result, query.From.Id);
                        bot.DeleteMessageAsync(msg.Chat.Id, msg.MessageId);
                    }
                }
                else if (query.Data == "pollList")
                {
                    var buttons = pollManager.GetPollsByUser(query.From.Id).Select(p => new[]
                    {
                        new InlineKeyboardCallbackButton(p.Name, p.Id.ToString())
                    });
                    bot.EditMessageTextAsync(msg.Chat.Id, msg.MessageId, BOT_PollList, replyMarkup: new InlineKeyboardMarkup() { InlineKeyboard = buttons.ToArray() });
                }
                else
                {
                    bot.EditMessageTextAsync(msg.Chat.Id, msg.MessageId, "Do you want to delete this pool?", replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardCallbackButton("Yes, delete", "pollDelete|" + data[0]),
                        new InlineKeyboardCallbackButton("No", "pollList")
                    }));
                }
                bot.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
            }
        }

        private static void PoolMessageSetup(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (bShouldIgnore)
            {
                bShouldIgnore = false;
                return;
            }
            var msg = e.Message;
            if(msg.Chat.Type == ChatType.Private)
            {
                foreach (var i in Enumerable.Range(0, msg.Entities.Count))
                {
                    var ent = msg.EntityValues[i];
                    if (msg.Entities[i].Type == MessageEntityType.BotCommand)
                    {
                        switch (ent)
                        {
                            case "/new":
                            case "/start":
                                // Get current pool, if not exists then 
                                var poll = pollManager.GetCurrentSetupPoll(msg.From.Id);
                                if (poll == null)
                                    bot.SendTextMessageAsync(msg.Chat.Id, BOT_PollName, replyMarkup: new ForceReply() { Force = true });
                                else
                                    bot.SendTextMessageAsync(msg.Chat.Id, "Finish last pool first...");
                                break;
                            case "/list":
                                var buttons = pollManager.GetPollsByUser(msg.From.Id).Select(p => new[] { new InlineKeyboardCallbackButton(p.Name, p.Id.ToString())});
                                bot.SendTextMessageAsync(msg.Chat.Id, BOT_PollList, replyMarkup: new InlineKeyboardMarkup() { InlineKeyboard = buttons.ToArray() });
                                break;
                            case "/devbreakquit":
                                if (msg.From.Id == PrivateChatID)
                                {
                                    ShouldQuit.Set();
                                    return;
                                }
                                break;
                            case "/raw":
                                if (msg.From.Id == PrivateChatID)
                                {
                                    var x = msg.Entities[i];
                                    var offset = x.Offset + x.Length;
                                    string query = "";
                                    if (msg.Entities.Count == i + 1)
                                    {
                                        query = msg.Text.Substring(offset);
                                    }
                                    else
                                    {
                                        var y = msg.Entities[i+1];
                                        query = msg.Text.Substring(offset, y.Offset - offset);
                                    }
                                    query = query.Trim();
                                    if (!query.EndsWith(";"))
                                        query += ";";

                                    var reader = pollManager.RawQuery(query);
                                    string recstr = "|";
                                    foreach (var column in Enumerable.Range(0, reader.FieldCount))
                                    {
                                        recstr += reader.GetName(column) + '|';
                                    }
                                    recstr += Environment.NewLine;
                                    foreach (var data in reader)
                                    {
                                        var record = data as DbDataRecord;
                                        recstr += '|';
                                        foreach (var recordId in Enumerable.Range(0, record.FieldCount))
                                        {
                                            recstr += record[recordId].ToString() + '|';
                                        }
                                        recstr += Environment.NewLine;
                                    }
                                    bot.SendTextMessageAsync(msg.Chat.Id, recstr);
                                }
                                break;
                            default:
                                bot.SendTextMessageAsync(msg.Chat.Id, "Unknown entity: " + ent, replyToMessageId: msg.MessageId);
                                break;
                        }
                    }
                }
                var reply = msg.ReplyToMessage;
                if (reply != null)
                {
                    if (reply.Text == BOT_PollName)
                    {
                        if (pollManager.GetCurrentSetupPoll(msg.From.Id) == null)
                        {
                            pollManager.AddPoll(msg.From.Id, msg.Text);
                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_GetOption, replyMarkup: new ForceReply() { Force = true });
                        }
                    }
                    else if (reply.Text == BOT_GetOption2 && msg.Text == "Finish")
                    {
                        var name = pollManager.SetPoolFinished(msg.From.Id);
                        // send share links
                        var share = new[] { new[] { new InlineKeyboardSwitchInlineQueryButton("Share poll", name) } };
                        bot.SendTextMessageAsync(msg.Chat.Id, "Here's the share link you can use to show the poll to people:", replyMarkup: new InlineKeyboardMarkup(share));
                    }
                    else
                    {
                        pollManager.AddPollOption(pollManager.GetCurrentSetupPoll(msg.From.Id).Id, msg.Text);
                        bot.SendTextMessageAsync(msg.Chat.Id, BOT_GetOption2, replyMarkup: new ForceReply() { Force = true });
                    }
                }
            }
        }

        private static void PoolInlineQuery(object sender, Telegram.Bot.Args.InlineQueryEventArgs e)
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
                var opt = BuildPoolButtons(p.Id);
                var message = BuildPoolMessage(p.Id, p.Name);
                return new InlineQueryResultArticle()
                {
                    Id = p.Id.ToString(),
                    Title = p.Name,
                    Description = "Send pool to chat",
                    ReplyMarkup = new InlineKeyboardMarkup(opt.ToArray()),
                    InputMessageContent = new InputTextMessageContent()
                    {
                        MessageText = message
                    }
                };
            }).ToArray();
            bot.AnswerInlineQueryAsync(query.Id, req, 0);
        }

        private static string BuildPoolMessage(long PollId, string PollName)
        {
            var options = pollManager.GetPollOptions(PollId);
            var votes = pollManager.GetPollVotes(PollId);
            var message = PollName + Environment.NewLine;
            foreach (var opt in options)
            {
                var optVotes = votes.Where(v => v.OptId == opt.Id);
                if (optVotes.Any())
                {
                    message += $"{opt.Id}. {opt.Text}: " 
                        + Environment.NewLine 
                        + string.Join(", ", optVotes.Select(v => pollManager.GetUsername(v.UserId) ?? "Unknown Username"))
                        + Environment.NewLine;
                }
            }
            return message;
        }

        private static IEnumerable<InlineKeyboardButton[]> BuildPoolButtons(long PollId)
        {
            var options = pollManager.GetPollOptions(PollId);
            return options.Select(o => new InlineKeyboardCallbackButton(o.Text, $"{PollId}|{o.Id}")).Partition(BOT_MaxButtonLen);
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
            var me = await bot.GetMeAsync();
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
        const string BOT_GetOption = "Ok, now you can send me poll options.";
        const string BOT_GetOption2 = "Keep them coming, I'll stop listening when you send the text `Finish`.";
        const string BOT_PollList = "Here's the list of all the polls you made:";
        const int BOT_MaxButtonLen = 20;
        static bool bShouldIgnore = false;
    }
}
