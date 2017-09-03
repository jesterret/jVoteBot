using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jVoteBot.PollData
{
    class Poll
    {
        public Poll(int UserId, int Status, string Name, string Description = null)
        {
            this.UserId = UserId;
            this.Status = Status;
            this.Name = Name;
            this.Description = Description;
        }
        public Poll(int Id, int UserId, int Status, string Name, string Description = null)
        {
            this.Id = Id;
            this.UserId = UserId;
            this.Status = Status;
            this.Name = Name;
            this.Description = Description;
        }
        public int Id { get; private set; }
        public int Status { get; private set; }
        public int UserId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
    }
}
