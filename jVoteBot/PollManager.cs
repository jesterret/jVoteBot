using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data.Common;
using System.IO;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using jVoteBot.PollData;

namespace jVoteBot
{
    class PollManager
    {
        const string SelectMaxIdPolls = "select MAX(Id) from Polls;";
        const string SelectMaxIdPollOptions = "select MAX(Id) from PollOption;";
        const string SelectMaxIdPollVotes = "select MAX(Id) from PollVote;";

        const string SelectUserOptionVoteExists = "select count(*) from PollVote where UserId = @UserId and OptionId = @OptId;";

        const string SelectPollByUserId = "select * from Polls where UserId = @UserId and Status = 1;";
        const string SelectPollByUserIdNotSetup = "select * from Polls where UserId = @UserId and Status = 0;";
        const string SelectPollByPollId = "select * from Polls where Id = @PollId and Status = 1;";
        const string SelectOptionsByPollId = "select * from PollOption where PollId = @PollId;";
        const string SelectUserVotesByPollId = "select * from PollVote where PollId = @PollId and UserId = @UserId;";
        const string SelectVotesByPollId = "select * from PollVote where PollId = @PollId;";

        const string InsertPoll = "insert into Polls (Id, UserId, Status, Name, Description) values (@Id, @UserId, @Status, @Name, @Description);";
        const string InsertPollOption = "insert into PollOption (Id, PollId, Text) values (@Id, @PollId, @Opt);";
        const string InsertVote = "insert into PollVote (Id, PollId, OptionId, UserId) values (@Id, @PollId, @OptId, @UserId);";

        const string DeletePollQuery = "delete from Polls where Id = @PollId and UserId = @UserId;";
        const string DeletePollOptions = "delete from PollOption where PollId = @PollId;";
        const string DeletePollVotes = "delete from PollVote where PollId = @PollId;";

        const string DeletePollUserVote = "delete from PollVote where PollId = @PollId and UserId = @UserId and OptionId = @OptId;";

        const string UpdatePollStatus = "update Polls set Status = @Status where Id = @Id;";

        public SqlConnection sqlConnection = new SqlConnection($"Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=\"{Path.Combine(Program.GetDirectory(), "PollDatabase.mdf")}\";Integrated Security=True;MultipleActiveResultSets=True");
        
        public PollManager()
        {
            sqlConnection.Open();
        }
        ~PollManager()
        {
            sqlConnection.Close();
        }

        public SqlDataReader RawQuery(string query)
        {
            var cmd = sqlConnection.CreateCommand();
            cmd.CommandText = query;
            return cmd.ExecuteReader();
        }
        
        public Poll GetPoll(int PollId)
        {
            using (var command = new SqlCommand(SelectPollByPollId, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                using (var reader = command.ExecuteReader())
                {
                    reader.Read();
                    return new Poll((int)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string, reader["Description"] as string);
                }
            }
        }

        public Poll GetCurrentSetupPoll(int UserId)
        {
            try
            {
                using (var command = new SqlCommand(SelectPollByUserIdNotSetup, sqlConnection))
                {
                    command.Parameters.AddWithValue("@UserId", UserId);
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        return new Poll((int)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string, reader["Description"] as string);
                    }
                }
            }
            catch { }
            return null;
        }

        public IEnumerable<Poll> GetPollsByUser(int UserId)
        {
            using (var command = new SqlCommand(SelectPollByUserId, sqlConnection))
            {
                command.Parameters.AddWithValue("@UserId", UserId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new Poll((int)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string, reader["Description"] as string);
            }
        }

        public IEnumerable<PollOption> GetPollOptions(int PollId)
        {
            using (var command = new SqlCommand(SelectOptionsByPollId, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollOption((int)reader["Id"], (int)reader["PollId"], reader["Text"] as string);
            }
        }

        public IEnumerable<PollVote> GetUserPollVotes(int PollId, int UserId)
        {
            using (var command = new SqlCommand(SelectUserVotesByPollId, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollVote((int)reader["Id"], PollId, UserId, (int)reader["OptId"]);
            }
        }

        public IEnumerable<PollVote> GetPollVotes(int PollId)
        {
            using (var command = new SqlCommand(SelectVotesByPollId, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollVote((int)reader["Id"], PollId, (int)reader["UserId"], (int)reader["OptionId"]);
            }
        }

        public string SetPoolFinished(int UserId)
        {
            var poll = GetCurrentSetupPoll(UserId);
            if (poll != null)
            {
                using (var command = new SqlCommand(UpdatePollStatus, sqlConnection))
                {
                    command.Parameters.AddWithValue("@Status", 1);
                    command.Parameters.AddWithValue("@Id", poll.Id);
                    command.ExecuteNonQuery();
                    return poll.Name;
                }
            }
            return null;
        }
        
        public void AddPoll(int UserId, string Name, string Description = null)
        {
            using (var command = new SqlCommand(InsertPoll, sqlConnection))
            {
                var Id = GetLastID(SelectMaxIdPolls);
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@Status", 0);
                command.Parameters.AddWithValue("@Name", Name);
                command.Parameters.AddWithValue("@Description", Description ?? "Send pool to chat");
                command.ExecuteNonQuery();
            }
        }

        public void AddPollOption(int PollId, string Option)
        {
            using (var command = new SqlCommand(InsertPollOption, sqlConnection))
            {
                command.Parameters.AddWithValue("@Id", GetLastID(SelectMaxIdPollOptions));
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@Opt", Option);
                command.ExecuteNonQuery();
            }
        }

        public void AddDeleteVote(int PollId, int UserId, int OptId)
        {
            using (var command = new SqlCommand(SelectUserOptionVoteExists, sqlConnection))
            {
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@OptId", OptId);
                var val = (int)command.ExecuteScalar();
                if(val > 0)
                    DeleteVote(PollId, UserId, OptId);
                else if (val == 0)
                    AddVote(GetLastID(SelectMaxIdPollVotes), PollId, UserId, OptId);
            }
        }

        public void DeletePoll(int PollId, int UserId)
        {
            using (var command = new SqlCommand(DeletePollQuery, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.ExecuteNonQuery();
            }
            using (var command = new SqlCommand(DeletePollOptions, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                command.ExecuteNonQuery();
            }
            using (var command = new SqlCommand(DeletePollVotes, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                command.ExecuteNonQuery();
            }
        }

        private int GetLastID(string Query)
        {
            int Id = 0;
            var data = RawQuery(Query);
            try
            {
                while (data.Read())
                {
                    Id = data.GetInt32(0) + 1;
                    break;
                }
            }
            catch { }
            return Id;
        }

        private void AddVote(int Id, int PollId, int UserId, int OptId)
        {
            using (var command = new SqlCommand(InsertVote, sqlConnection))
            {
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@OptId", OptId);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteVote(int PollId, int UserId, int OptId)
        {
            using (var command = new SqlCommand(DeletePollUserVote, sqlConnection))
            {
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@OptId", OptId);
                command.ExecuteNonQuery();
            }
        }
    }
}
