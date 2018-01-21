using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jVoteBot.PollData
{
    public class PollOption
    {
        public string Text { get; set; }
        public List<int> Votes { get; set; } = new List<int>();

        public void Vote(int userId)
        {
            if (Votes.Contains(userId))
                Votes.Remove(userId);
            else
                Votes.Add(userId);
        }
    }
}
