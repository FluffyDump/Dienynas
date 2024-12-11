using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace RazorPages.Pages
{
    public class LoginHistoryModel : PageModel
    {
        private readonly TokenService _tokenService;

        public List<Prisijungimas> LoginHistory { get; set; }

        public LoginHistoryModel(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public void OnGet()
        {
            var token = Request.Cookies["access_token"];

            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return;
            }

            string query = @"
                SELECT autentifikacijos_id, sesijos_pradzios_laikas, naudotojas_autentifikuotas
                FROM Prisijungimas
                WHERE fk_Naudotojo_id = @userId
                ORDER BY sesijos_pradzios_laikas DESC
                LIMIT 15;";

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            LoginHistory = new List<Prisijungimas>();

                            while (reader.Read())
                            {
                                var login = new Prisijungimas
                                {
                                    sesijos_pradzios_laikas = reader["sesijos_pradzios_laikas"] != DBNull.Value ? Convert.ToDateTime(reader["sesijos_pradzios_laikas"]) : DateTime.MinValue,
                                    naudotojas_autentifikuotas = reader["naudotojas_autentifikuotas"] != DBNull.Value ? Convert.ToBoolean(reader["naudotojas_autentifikuotas"]) : false
                                };

                                LoginHistory.Add(login);
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                ModelState.AddModelError("", "Įvyko klaida jungiantis prie duomenų bazės: " + ex.Message);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Įvyko nenumatyta klaida: " + ex.Message);
            }
        }
    }
}