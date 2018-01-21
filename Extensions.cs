using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Telegram.Bot.Types.ReplyMarkups;

namespace jVoteBot
{
    static class Extensions
    {
        public static IEnumerable<InlineKeyboardButton[]> Partition(this IEnumerable<InlineKeyboardButton> sequence, int partitionSize)
        {
            const int ButtonBorderOffset = 5;
            Contract.Requires(sequence != null);
            Contract.Requires(partitionSize > 0);

            List<InlineKeyboardButton> buffer = new List<InlineKeyboardButton>();
            int len = 0;
            foreach (var item in sequence)
            {
                if (len >= partitionSize)
                {
                    yield return buffer.ToArray();
                    buffer.Clear();
                    buffer.Add(item);
                    len = item.Text.Length + ButtonBorderOffset;
                }
                else
                {
                    buffer.Add(item);
                    len += item.Text.Length + ButtonBorderOffset;
                }
            }
            if (len != 0 && buffer.Count > 0)
                yield return buffer.ToArray();
        }
    }
}
