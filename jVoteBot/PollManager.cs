using jVoteBot.PollData;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace jVoteBot
{
    class PollManager
    {
        const string SelectMaxIdPolls = "select MAX(Id) from Poll;";
        const string SelectMaxIdPollOptions = "select MAX(Id) from PollOption;";
        const string SelectMaxIdPollVotes = "select MAX(Id) from PollVote;";

        const string SelectUserExists = "select count(*) from PollUser where Id = @Id;";
        const string SelectUserOptionVoteExists = "select count(*) from PollVote where UserId = @UserId and OptionId = @OptId;";

        const string SelectPollByUserId = "select * from Poll where UserId = @UserId and Status = 1;";
        const string SelectPollByUserIdNotSetup = "select * from Poll where UserId = @UserId and Status = 0;";
        const string SelectPollByPollId = "select * from Poll where Id = @PollId and Status = 1;";
        const string SelectOptionsByPollId = "select * from PollOption where PollId = @PollId;";
        const string SelectUserVotesByPollId = "select * from PollVote where PollId = @PollId and UserId = @UserId;";
        const string SelectVotesByPollId = "select * from PollVote where PollId = @PollId;";
        const string SelectUserById = "select * from PollUser where Id = @UserId;";

        const string InsertPoll = "insert into Poll (Id, UserId, Status, Name) values (@Id, @UserId, @Status, @Name);";
        const string InsertOption = "insert into PollOption (Id, PollId, Text) values (@Id, @PollId, @Opt);";
        const string InsertVote = "insert into PollVote (Id, PollId, OptionId, UserId) values (@Id, @PollId, @OptId, @UserId);";
        const string InsertUser = "insert into PollUser (Id, Name) values (@Id, @Name);";

        const string DeletePollQuery = "delete from Poll where Id = @PollId and UserId = @UserId;";
        const string DeletePollOptions = "delete from PollOption where PollId = @PollId;";
        const string DeletePollVotes = "delete from PollVote where PollId = @PollId;";
        const string DeletePollUserVote = "delete from PollVote where PollId = @PollId and UserId = @UserId and OptionId = @OptId;";

        const string UpdatePollStatus = "update Poll set Status = 1 where Id = @Id;";
        const string UpdatePollUserName = "update PollUser set Name = @Name where Id = @Id;";
        
        public SQLiteConnection sqlConnection = new SQLiteConnection($"Data Source=\"{Path.Combine(Program.GetDirectory(), "PollDatabase.sqlite")}\";Version=3;");
        public PollManager()
        {
            sqlConnection.Open();
        }
        ~PollManager()
        {
            sqlConnection.Close();
        }

        public SQLiteDataReader RawQuery(string query)
        {
            var cmd = sqlConnection.CreateCommand();
            cmd.CommandText = query;
            return cmd.ExecuteReader();
        }

        public PollUser GetUser(int UserId)
        {
            try
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandText = SelectUserById;
                    command.Parameters.AddWithValue("@UserId", UserId);
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        return new PollUser((int)reader["Id"], reader["Name"] as string);
                    }
                }
            }
            catch { }
            return null;
        }
        
        public Poll GetPoll(long PollId)
        {
            try
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandText = SelectPollByPollId;
                    command.Parameters.AddWithValue("@PollId", PollId);
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        return new Poll((long)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string);
                    }
                }
            }
            catch { }
            return null;
        }

        public Poll GetCurrentSetupPoll(int UserId)
        {
            try
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandText = SelectPollByUserIdNotSetup;
                    command.Parameters.AddWithValue("@UserId", UserId);
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        return new Poll((long)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string);
                    }
                }
            }
            catch { }
            return null;
        }

        public IEnumerable<Poll> GetPollsByUser(int UserId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = SelectPollByUserId;
                command.Parameters.AddWithValue("@UserId", UserId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new Poll((long)reader["Id"], (int)reader["UserId"], (int)reader["Status"], reader["Name"] as string);
            }
        }

        public IEnumerable<PollOption> GetPollOptions(long PollId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = SelectOptionsByPollId;
                command.Parameters.AddWithValue("@PollId", PollId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollOption((long)reader["Id"], (long)reader["PollId"], reader["Text"] as string);
            }
        }

        public IEnumerable<PollVote> GetUserPollVotes(long PollId, int UserId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = SelectUserVotesByPollId;
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollVote((long)reader["Id"], PollId, UserId, (long)reader["OptId"]);
            }
        }

        public IEnumerable<PollVote> GetPollVotes(long PollId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = SelectVotesByPollId;
                command.Parameters.AddWithValue("@PollId", PollId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return new PollVote((long)reader["Id"], PollId, (int)reader["UserId"], (long)reader["OptionId"]);
            }
        }

        public string SetPoolFinished(int UserId)
        {
            var poll = GetCurrentSetupPoll(UserId);
            if (poll != null)
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandText = UpdatePollStatus;
                    command.Parameters.AddWithValue("@Id", poll.Id);
                    command.ExecuteNonQuery();
                    return poll.Name;
                }
            }
            return null;
        }
        
        public long AddPoll(int UserId, string Name)
        {
            var Id = GetLastID(SelectMaxIdPolls);
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = InsertPoll;
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@Status", 0);
                command.Parameters.AddWithValue("@Name", Name);
                command.ExecuteNonQuery();
            }
            return Id;
        }

        public long AddPollOption(long PollId, string Option)
        {
            var Id = GetLastID(SelectMaxIdPollOptions);
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = InsertOption;
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@Opt", Option);
                command.ExecuteNonQuery();
            }
            return Id;
        }

        public void AddDeleteVote(long PollId, Telegram.Bot.Types.User user, long OptId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                TryAddUser(user.Id, Program.UserToString(user));
                command.CommandText = SelectUserOptionVoteExists;
                command.Parameters.AddWithValue("@UserId", user.Id);
                command.Parameters.AddWithValue("@OptId", OptId);
                var val = (long)command.ExecuteScalar();
                if(val > 0)
                    DeleteVote(PollId, user.Id, OptId);
                else if (val == 0)
                    AddVote(GetLastID(SelectMaxIdPollVotes), PollId, user.Id, OptId);
            }
        }

        public void DeletePoll(long PollId, int UserId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = DeletePollQuery;
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.ExecuteNonQuery();
            }
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = DeletePollOptions;
                command.Parameters.AddWithValue("@PollId", PollId);
                command.ExecuteNonQuery();
            }
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = DeletePollVotes;
                command.Parameters.AddWithValue("@PollId", PollId);
                command.ExecuteNonQuery();
            }
        }

        public string GetUsername(int Id)
        {
            return GetUser(Id)?.Name;
        }

        private bool GetUserExists(int UserId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = SelectUserExists;
                command.Parameters.AddWithValue("@Id", UserId);
                var count = command.ExecuteScalar();
                return (long)count > 0;
            }
        }

        private long GetLastID(string Query)
        {
            long Id = 0;
            var data = RawQuery(Query);
            try
            {
                while (data.Read())
                {
                    var val = data.GetInt64(0);
                    if (val == long.MaxValue)
                        throw new OverflowException();
                    Id = val + 1;
                    break;
                }
                data.Close();
            }
            catch { }
            return Id;
        }

        private void TryAddUser(int Id, string Name)
        {
            if (!GetUserExists(Id))
                AddUser(Id, Name);
            else
                UpdateUser(Id, Name);
        }

        private void AddUser(int Id, string Name)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = InsertUser;
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@Name", Name);
                command.ExecuteNonQuery();
            }
        }

        private void UpdateUser(int Id, string Name)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = UpdatePollUserName;
                command.Parameters.AddWithValue("@Name", Name);
                command.Parameters.AddWithValue("@Id", Id);
                command.ExecuteNonQuery();
            }
        }

        private void AddVote(long Id, long PollId, int UserId, long OptId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = InsertVote;
                command.Parameters.AddWithValue("@Id", Id);
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@OptId", OptId);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteVote(long PollId, int UserId, long OptId)
        {
            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = DeletePollUserVote;
                command.Parameters.AddWithValue("@PollId", PollId);
                command.Parameters.AddWithValue("@UserId", UserId);
                command.Parameters.AddWithValue("@OptId", OptId);
                command.ExecuteNonQuery();
            }
        }
    }
}
