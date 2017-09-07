namespace jVoteBot.PollData
{
    class Poll
    {
        public Poll(int UserId, int Status, string Name)
        {
            this.UserId = UserId;
            this.Status = Status;
            this.Name = Name;
        }
        public Poll(long Id, int UserId, int Status, string Name)
        {
            this.Id = Id;
            this.UserId = UserId;
            this.Status = Status;
            this.Name = Name;
        }
        public long Id { get; private set; }
        public int Status { get; private set; }
        public int UserId { get; private set; }
        public string Name { get; private set; }
    }
}
