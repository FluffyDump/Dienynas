using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace RazorPages.Pages
{
    public class ChangePasswordModel : PageModel
    {
        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        private readonly TokenService _tokenService;

        public ChangePasswordModel(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
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

            if (NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError("", "Pateikti slaptažodžiai nesutampa!");
                return Page();
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    if (NewPassword.Length < 6)
                    {
                        ModelState.AddModelError("", "Slaptažodis turi būti sudarytas iš bent 6 simbolių!");
                        return Page();
                    }

                    string hashedPassword = PasswordHelper.HashPassword(NewPassword);

                    string updatePasswordQuery = "UPDATE Naudotojas SET slaptazodis = @newPassword WHERE naudotojo_id = @userId";
                    using (var command = new MySqlCommand(updatePasswordQuery, connection))
                    {
                        command.Parameters.AddWithValue("@newPassword", hashedPassword);
                        command.Parameters.AddWithValue("@userId", userId);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            ModelState.AddModelError("", "Nepavyko atnaujinti slaptažodžio.");
                            return Page();
                        }
                    }
                }

                return RedirectToPage("/Profile");
            }
            catch (MySqlException ex)
            {
                ModelState.AddModelError("", "Įvyko klaida jungiantis prie duomenų bazės: " + ex.Message);
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Įvyko nenumatyta klaida: " + ex.Message);
                return Page();
            }
        }
    }
}