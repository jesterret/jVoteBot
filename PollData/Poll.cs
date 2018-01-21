using System.Collections.Generic;

namespace jVoteBot.PollData
{
    public class Poll
    {
        public long Id { get; private set; }
        public string Name { get; set; }
        public int OwnerId { get; set; }
        public bool CanAddOptions { get; set; }
        public bool IsSetUp { get; set; }
        public List<PollOption> Options { get; set; } = new List<PollOption>();
        public List<string> InlineQueries { get; set; } = new List<string>();

        public bool AddOption(int userId, string text)
        {
            if (CanAddOptions || userId == OwnerId)
            {
                Options.Add(new PollOption
                {
                    Text = text
                });
            }
            else
                return false;
            
            return true;
        }

        public void AddQuery(string QueryId)
        {
            if (!InlineQueries.Contains(QueryId))
                InlineQueries.Add(QueryId);
        }

        public bool AddVote(int userId, int optionIndex)
        {
            if (optionIndex > Options.Count)
                return false;

            Options[optionIndex].Vote(userId);
            return true;
        }
    }
}
