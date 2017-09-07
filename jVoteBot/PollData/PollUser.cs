namespace jVoteBot.PollData
{
    class PollUser
    {
        public PollUser(string Name)
        {
            this.Name = Name;
        }
        public PollUser(int Id, string Name)
        {
            this.Id = Id;
            this.Name = Name;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
    }
}
