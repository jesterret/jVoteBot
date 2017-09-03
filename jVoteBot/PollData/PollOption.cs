using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jVoteBot.PollData
{
    class PollOption
    {
        public PollOption(int PollId, string Option)
        {
            this.PollId = PollId;
            this.Text = Option;
        }
        public PollOption(int Id, int PollId, string Option)
        {
            this.Id = Id;
            this.PollId = PollId;
            this.Text = Option;
        }
        public int Id { get; private set; }
        public int PollId { get; private set; }
        public string Text { get; private set; }
    }
}
