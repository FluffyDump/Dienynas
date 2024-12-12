using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace RazorPages.Pages
{
    public class ChangeEmailModel : PageModel
    {
        [BindProperty]
        public string NewEmail { get; set; }

        private readonly TokenService _tokenService;

        public ChangeEmailModel(TokenService tokenService)
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

            if (string.IsNullOrEmpty(NewEmail))
            {
                ModelState.AddModelError("", "Naujas elektroninis pašto adresas negali būti tuščias!");
                return Page();
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    string checkEmailQuery = "SELECT COUNT(*) FROM Naudotojas WHERE elektroninis_pastas = @newEmail AND naudotojo_id != @userId";
                    using (var checkEmailCommand = new MySqlCommand(checkEmailQuery, connection))
                    {
                        checkEmailCommand.Parameters.AddWithValue("@newEmail", NewEmail);
                        checkEmailCommand.Parameters.AddWithValue("@userId", userId);

                        int emailCount = Convert.ToInt32(checkEmailCommand.ExecuteScalar());

                        if (emailCount > 0)
                        {
                            ModelState.AddModelError("", "Toks el. pašto adresas jau naudojamas kito vartotojo!");
                            return Page();
                        }
                    }

                    string updateEmailQuery = "UPDATE Naudotojas SET elektroninis_pastas = @newEmail WHERE naudotojo_id = @userId";
                    using (var command = new MySqlCommand(updateEmailQuery, connection))
                    {
                        command.Parameters.AddWithValue("@newEmail", NewEmail);
                        command.Parameters.AddWithValue("@userId", userId);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            ModelState.AddModelError("", "Nepavyko atnaujinti elektroninio pašto adreso!");
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