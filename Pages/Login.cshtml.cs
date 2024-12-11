using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace RazorPages.Pages
{
    public class LoginModel : PageModel
    {
        private readonly TokenService _tokenService;

        public LoginModel(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        [BindProperty]
        public string UsernameOrEmail { get; set; }
        [BindProperty]
        public string Password { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            string getUserQuery = "SELECT naudotojo_id, slaptazodis FROM Naudotojas WHERE slapyvardis = @usernameOrEmail OR elektroninis_pastas = @usernameOrEmail";

            try
            {
                int? userId = null;
                string storedHashedPassword = null;

                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    using (var command = new MySqlCommand(getUserQuery, connection))
                    {
                        command.Parameters.AddWithValue("@usernameOrEmail", UsernameOrEmail);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userId = reader.GetInt32("naudotojo_id");
                                storedHashedPassword = reader.GetString("slaptazodis");
                            }
                        }
                    }

                    bool isAuthenticated = false;

                    if (userId.HasValue && storedHashedPassword != null)
                    {
                        isAuthenticated = PasswordHelper.VerifyPassword(Password, storedHashedPassword);
                    }
                    
                    string insertLoginQuery = @"INSERT INTO Prisijungimas (sesijos_pradzios_laikas, naudotojas_autentifikuotas, 
                    fk_Naudotojo_id) VALUES (ADDTIME(NOW(), '02:00:00'), @isAuthenticated, @userId)";

                    using (var command = new MySqlCommand(insertLoginQuery, connection))
                    {
                        command.Parameters.AddWithValue("@isAuthenticated", isAuthenticated);
                        command.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                        command.ExecuteNonQuery();
                    }

                    if (isAuthenticated)
                    {
                        var token = _tokenService.GenerateToken(userId.Value);
                        HttpContext.Response.Cookies.Append("access_token", token);
                        return RedirectToPage("/Profile");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Neteisingas vartotojo vardas, el. pašto adresas arba slaptažodis.");
                    }
                }
            }
            catch (MySqlException ex) when (ex.Message.Contains("fk_Naudotojo_id")) 
            { 
                ModelState.AddModelError(string.Empty, "Neteisingas vartotojo vardas, el. pašto adresas arba slaptažodis.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Įvyko serverio klaida: " + ex.Message);
            }
            return Page();
        }
    }
}