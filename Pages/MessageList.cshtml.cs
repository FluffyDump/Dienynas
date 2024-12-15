using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPages.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using Dienynas.Models;
using System.Text.Json;

namespace RazorPages.Pages
{
    public class MessageListModel : PageModel
    {
        private readonly TokenService _tokenService;
        private readonly ILogger<MessageListModel> _logger;

        public List<Zinute> Zinutes { get; set; } = new List<Zinute>();
        public Dictionary<int, Naudotojas> Naudotojai { get; set; } = new Dictionary<int, Naudotojas>();
        public string ErrorMessage { get; set; }
        public bool HasMessages => Zinutes.Any();

        public string FilterUsername { get; set; }
        public string SortOrder { get; set; } = "desc";
        public string FilterPrioritetas { get; set; }
        public Priotitetas? Priority { get; set; }

        public MessageListModel(TokenService tokenService, ILogger<MessageListModel> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }
        public bool IsSelectedPriority(string priority)
        {
            return FilterPrioritetas == priority;
        }
        public IActionResult OnGet(string filterUsername, string sortOrder, string filterPrioritetas)
        {
            FilterUsername = filterUsername;
            SortOrder = sortOrder ?? "desc"; 
            FilterPrioritetas = filterPrioritetas ?? "";

            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No access token found");
                ErrorMessage = "Praðome prisijungti";
                return RedirectToPage("/Login");
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                _logger.LogWarning("Invalid access token");
                ErrorMessage = "Netinkamas prisijungimas";
                return RedirectToPage("/Login");
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    FetchMessageSenders(connection, userId);
                    FetchMessages(connection, userId);

                    if (!string.IsNullOrEmpty(FilterUsername))
                    {
                        var filteredUserIds = Naudotojai
                            .Where(n => n.Value.slapyvardis.Equals(FilterUsername, StringComparison.OrdinalIgnoreCase))
                            .Select(n => n.Key)
                            .ToHashSet();

                        Zinutes = Zinutes.Where(z => filteredUserIds.Contains(z.fk_naudotojo_siuntejo_id)).ToList();
                    }

                    if (!string.IsNullOrEmpty(FilterPrioritetas) &&
                        Enum.TryParse<Priotitetas>(FilterPrioritetas, out var priority))
                    {
                        Priority = priority;
                        Zinutes = Zinutes.Where(z => z.priotitetas == Priority.Value).ToList();
                    }

                    if (SortOrder?.ToLower() == "asc")
                    {
                        Zinutes = Zinutes.OrderBy(z => z.date).ToList();
                    }
                    else
                    {
                        Zinutes = Zinutes.OrderByDescending(z => z.date).ToList();
                    }

                    if (!HasMessages)
                    {
                        ErrorMessage = "Jûs dar negavote jokiø þinuèiø";
                    }
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Database error when fetching messages");
                ErrorMessage = $"Duomenø bazës klaida: {ex.Message}";
            }

            return Page();
        }

        private void FetchMessageSenders(MySqlConnection connection, object userId)
        {
            string usersQuery = @"
            SELECT DISTINCT 
                n.naudotojo_id, 
                n.vardas, 
                n.pavarde,
                n.slapyvardis
            FROM Naudotojas n
            JOIN Zinute z ON z.fk_Siuntejo_id = n.naudotojo_id
            WHERE z.fk_Gavejo_id = @userId";

            using (var usersCommand = new MySqlCommand(usersQuery, connection))
            {
                usersCommand.Parameters.AddWithValue("@userId", userId);

                using (var usersReader = usersCommand.ExecuteReader())
                {
                    while (usersReader.Read())
                    {
                        var naudotojas = new Naudotojas
                        {
                            naudotojo_id = Convert.ToInt32(usersReader["naudotojo_id"]),
                            vardas = usersReader["vardas"]?.ToString(),
                            pavarde = usersReader["pavarde"]?.ToString(),
                            slapyvardis = usersReader["slapyvardis"]?.ToString()
                        };

                        Naudotojai[naudotojas.naudotojo_id] = naudotojas;
                    }
                }
            }
        }

        private void FetchMessages(MySqlConnection connection, object userId)
        {
            string query = @"
            SELECT 
                z.zinutes_id, 
                z.pavadinimas, 
                z.turinys, 
                z.s_data, 
                z.skaityta, 
                z.prioritetas, 
                z.fk_Siuntejo_id, 
                z.fk_Gavejo_id, 
                z.fk_Klases_id
            FROM Zinute z
            WHERE z.fk_Gavejo_id = @userId
            ORDER BY z.s_data DESC";

            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var zinute = new Zinute
                        {
                            zinutes_id = Convert.ToInt32(reader["zinutes_id"]),
                            pavadinimas = reader["pavadinimas"]?.ToString() ?? "-",
                            turinys = reader["turinys"]?.ToString() ?? "-",
                            date = reader["s_data"] != DBNull.Value && DateTime.TryParse(reader["s_data"].ToString(), out var parsedDate)
                                ? parsedDate
                                : DateTime.MinValue,
                            skaityta = reader["skaityta"] != DBNull.Value && Convert.ToBoolean(reader["skaityta"]),
                            priotitetas = reader["prioritetas"] == DBNull.Value
                                    ? Priotitetas.Zemas
                                    : Enum.Parse<Priotitetas>(reader["prioritetas"].ToString()),
                            fk_naudotojo_siuntejo_id = reader["fk_Siuntejo_id"] != DBNull.Value ? Convert.ToInt32(reader["fk_Siuntejo_id"]) : 0,
                            fk_naudotojo_gavejo_id = reader["fk_Gavejo_id"] != DBNull.Value ? Convert.ToInt32(reader["fk_Gavejo_id"]) : 0,
                            fk_klases_id = reader["fk_Klases_id"] != DBNull.Value ? Convert.ToInt32(reader["fk_Klases_id"]) : (int?)null
                        };

                        Zinutes.Add(zinute);
                    }
                }
            }
        }

        public IActionResult OnPostMarkAsRead(int messageId)
        {
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

                    string updateQuery = @"
            UPDATE Zinute 
            SET skaityta = TRUE 
            WHERE zinutes_id = @MessageId AND fk_Gavejo_id = @UserId";

                    using (var command = new MySqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MessageId", messageId);
                        command.Parameters.AddWithValue("@UserId", userId);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read");
                TempData["ErrorMessage"] = "Klaida paþymint þinutæ kaip skaitytà";
            }

            return RedirectToPage("/Message", new { messageId = messageId });
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