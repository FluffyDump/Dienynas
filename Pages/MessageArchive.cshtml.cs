using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPages.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using Dienynas.Models;

namespace RazorPages.Pages
{
    public class MessageArchiveModel : PageModel
    {
        private readonly TokenService _tokenService;
        private readonly ILogger<MessageArchiveModel> _logger;

        public List<Zinute> Zinutes { get; set; } = new List<Zinute>();
        public Dictionary<int, Naudotojas> Naudotojai { get; set; } = new Dictionary<int, Naudotojas>();

        public MessageArchiveModel(TokenService tokenService, ILogger<MessageArchiveModel> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        public void OnGet()
        {
            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                Response.Redirect("/Login");
                return;
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                Response.Redirect("/Login");
                return;
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();
                    FetchMessages(connection, userId);
                    FetchMessageSenders(connection, userId);
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Database error fetching messages");
            }
        }

        public IActionResult OnPost(List<int> messageIds)
        {
            if (messageIds == null || messageIds.Count == 0)
            {
                return RedirectToPage();
            }

            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    string archiveQuery = @"
                        UPDATE Zinute 
                        SET archyvuota = TRUE 
                        WHERE zinutes_id = @MessageId AND fk_Gavejo_id = @UserId";

                    foreach (var messageId in messageIds)
                    {
                        using (var command = new MySqlCommand(archiveQuery, connection))
                        {
                            command.Parameters.AddWithValue("@MessageId", messageId);
                            command.Parameters.AddWithValue("@UserId", userId);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Error archiving messages");
            }

            return RedirectToPage();
        }

        private void FetchMessages(MySqlConnection connection, object userId)
        {
            string query = @"
            SELECT 
                z.zinutes_id, 
                z.pavadinimas, 
                z.archyvuota, 
                z.fk_Siuntejo_id
            FROM Zinute z
            WHERE z.fk_Gavejo_id = @UserId";

            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var message = new Zinute
                        {
                            zinutes_id = Convert.ToInt32(reader["zinutes_id"]),
                            pavadinimas = reader["pavadinimas"]?.ToString() ?? "-",
                            archyvuota = reader["archyvuota"] != DBNull.Value && Convert.ToBoolean(reader["archyvuota"]),
                            fk_naudotojo_siuntejo_id = Convert.ToInt32(reader["fk_Siuntejo_id"])
                        };

                        Zinutes.Add(message);
                    }
                }
            }
        }

        private void FetchMessageSenders(MySqlConnection connection, object userId)
        {
            string usersQuery = @"
            SELECT DISTINCT 
                n.naudotojo_id, 
                n.vardas, 
                n.pavarde
            FROM Naudotojas n
            JOIN Zinute z ON z.fk_Siuntejo_id = n.naudotojo_id
            WHERE z.fk_Gavejo_id = @UserId";

            using (var command = new MySqlCommand(usersQuery, connection))
            {
                command.Parameters.AddWithValue("@UserId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var naudotojas = new Naudotojas
                        {
                            naudotojo_id = Convert.ToInt32(reader["naudotojo_id"]),
                            vardas = reader["vardas"]?.ToString(),
                            pavarde = reader["pavarde"]?.ToString()
                        };

                        Naudotojai[naudotojas.naudotojo_id] = naudotojas;
                    }
                }
            }
        }

        public string GetSenderFullName(int senderId)
        {
            if (Naudotojai.TryGetValue(senderId, out var sender))
            {
                return $"{sender.vardas} {sender.pavarde}";
            }
            return "Neþinomas siuntëjas";
        }
    }
}
