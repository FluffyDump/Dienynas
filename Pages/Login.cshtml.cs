using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace RazorPages.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string UsernameOrEmail { get; set; }
        [BindProperty]
        public string Password { get; set; }
        [BindProperty]
        public int LoginAttemptId { get; set; }

        public class TwoFACodeRequest
        {
            public string TwoFACode { get; set; }
            public string UsernameOrEmail { get; set; }
            public string Password { get; set; }
            public int LoginAttemptId { get; set; }
        }

        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;

        public LoginModel(TokenService tokenService, EmailService emailService)
        {
            _tokenService = tokenService;
            _emailService = emailService;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            string getUserQuery = "SELECT naudotojo_id, slaptazodis, elektroninis_pastas FROM Naudotojas WHERE slapyvardis = @usernameOrEmail OR elektroninis_pastas = @usernameOrEmail";

            try
            {
                int? userId = null;
                string storedHashedPassword = null;
                string userEmail = null;

                using (var connection = DatabaseHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    using (var command = new MySqlCommand(getUserQuery, connection))
                    {
                        command.Parameters.AddWithValue("@usernameOrEmail", UsernameOrEmail);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetInt32("naudotojo_id");
                                storedHashedPassword = reader.GetString("slaptazodis");
                                userEmail = reader.GetString("elektroninis_pastas");
                            }
                        }
                    }

                    bool isAuthenticated = false;

                    if (userId.HasValue && storedHashedPassword != null)
                    {
                        isAuthenticated = PasswordHelper.VerifyPassword(Password, storedHashedPassword);
                    }

                    string insertLoginQuery = @"
                    INSERT INTO Prisijungimas (sesijos_pradzios_laikas, naudotojas_autentifikuotas, fk_Naudotojo_id) 
                    VALUES (ADDTIME(NOW(), '02:00:00'), @isAuthenticated, @userId);
                    SELECT LAST_INSERT_ID();";  // Get the last inserted ID

                    using (var command = new MySqlCommand(insertLoginQuery, connection))
                    {
                        command.Parameters.AddWithValue("@isAuthenticated", false);
                        command.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);

                        var result = await command.ExecuteScalarAsync();

                        LoginAttemptId = Convert.ToInt32(result);
                    }


                    if (!isAuthenticated)
                    {
                        ModelState.AddModelError(string.Empty, "Neteisingas vartotojo vardas, el. pašto adresas arba slaptažodis.");
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

        public async Task<JsonResult> OnPostVerify2FACodeAsync([FromBody] TwoFACodeRequest request)
        {
            try
            {
                bool isAuthenticated = false;
                using (var connection = DatabaseHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string getUserQuery = @"
                        SELECT naudotojo_id, slaptazodis 
                        FROM Naudotojas 
                        WHERE slapyvardis = @usernameOrEmail OR elektroninis_pastas = @usernameOrEmail";

                    int? userId = null;
                    string storedHashedPassword = null;

                    using (var getUserCommand = new MySqlCommand(getUserQuery, connection))
                    {
                        getUserCommand.Parameters.AddWithValue("@usernameOrEmail", request.UsernameOrEmail);
                
                        using (var reader = await getUserCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetInt32("naudotojo_id");
                                storedHashedPassword = reader.GetString("slaptazodis");
                            }
                            else
                            {
                                return new JsonResult(new { success = false, message = "Neteisingas vartotojo vardas arba el. pašto adresas." });
                            }
                        }
                    }

                    if (userId.HasValue && storedHashedPassword != null)
                    {
                        isAuthenticated = PasswordHelper.VerifyPassword(request.Password, storedHashedPassword);
                        if (!isAuthenticated)
                        {
                            return new JsonResult(new { success = false, message = "Neteisingas slaptažodis." });
                        }
                    }

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

                                    var token = _tokenService.GenerateToken(userId.Value);
                                    HttpContext.Response.Cookies.Append("access_token", token);

                                    string updateLoginQuery = @"
                                    UPDATE Prisijungimas 
                                    SET sesijos_pradzios_laikas = ADDTIME(NOW(), '02:00:00'), naudotojas_autentifikuotas = @isAuthenticated 
                                    WHERE autentifikacijos_id = @loginAttemptId";

                                    using (var command = new MySqlCommand(updateLoginQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@isAuthenticated", true);
                                        command.Parameters.AddWithValue("@loginAttemptId", request.LoginAttemptId);

                                        int rowsAffected = await command.ExecuteNonQueryAsync();

                                        if (rowsAffected == 0)
                                        {
                                            return new JsonResult(new { success = false, message = "Serverio klaida - atnaujinti duomenų!" });
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