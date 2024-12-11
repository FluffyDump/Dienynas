using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System;

namespace RazorPages.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly TokenService _tokenService;
        public RegisterModel(TokenService tokenService)
        {
            _tokenService = tokenService;
            Input = new Naudotojas();
        }

        [BindProperty]
        public Naudotojas Input { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
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
                profilio_nuotrauka = Input.profilio_nuotrauka,
                aprasymas = Input.aprasymas,
                viesa_kontaktine_informacija = Input.viesa_kontaktine_informacija
            };

            string insertUserQuery = $"INSERT INTO Naudotojas (slapyvardis, elektroninis_pastas, slaptazodis, vardas, pavarde, gimimo_data, mokymo_istaigos_pavadinimas, miestas, adresas, profilio_nuotrauka, aprasymas, viesa_kontaktine_informacija) VALUES ('{user.slapyvardis}', '{user.elektroninis_pastas}', '{user.slaptazodis}', '{user.vardas}', '{user.pavarde}', '{user.gimimo_data:yyyy-MM-dd}', '{user.mokymo_istaigos_pavadinimas}', '{user.miestas}', '{user.adresas}', '{user.profilio_nuotrauka}', '{user.aprasymas}', '{user.viesa_kontaktine_informacija}'); SELECT LAST_INSERT_ID();";

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
                        string insertMokinysQuery = $"INSERT INTO Mokinys (naudotojo_id) VALUES ({newUserId})";
                        using (var command = new MySqlCommand(insertMokinysQuery, connection))
                        {
                            command.ExecuteNonQuery();
                        }

                        string insertPrisijungimasQuery = $"INSERT INTO Prisijungimas (sesijos_pradzios_laikas, naudotojas_autentifikuotas, fk_Naudotojo_id) VALUES (NOW(), True, {newUserId})";
                        using (var command = new MySqlCommand(insertPrisijungimasQuery, connection))
                        {
                            command.ExecuteNonQuery();
                        }

                        var token = _tokenService.GenerateToken(newUserId);
                        HttpContext.Response.Cookies.Append("access_token", token);

                        return RedirectToPage("/Profile");
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
    }
}