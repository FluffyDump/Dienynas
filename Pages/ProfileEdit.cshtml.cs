using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dienynas.Models;
using MySql.Data.MySqlClient;
using System;
using System.IO;

namespace RazorPages.Pages
{
    public class EditProfileModel : PageModel
    {
        [BindProperty]
        [ValidateNever]
        public string? NewUsername { get; set; }
        [BindProperty]
        [ValidateNever]
        public string? NewDescription { get; set; }
        [BindProperty]
        [ValidateNever]
        public IFormFile? ProfilePicture { get; set; }

        public string CurrentUsername { get; set; }
        public string CurrentDescription { get; set; }

        private readonly TokenService _tokenService;

        public EditProfileModel(TokenService tokenService)
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

            string query = "SELECT slapyvardis, aprasymas FROM Naudotojas WHERE naudotojo_id = @userId";

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
                                CurrentUsername = reader["slapyvardis"]?.ToString() ?? "-";
                                CurrentDescription = reader["aprasymas"]?.ToString() ?? "-";
                            }
                            else
                            {
                                ModelState.AddModelError("", "Vartotojas nerastas.");
                                return;
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

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    string query = "SELECT slapyvardis, aprasymas FROM Naudotojas WHERE naudotojo_id = @userId";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                CurrentUsername = reader["slapyvardis"]?.ToString() ?? "-";
                                CurrentDescription = reader["aprasymas"]?.ToString() ?? "-";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(NewUsername))
                    {
                        string checkQuery = "SELECT COUNT(*) FROM Naudotojas WHERE slapyvardis = @newUsername AND naudotojo_id != @userId";
                        using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@newUsername", NewUsername);
                            checkCommand.Parameters.AddWithValue("@userId", userId);

                            var count = Convert.ToInt32(checkCommand.ExecuteScalar());
                            if (count > 0)
                            {
                                ModelState.AddModelError("", "Toks slapyvardis jau naudojamas kito vartotojo!");
                                return Page();
                            }
                        }

                        string updateUsernameQuery = "UPDATE Naudotojas SET slapyvardis = @newUsername WHERE naudotojo_id = @userId";
                        using (var updateCommand = new MySqlCommand(updateUsernameQuery, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@newUsername", NewUsername);
                            updateCommand.Parameters.AddWithValue("@userId", userId);
                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrEmpty(NewDescription))
                    {
                        string updateDescriptionQuery = "UPDATE Naudotojas SET aprasymas = @newDescription WHERE naudotojo_id = @userId";
                        using (var updateCommand = new MySqlCommand(updateDescriptionQuery, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@newDescription", NewDescription);
                            updateCommand.Parameters.AddWithValue("@userId", userId);
                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    if (ProfilePicture != null)
                    {
                        var fileExtension = Path.GetExtension(ProfilePicture.FileName).ToLower();
                        if (fileExtension != ".png")
                        {
                            ModelState.AddModelError("", "Pateiktos nuotraukos formatas nėra .png!");
                            return Page();
                        }

                        var fileMimeType = ProfilePicture.ContentType;
                        if (fileMimeType != "image/png")
                        {
                            ModelState.AddModelError("", "Pateiktos nuotraukos formatas nėra .png!");
                            return Page();
                        }

                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var filePath = Path.Combine(uploadsFolder, $"{userId}.png");
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await ProfilePicture.CopyToAsync(stream);
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

            return RedirectToPage("/Profile");
        }
    }
}