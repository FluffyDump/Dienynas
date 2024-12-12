using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System;
using System.IO;

namespace RazorPages.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly TokenService _tokenService;
        public Naudotojas Naudotojas { get; set; }

        public ProfileModel(TokenService tokenService)
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

            string query = $"SELECT * FROM Naudotojas WHERE naudotojo_id = @userId";

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
                            if (reader.Read())
                            {
                                Naudotojas = new Naudotojas
                                {
                                    vardas = reader["vardas"]?.ToString() ?? "-",
                                    pavarde = reader["pavarde"]?.ToString() ?? "-",
                                    slapyvardis = reader["slapyvardis"]?.ToString() ?? "-",
                                    aprasymas = reader["aprasymas"]?.ToString() ?? "-",
                                    gimimo_data = reader["gimimo_data"] != DBNull.Value ? (DateTime)reader["gimimo_data"] : DateTime.MinValue,
                                    elektroninis_pastas = reader["elektroninis_pastas"]?.ToString() ?? "-",
                                    mokymo_istaigos_pavadinimas = reader["mokymo_istaigos_pavadinimas"]?.ToString() ?? "-",
                                    miestas = reader["miestas"]?.ToString() ?? "-",
                                    adresas = reader["adresas"]?.ToString() ?? "-"
                                };
                            }
                            else
                            {
                                ModelState.AddModelError("", "Vartotojas nerastas.");
                                return;
                            }
                        }
                    }

                    string roleQuery = @"
                        SELECT 
                            CASE
                                WHEN EXISTS (SELECT 1 FROM Mokinys WHERE naudotojo_id = @userId) THEN 'Mokinys'
                                WHEN EXISTS (SELECT 1 FROM Mokytojas WHERE naudotojo_id = @userId) THEN 'Mokytojas'
                                WHEN EXISTS (SELECT 1 FROM Administratorius WHERE naudotojo_id = @userId) THEN 'Administratorius'
                                ELSE 'Nežinomas'
                            END AS profilio_tipas;";

                    using (var roleCommand = new MySqlCommand(roleQuery, connection))
                    {
                        roleCommand.Parameters.AddWithValue("@userId", userId);
                        var role = roleCommand.ExecuteScalar()?.ToString();

                        Naudotojas.profilio_tipas = role ?? "-";
                    }

                    var profileImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", $"{userId}.png");
                    if (System.IO.File.Exists(profileImagePath))
                    {
                        Naudotojas.profilio_nuotrauka = $"/Images/{userId}.png";
                    }
                    else
                    {
                        Naudotojas.profilio_nuotrauka = "/Images/user_default.png";
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