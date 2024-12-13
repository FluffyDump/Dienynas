using MySql.Data.MySqlClient;
using System.Data;

public static class DatabaseHelper
{
    public static MySqlConnection GetConnection()
    {
        return new MySqlConnection(connectionString);
    }

    public static DataTable ExecuteQuery(string query)
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            using (var command = new MySqlCommand(query, connection))
            {
                using (var dataAdapter = new MySqlDataAdapter(command))
                {
                    DataTable dataTable = new DataTable();
                    dataAdapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }
    }

    public static int ExecuteNonQuery(string query)
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            using (var command = new MySqlCommand(query, connection))
            {
                return command.ExecuteNonQuery();
            }
        }
    }
}
