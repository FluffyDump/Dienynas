using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using RazorPages.Models;
using System;

namespace RazorPages.Pages
{
    public class MessageModel : PageModel
    {
        private readonly TokenService _tokenService;
        public string SenderName { get; set; }
        public DateTime MessageDate { get; set; }
        public string MessageBody { get; set; }
        public Priotitetas? Priority { get; set; }
        public MessageModel(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public IActionResult OnGet(int messageId)
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

                    var messageQuery = @"
                    SELECT z.pavadinimas, z.turinys, z.s_data, z.prioritetas, n.vardas, n.pavarde
                    FROM Zinute z
                    JOIN Naudotojas n ON z.fk_Siuntejo_id = n.naudotojo_id
                    WHERE z.zinutes_id = @messageId AND z.fk_Gavejo_id = @userId";

                    using (var command = new MySqlCommand(messageQuery, connection))
                    {
                        command.Parameters.AddWithValue("@messageId", messageId);
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                SenderName = $"{reader["vardas"]} {reader["pavarde"]}";
                                MessageDate = Convert.ToDateTime(reader["s_data"]);
                                MessageBody = reader["turinys"]?.ToString();
                                Priority = reader["prioritetas"] == DBNull.Value
                                    ? Priotitetas.Zemas
                                    : Enum.Parse<Priotitetas>(reader["prioritetas"].ToString());
                            }
                            else
                            {
                                return RedirectToPage("/MessageList");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = $"Error: {ex.Message}";
                return Page();
            }

            return Page();
        }

        public IActionResult OnPostChangePriority(int messageId, Priotitetas newPriority)
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

                    var updateQuery = "UPDATE Zinute SET prioritetas = @newPriority WHERE zinutes_id = @messageId AND fk_Gavejo_id = @userId";

                    using (var command = new MySqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@newPriority", newPriority.ToString());
                        command.Parameters.AddWithValue("@messageId", messageId);
                        command.Parameters.AddWithValue("@userId", userId);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = $"Error updating priority: {ex.Message}";
                return Page();
            }

            TempData["SuccessMessage"] = "Prioritetas sëkmingai pakeistas!";
            return RedirectToPage(new { messageId });
        }
    }
}
