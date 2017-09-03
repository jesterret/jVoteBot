using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jVoteBot.PollData
{
    class PollVote
    {
        public PollVote(int PollId, int UserId, int OptId)
        {
            this.OptId = OptId;
            this.PollId = PollId;
            this.UserId = UserId;
        }
        public PollVote(int Id, int PollId, int UserId, int OptId)
        {
            this.Id = Id;
            this.OptId = OptId;
            this.PollId = PollId;
            this.UserId = UserId;
        }
        public int Id { get; private set; }
        public int OptId { get; private set; }
        public int PollId { get; private set; }
        public int UserId { get; private set; }
    }
}
