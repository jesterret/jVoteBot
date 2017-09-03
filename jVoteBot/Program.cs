using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineKeyboardButtons;
using System.Data.SqlClient;
using System.Data.Common;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;

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
            bot.StartReceiving(new[] { UpdateType.MessageUpdate, UpdateType.InlineQueryUpdate, UpdateType.CallbackQueryUpdate });
            ShouldQuit.WaitOne();
            bot.StopReceiving();
            bot = null;
            pollManager = null;
            Thread.Sleep(1000);
        }

        private static void PoolAnswerCallback(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            var query = e.CallbackQuery;
            var data = query.Data.Split(new[] { '|' });
            if (!string.IsNullOrEmpty(query.InlineMessageId))
            {
                if(data.Length == 2)
                {
                    if(int.TryParse(data[0], out int pId) && int.TryParse(data[1], out int oId))
                    {
                        var poll = pollManager.GetPoll(pId);
                        if (poll != null)
                        {
                            pollManager.AddDeleteVote(pId, query.From.Id, oId);
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
                    if (int.TryParse(id, out int result))
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
            var msg = e.Message;
            if(msg.Chat.Type == ChatType.Private)
            {
                foreach(var ent in msg.EntityValues)
                {
                    if(ent[0] == '@')
                        continue;

                    switch(ent)
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
                            var buttons = pollManager.GetPollsByUser(msg.From.Id).Select(p => new[]
                            {
                                new InlineKeyboardCallbackButton(p.Name, p.Id.ToString())
                            });
                            bot.SendTextMessageAsync(msg.Chat.Id, BOT_PollList, replyMarkup: new InlineKeyboardMarkup() { InlineKeyboard = buttons.ToArray() });
                            break;
                        case "/devbreakquit":
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
                var reply = msg.ReplyToMessage;
                if (reply != null)
                {
                    if (reply.Text == BOT_PollName)
                    {
                        pollManager.AddPoll(msg.From.Id, msg.Text);
                        bot.SendTextMessageAsync(msg.Chat.Id, BOT_GetOption, replyMarkup: new ForceReply() { Force = true });
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
            var query = e.InlineQuery;
            var UserId = query.From.Id;
            var text = query.Query;

            var req = pollManager.GetPollsByUser(UserId).Select(p =>
            {
                var opt = BuildPoolButtons(p.Id);
                var message = BuildPoolMessage(p.Id, p.Name);
                return new InlineQueryResultArticle()
                {
                    Id = p.Id.ToString(),
                    Title = p.Name,
                    Description = p.Description,
                    ReplyMarkup = new InlineKeyboardMarkup(opt.ToArray()),
                    InputMessageContent = new InputTextMessageContent()
                    {
                        MessageText = message
                    }
                };
            }).ToArray();
           bot.AnswerInlineQueryAsync(query.Id, req, 0);
        }

        private static string BuildPoolMessage(int PollId, string PollName)
        {
            var options = pollManager.GetPollOptions(PollId);
            return PollName + Environment.NewLine + string.Join(Environment.NewLine, options.Select(o =>
            {
                var votes = pollManager.GetPollVotes(PollId);
                return $"{o.Text}: " + Environment.NewLine + string.Join(", ", votes.Where(v => v.OptId == o.Id).Select(v =>
                {
                    return GetChatUserName(v.UserId, v.UserId);
                }));
            }));
        }

        private static IEnumerable<InlineKeyboardButton[]> BuildPoolButtons(int PollId)
        {
            var options = pollManager.GetPollOptions(PollId);
            return options.Select(o => new InlineKeyboardCallbackButton(o.Text, $"{PollId}|{o.Id}")).Partition(BOT_MaxButtonLen);
        }

        private static string UserToString(Telegram.Bot.Types.User user)
        {
            if (string.IsNullOrEmpty(user.Username))
            {
                if (string.IsNullOrEmpty(user.LastName))
                    return user.FirstName;
                else
                    return $"{user.FirstName} {user.LastName}";
            }
            else
                return $"@{user.Username}";
        }

        public static async Task<string> GetChatUserNameAsync(int ChatId, int UserId)
        {
            string retn = string.Empty;
            var user = (await bot.GetChatMemberAsync(ChatId, UserId)).User;
            return UserToString(user);

        }

        public static string GetChatUserName(int ChatId, int UserId)
        {
            var ret = GetChatUserNameAsync(ChatId, UserId);
            ret.Wait();
            return ret.Result;
        }

        const string BOT_PollName = "What's the poll name?";
        const string BOT_GetOption = "Ok, now you can send me poll options.";
        const string BOT_GetOption2 = "Keep them coming, I'll stop listening when you send the text `Finish`.";
        const string BOT_PollList = "Here's the list of all the polls you made:";
        const int BOT_MaxButtonLen = 20;
    }
}
