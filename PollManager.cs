using jVoteBot.PollData;
using LiteDB;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace jVoteBot
{
    public class PollManager
    {
        private LiteDatabase db = new LiteDatabase(Path.Combine(Program.GetDirectory(), "PollDatabase.db"));
        ~PollManager()
        {
            db.Dispose();
        }

        public Poll GetPoll(long pollId)
        {
            return PollTable.FindById(pollId);
        }
        public PollUser GetUser(int userId)
        {
            return UserTable.FindById(userId);
        }
        public string GetUsername(int userId)
        {
            return GetUser(userId)?.Name;
        }
        public void SetUser(int userId, string name)
        {
            UserTable.Upsert(new PollUser
            {
                Id = userId,
                Name = name
            });
        }
        public bool DeletePoll(long pollId, int userId)
        {
            var targetPoll = GetPoll(pollId);
            if (targetPoll != null && userId == targetPoll.OwnerId)
                return PollTable.Delete(pollId);
            else
                return false;
        }
        public IEnumerable<Poll> GetPollsByUser(int userId)
        {
            return PollTable.Find(poll => poll.OwnerId == userId);
        }
        public void AddPoll(int userId, string name)
        {
            PollTable.Insert(new Poll
            {
                Name = name,
                OwnerId = userId,
                IsSetUp = false,
                CanAddOptions = false
            });
        }
        public Poll GetCurrentSetupPoll(int userId)
        {
            return GetPollsByUser(userId).Where(poll => poll.IsSetUp == false).SingleOrDefault();
        }

        public void Update(Poll poll)
        {
            PollTable.Update(poll);
        }
        
        public LiteCollection<Poll> PollTable => db.GetCollection<Poll>("polls");
        public LiteCollection<PollUser> UserTable => db.GetCollection<PollUser>("users");
    }
}
