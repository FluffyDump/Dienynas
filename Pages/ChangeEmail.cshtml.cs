using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace RazorPages.Pages
{
    public class ChangeEmailModel : PageModel
    {
        [BindProperty]
        public string NewEmail { get; set; }

        public class TwoFACodeRequest
        {
            public string TwoFACode { get; set; }
            public string NewEmail { get; set; }
        }

        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;

        public ChangeEmailModel(TokenService tokenService, EmailService emailService)
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
                return RedirectToPage("/Login");
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return RedirectToPage("/Index");
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
                    await connection.OpenAsync();

                    string checkEmailQuery = "SELECT COUNT(*) FROM Naudotojas WHERE elektroninis_pastas = @newEmail AND naudotojo_id != @userId";
                    using (var checkEmailCommand = new MySqlCommand(checkEmailQuery, connection))
                    {
                        checkEmailCommand.Parameters.AddWithValue("@newEmail", NewEmail);
                        checkEmailCommand.Parameters.AddWithValue("@userId", userId);

                        int emailCount = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync());

                        if (emailCount > 0)
                        {
                            ModelState.AddModelError("", "Toks el. pašto adresas jau naudojamas kito vartotojo!");
                            return Page();
                        }
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

                    bool emailSent = await _emailService.SendEmailAsync(NewEmail, "Mokykla - 2FA kodas", $"Jūsų mokyklos 2FA kodas: {twoFACode}");

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

                                    string newEmail = request.NewEmail;

                                    string updateEmailQuery = "UPDATE Naudotojas SET elektroninis_pastas = @newEmail WHERE naudotojo_id = @userId";
                                    using (var emailUpdateCommand = new MySqlCommand(updateEmailQuery, connection))
                                    {
                                        emailUpdateCommand.Parameters.AddWithValue("@newEmail", newEmail);
                                        emailUpdateCommand.Parameters.AddWithValue("@userId", userId);

                                        int rowsAffected = await emailUpdateCommand.ExecuteNonQueryAsync();

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