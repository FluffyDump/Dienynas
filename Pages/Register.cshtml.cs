using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System.Data;
using System;

namespace RazorPages.Pages
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        public Naudotojas Input { get; set; }
        [BindProperty]
        public int RegistrationAttemptId { get; set; }
        [BindProperty]
        public int SessionAttemptId { get; set; }

        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        public RegisterModel(TokenService tokenService, EmailService emailService)
        {
            _tokenService = tokenService;
            Input = new Naudotojas();
            _emailService = emailService;
        }

        public class TwoFACodeRequest
        {
            public string TwoFACode { get; set; }
            public int RegistrationAttemptId { get; set; }
            public int SessionAttemptId { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Input.slaptazodis.Length < 6)
            {
                ModelState.AddModelError("", "Slaptažodis turi būti sudarytas iš bent 6 simbolių!");
                return Page();
            }

            string hashedPassword = PasswordHelper.HashPassword(Input.slaptazodis);

            var user = new Naudotojas
            {
                slapyvardis = Input.slapyvardis,
                elektroninis_pastas = Input.elektroninis_pastas,
                slaptazodis = hashedPassword,
                vardas = Input.vardas,
                pavarde = Input.pavarde,
                gimimo_data = Input.gimimo_data,
                mokymo_istaigos_pavadinimas = Input.mokymo_istaigos_pavadinimas,
                miestas = Input.miestas,
                adresas = Input.adresas,
                aprasymas = Input.aprasymas
            };

            string insertUserQuery = $"INSERT INTO Naudotojas (slapyvardis, elektroninis_pastas, slaptazodis, vardas, pavarde, gimimo_data, mokymo_istaigos_pavadinimas, miestas, adresas, aprasymas) VALUES ('{user.slapyvardis}', '{user.elektroninis_pastas}', '{user.slaptazodis}', '{user.vardas}', '{user.pavarde}', '{user.gimimo_data:yyyy-MM-dd}', '{user.mokymo_istaigos_pavadinimas}', '{user.miestas}', '{user.adresas}', '{user.aprasymas}'); SELECT LAST_INSERT_ID();";

            try
            {
                int newUserId;

                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();
                    using (var command = new MySqlCommand(insertUserQuery, connection))
                    {
                        newUserId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    if (newUserId > 0)
                    {
                        string insertRoleQuery = string.Empty;

                        switch (Input.profilio_tipas)
                        {
                            case "Mokinys":
                                insertRoleQuery = $"INSERT INTO Mokinys (naudotojo_id) VALUES ({newUserId});";
                                break;
                            case "Mokytojas":
                                insertRoleQuery = $"INSERT INTO Mokytojas (naudotojo_id) VALUES ({newUserId});";
                                break;
                            case "Administratorius":
                                insertRoleQuery = $"INSERT INTO Administratorius (naudotojo_id) VALUES ({newUserId});";
                                    break;
                            default:
                                throw new ArgumentException("Netinkamas profilio tipas!");
                        }

                        using (var command = new MySqlCommand(insertRoleQuery, connection))
                        {
                            command.ExecuteNonQuery();
                        }

                        string insertLoginQuery = @"
                        INSERT INTO Prisijungimas (sesijos_pradzios_laikas, naudotojas_autentifikuotas, fk_Naudotojo_id) 
                        VALUES (ADDTIME(NOW(), '02:00:00'), @isAuthenticated, @userId);
                        SELECT LAST_INSERT_ID();";

                        using (var command = new MySqlCommand(insertLoginQuery, connection))
                        {
                            command.Parameters.AddWithValue("@isAuthenticated", false);
                            command.Parameters.AddWithValue("@userId", newUserId);

                            var result = await command.ExecuteScalarAsync();

                            SessionAttemptId = Convert.ToInt32(result);
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
                            saveCodeCommand.Parameters.AddWithValue("@userId", newUserId);
                            await saveCodeCommand.ExecuteNonQueryAsync();
                        }

                        bool emailSent = await _emailService.SendEmailAsync(Input.elektroninis_pastas, "Mokykla - 2FA kodas", $"Jūsų mokyklos 2FA kodas: {twoFACode}");

                        if (!emailSent)
                        {
                            ModelState.AddModelError("", "Nepavyko išsiųsti 2FA kodo.");
                            return Page();
                        }

                        TempData["TwoFACodeSent"] = true;

                        RegistrationAttemptId = newUserId;

                        return Page();
                    }
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Message.Contains("uc_slapyvardis"))
                {
                    ModelState.AddModelError(string.Empty, "Slapyvardis jau užimtas!");
                }
                else if (ex.Message.Contains("uc_elektroninis_pastas"))
                {
                    ModelState.AddModelError(string.Empty, "Elektroninis paštas jau užimtas!");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Įvyko serverio klaida!" + ex.Message);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Įvyko serverio klaida!" + ex.Message);
            }
            return Page();
        }

        public async Task<JsonResult> OnPostVerify2FACodeAsync([FromBody] TwoFACodeRequest request)
        {
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
                        getCodeCommand.Parameters.AddWithValue("@userId", request.RegistrationAttemptId);

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
                                        updateCodeCommand.Parameters.AddWithValue("@userId", request.RegistrationAttemptId);
                                        updateCodeCommand.Parameters.AddWithValue("@code", storedCode);
                                        await updateCodeCommand.ExecuteNonQueryAsync();
                                    }

                                    string updateLoginQuery = @"
                                    UPDATE Prisijungimas 
                                    SET sesijos_pradzios_laikas = ADDTIME(NOW(), '02:00:00'), naudotojas_autentifikuotas = @isAuthenticated 
                                    WHERE autentifikacijos_id = @loginAttemptId";

                                    using (var command = new MySqlCommand(updateLoginQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@isAuthenticated", true);
                                        command.Parameters.AddWithValue("@loginAttemptId", request.SessionAttemptId);

                                        int rowsAffected = await command.ExecuteNonQueryAsync();

                                        if (rowsAffected == 0)
                                        {
                                            return new JsonResult(new { success = false, message = "Serverio klaida - atnaujinti duomenų!" });
                                        }

                                        var token = _tokenService.GenerateToken(request.RegistrationAttemptId);
                                        HttpContext.Response.Cookies.Append("access_token", token);

                                        return new JsonResult(new { success = true });
                                    }
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