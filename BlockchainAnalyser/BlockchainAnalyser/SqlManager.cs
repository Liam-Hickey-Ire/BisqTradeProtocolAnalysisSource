using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace BlockchainAnalyser
{
    class SqlManager
    {
        string m_connectionString = string.Empty;
        string m_filePath = string.Empty;

        public SqlManager(string connectionString, string filePath)
        {
            m_connectionString = connectionString;
            m_filePath = filePath;
        }

        ~SqlManager() { }

        public DataTable ExecuteQuery(SqlCommand command)
        {
            using (SqlConnection connection = new SqlConnection(m_connectionString))
            {
                connection.Open();
                command.Connection = connection;
                DataTable dataTable = new DataTable();
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                adapter.SelectCommand.CommandTimeout = 360; // 360 second timeout, the reaosn being, this app which uses a LOT of memory when loading verbose trades, which slows us down, slowing down the bigger wueries that we use
                adapter.Fill(dataTable);
                return dataTable;
            }
        }

        public void ExecuteNonQuery(SqlCommand command)
        {
            using (SqlConnection connection = new SqlConnection(m_connectionString))
            {
                connection.Open();
                command.Connection = connection;
                command.ExecuteNonQuery();
            }
        }

        public DataTable ExecuteQueryScript(string filename)
        {
            string script = File.ReadAllText(Path.Combine(m_filePath, filename));
            SqlCommand command = new SqlCommand(script);
            return ExecuteQuery(command);
        }

        public DataTable ExecuteQueryScript(string filename, List<SqlParameter> sqlParams)
        {
            string script = File.ReadAllText(Path.Combine(m_filePath, filename));
            SqlCommand command = new SqlCommand(script);
            AddParametersToCommand(command, sqlParams);
            return ExecuteQuery(command);
        }

        public void ExecuteNonQueryScript(string filename)
        {
            string script = File.ReadAllText(Path.Combine(m_filePath, filename));
            SqlCommand command = new SqlCommand(script);
            ExecuteNonQuery(command);
        }

        public void ExecuteNonQueryScript(string filename, List<SqlParameter> sqlParams)
        {
            string script = File.ReadAllText(Path.Combine(m_filePath, filename));
            SqlCommand command = new SqlCommand(script);
            AddParametersToCommand(command, sqlParams);
            ExecuteNonQuery(command);
        }

        private SqlCommand AddParametersToCommand(SqlCommand command, List<SqlParameter> sqlParams)
        {
            foreach (SqlParameter param in sqlParams)
            {
                command.Parameters.Add(param);
            }
            return command;
        }
    }
}
