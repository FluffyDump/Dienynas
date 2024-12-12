using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Data;
using System;

namespace RazorPages.Pages
{
    public class ChangePasswordModel : PageModel
    {
        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        public class TwoFACodeRequest
        {
            public string TwoFACode { get; set; }
            public string NewPassword { get; set; }
        }

        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;

        public ChangePasswordModel(TokenService tokenService, EmailService emailService)
        {
            _tokenService = tokenService;
            _emailService = emailService;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Index");
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return RedirectToPage("/Index");
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
                    await connection.OpenAsync();

                    string getUserQuery = "SELECT elektroninis_pastas FROM Naudotojas WHERE naudotojo_id = @userId";
                    string userEmail = null;

                    using (var getEmailCommand = new MySqlCommand(getUserQuery, connection))
                    {
                        getEmailCommand.Parameters.AddWithValue("@userId", userId);

                        using (var reader = await getEmailCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userEmail = reader.GetString("elektroninis_pastas");
                            }
                            else
                            {
                                ModelState.AddModelError("", "Naudotojo el. paštas nerastas.");
                                return Page();
                            }
                        }
                    }

                    if (NewPassword.Length < 6)
                    {
                        ModelState.AddModelError("", "Slaptažodis turi būti sudarytas iš bent 6 simbolių!");
                        return Page();
                    }

                    string twoFACode = _emailService.GenerateRandomCode();

                    string saveCodeQuery = @"
                        INSERT INTO Dvieju_faktoriu_autentifikacija 
                            (patvirtinimo_kodas, kodo_issiuntimo_laikas, kodo_galiojimo_laikas, fk_Naudotojo_id) 
                        VALUES 
                            (@code, DATE_ADD(NOW(), INTERVAL 2 HOUR), DATE_ADD(DATE_ADD(NOW(), INTERVAL 2 HOUR), INTERVAL 15 MINUTE), @userId)";

                    using (var saveCodeCommand = new MySqlCommand(saveCodeQuery, connection))
                    {
                        saveCodeCommand.Parameters.AddWithValue("@code", twoFACode);
                        saveCodeCommand.Parameters.AddWithValue("@userId", userId);
                        await saveCodeCommand.ExecuteNonQueryAsync();
                    }

                    bool emailSent = await _emailService.SendEmailAsync(userEmail, "Mokykla - 2FA kodas", $"Jūsų mokyklos 2FA kodas: {twoFACode}");

                    if (!emailSent)
                    {
                        ModelState.AddModelError("", "Nepavyko išsiųsti 2FA kodo.");
                        return Page();
                    }

                    TempData["TwoFACodeSent"] = true;
                    return Page();
                }
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


        public async Task<JsonResult> OnPostVerify2FACodeAsync([FromBody] TwoFACodeRequest request)
        {
            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                return new JsonResult(new { success = false, message = "Sesija negalioja!" });
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return new JsonResult(new { success = false, message = "Sesija negalioja!" });
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string getCodeQuery = @"
                        SELECT patvirtinimo_kodas, kodo_galiojimo_laikas, kodas_panaudotas 
                        FROM Dvieju_faktoriu_autentifikacija 
                        WHERE fk_Naudotojo_id = @userId 
                        ORDER BY kodo_issiuntimo_laikas DESC 
                        LIMIT 1";
            
                    using (var getCodeCommand = new MySqlCommand(getCodeQuery, connection))
                    {
                        getCodeCommand.Parameters.AddWithValue("@userId", userId);

                        using (var reader = await getCodeCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string? storedCode = reader.IsDBNull("patvirtinimo_kodas") ? (string?)null : reader.GetString("patvirtinimo_kodas");
                                DateTime? expirationTime = reader.IsDBNull("kodo_galiojimo_laikas") ? (DateTime?)null : reader.GetDateTime("kodo_galiojimo_laikas");
                                bool? codeUsed = reader.IsDBNull("kodas_panaudotas") ? (bool?)null : reader.GetBoolean("kodas_panaudotas");

                                if (storedCode == null || expirationTime == null)
                                {
                                    return new JsonResult(new { success = false, message = "Įvyko serverio klaida!" });
                                }

                                if (codeUsed == true)
                                {
                                    return new JsonResult(new { success = false, message = "2FA kodas jau panaudotas!" });
                                }

                                if (DateTime.Now > expirationTime)
                                {
                                    return new JsonResult(new { success = false, message = "2FA kodo galiojimas pasibaigęs!" });
                                }

                                if (storedCode == request.TwoFACode)
                                {
                                    await reader.CloseAsync();

                                    string updateCodeQuery = @"
                                        UPDATE Dvieju_faktoriu_autentifikacija 
                                        SET kodas_panaudotas = 1 
                                        WHERE fk_Naudotojo_id = @userId AND patvirtinimo_kodas = @code";
                            
                                    using (var updateCodeCommand = new MySqlCommand(updateCodeQuery, connection))
                                    {
                                        updateCodeCommand.Parameters.AddWithValue("@userId", userId);
                                        updateCodeCommand.Parameters.AddWithValue("@code", storedCode);
                                        await updateCodeCommand.ExecuteNonQueryAsync();
                                    }

                                    string hashedPassword = PasswordHelper.HashPassword(request.NewPassword);

                                    string updatePasswordQuery = "UPDATE Naudotojas SET slaptazodis = @newPassword WHERE naudotojo_id = @userId";
                                    using (var passwordUpdateCommand = new MySqlCommand(updatePasswordQuery, connection))
                                    {
                                        passwordUpdateCommand.Parameters.AddWithValue("@newPassword", hashedPassword);
                                        passwordUpdateCommand.Parameters.AddWithValue("@userId", userId);

                                        int rowsAffected = await passwordUpdateCommand.ExecuteNonQueryAsync();

                                        if (rowsAffected == 0)
                                        {
                                            return new JsonResult(new { success = false, message = "Serverio klaida - nepavyko atnaujinti el. pašto adreso!" });
                                        }
                                    }

                                    return new JsonResult(new { success = true });
                                }
                                else
                                {
                                    return new JsonResult(new { success = false, message = "Pateiktas neteisingas 2FA kodas!" });
                                }
                            }
                            else
                            {
                                return new JsonResult(new { success = false, message = "Įvyko serverio klaida!" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Įvyko serverio klaida: " + ex.Message });
            }
        }
    }
}