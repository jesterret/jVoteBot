namespace jVoteBot.PollData
{
    class PollVote
    {
        public PollVote(long PollId, int UserId, long OptId)
        {
            this.OptId = OptId;
            this.PollId = PollId;
            this.UserId = UserId;
        }
        public PollVote(long Id, long PollId, int UserId, long OptId)
        {
            this.Id = Id;
            this.OptId = OptId;
            this.PollId = PollId;
            this.UserId = UserId;
        }
        public long Id { get; private set; }
        public long OptId { get; private set; }
        public long PollId { get; private set; }
        public int UserId { get; private set; }
    }
}
