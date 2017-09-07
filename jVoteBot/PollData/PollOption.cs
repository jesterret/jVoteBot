namespace jVoteBot.PollData
{
    class PollOption
    {
        public PollOption(long PollId, string Option)
        {
            this.PollId = PollId;
            this.Text = Option;
        }
        public PollOption(long Id, long PollId, string Option)
        {
            this.Id = Id;
            this.PollId = PollId;
            this.Text = Option;
        }
        public long Id { get; private set; }
        public long PollId { get; private set; }
        public string Text { get; private set; }
    }
}
