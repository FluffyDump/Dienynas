using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using RazorPages.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RazorPages.Pages
{
    public class NewMessageModel : PageModel
    {
        private readonly TokenService _tokenService;
        private readonly ILogger<NewMessageModel> _logger;

        public NewMessageModel(TokenService tokenService, ILogger<NewMessageModel> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        [BindProperty]
        [Display(Name = "Gavëjo Vartotojo Vardas")]
        [StringLength(50, ErrorMessage = "Gavëjo vardas negali bûti ilgesnis nei 50 simboliø.")]
        public string? RecipientUsername { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Þinutës pavadinimas yra privalomas.")]
        [Display(Name = "Þinutës Pavadinimas")]
        [StringLength(100, ErrorMessage = "Þinutës pavadinimas negali bûti ilgesnis nei 100 simboliø.")]
        public string Title { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Þinutës turinys yra privalomas.")]
        [Display(Name = "Þinutës Turinys")]
        [StringLength(1000, ErrorMessage = "Þinutës turinys negali bûti ilgesnis nei 1000 simboliø.")]
        public string MessageBody { get; set; }

        [BindProperty]
        public bool SendToClass { get; set; }

        [BindProperty]
        public int? SelectedClassId { get; set; }

        public bool IsTeacher { get; private set; }
        public List<Klase> AvailableClasses { get; private set; } = new List<Klase>();

        public void OnGet()
        {
            CheckTeacherStatus();

            if (IsTeacher)
            {
                LoadAvailableClasses();
            }

            if (Request.Query.ContainsKey("recipient"))
            {
                RecipientUsername = Request.Query["recipient"].ToString();
            }
        }


        private void CheckTeacherStatus()
        {
            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                IsTeacher = false;
                return;
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            if (userId == null)
            {
                IsTeacher = false;
                return;
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string checkUserProfileQuery = @"
            SELECT 
                CASE
                    WHEN EXISTS (SELECT 1 FROM Mokinys WHERE naudotojo_id = @userId) THEN 'Mokinys'
                    WHEN EXISTS (SELECT 1 FROM Mokytojas WHERE naudotojo_id = @userId) THEN 'Mokytojas'
                    WHEN EXISTS (SELECT 1 FROM Administratorius WHERE naudotojo_id = @userId) THEN 'Administratorius'
                    ELSE 'Neþinomas'
                END AS profilio_tipas;";

                using (var command = new MySqlCommand(checkUserProfileQuery, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    var profileType = command.ExecuteScalar()?.ToString();

                    IsTeacher = profileType == "Mokytojas";
                }
            }
        }


        private void LoadAvailableClasses()
        {
            var token = Request.Cookies["access_token"];
            var userId = _tokenService.GetUserIdFromToken(token);

            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string fetchClassesQuery = @"
                    SELECT DISTINCT k.klases_id, k.pavadinimas 
                    FROM Klase k
                    JOIN Pamoka p ON p.fk_klases_id = k.klases_id
                    WHERE p.fk_mokytojo_id = @userId";

                using (var command = new MySqlCommand(fetchClassesQuery, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AvailableClasses.Add(new Klase
                            {
                                klases_id = Convert.ToInt32(reader["klases_id"]),
                                pavadinimas = reader["pavadinimas"].ToString()
                            });
                        }
                    }
                }
            }
        }

        public IActionResult OnPost()
        {
            var token = Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Attempted to send message without authentication");
                return RedirectToPage("/Login");
            }

            var senderIdObj = _tokenService.GetUserIdFromToken(token);
            if (senderIdObj == null)
            {
                _logger.LogWarning("Invalid token when attempting to send message");
                return RedirectToPage("/Login");
            }

            string senderId = senderIdObj.ToString();

            CheckTeacherStatus();
            if (SendToClass && !IsTeacher)
            {
                ModelState.AddModelError("", "Tik mokytojai gali siøsti þinutes visai klasei.");
                return Page();
            }

            if (!ModelState.IsValid)
            {
                if (IsTeacher)
                {
                    LoadAvailableClasses();
                }
                return Page();
            }

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    if (SendToClass)
                    {
                        if (!SelectedClassId.HasValue)
                        {
                            ModelState.AddModelError("SelectedClassId", "Privalote pasirinkti klasæ.");
                            LoadAvailableClasses();
                            return Page();
                        }

                        SendMessageToClass(connection, senderId, SelectedClassId.Value);
                    }
                    else
                    {
                        int recipientId = ValidateRecipient(connection, RecipientUsername);

                        if (recipientId == Convert.ToInt32(senderId))
                        {
                            ModelState.AddModelError("RecipientUsername", "Negalite siøsti þinuèiø sau paèiam.");
                            return Page();
                        }

                        InsertMessage(connection, senderId, recipientId, null);
                    }

                    _logger.LogInformation($"Message sent from user {senderId}");

                    TempData["SuccessMessage"] = SendToClass
                        ? "Þinutë sëkmingai iðsiøsta visai klasei!"
                        : "Þinutë sëkmingai iðsiøsta!";
                    return RedirectToPage("/MessageList");
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Database error when sending message");
                ModelState.AddModelError("", "Klaida duomenø bazëje: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when sending message");
                ModelState.AddModelError("", "Netikëta klaida: " + ex.Message);
            }

            if (IsTeacher)
            {
                LoadAvailableClasses();
            }
            return Page();
        }

        private void SendMessageToClass(MySqlConnection connection, string senderId, int classId)
        {
            string fetchStudentsQuery = @"
                SELECT n.naudotojo_id 
                FROM Naudotojas n
                JOIN Mokinys m ON n.naudotojo_id = m.naudotojo_id
                WHERE m.fk_klases_id = @classId";

            using (var studentsCommand = new MySqlCommand(fetchStudentsQuery, connection))
            {
                studentsCommand.Parameters.AddWithValue("@classId", classId);
                using (var studentsReader = studentsCommand.ExecuteReader())
                {
                    var studentIds = new List<int>();
                    while (studentsReader.Read())
                    {
                        studentIds.Add(Convert.ToInt32(studentsReader["naudotojo_id"]));
                    }
                    studentsReader.Close();

                    foreach (var studentId in studentIds)
                    {
                        InsertMessage(connection, senderId, studentId, classId);
                    }
                }
            }
        }

        private int ValidateRecipient(MySqlConnection connection, string username)
        {
            string fetchRecipientQuery = "SELECT naudotojo_id FROM Naudotojas WHERE slapyvardis = @username";
            using (var command = new MySqlCommand(fetchRecipientQuery, connection))
            {
                command.Parameters.AddWithValue("@username", username);
                var result = command.ExecuteScalar();

                if (result == null)
                {
                    throw new InvalidOperationException("Gavëjas su tokiu vardu nerastas.");
                }

                return Convert.ToInt32(result);
            }
        }

        private void InsertMessage(MySqlConnection connection, string senderId, int recipientId, int? classId)
        {
            Priotitetas priotitetas = Priotitetas.Zemas;
            string insertQuery = @"
        INSERT INTO Zinute (pavadinimas, turinys, s_data, skaityta, prioritetas, fk_Siuntejo_id, fk_Gavejo_id, fk_klases_id)
        VALUES (@pavadinimas, @turinys, @date, @skaityta, @prioritetas, @senderId, @recipientId, @classId);
        ";

            using (var command = new MySqlCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@pavadinimas", Title);
                command.Parameters.AddWithValue("@turinys", MessageBody);
                command.Parameters.AddWithValue("@date", DateTime.Now);
                command.Parameters.AddWithValue("@skaityta", false);
                command.Parameters.AddWithValue("@prioritetas", priotitetas.ToString()); 
                command.Parameters.AddWithValue("@senderId", senderId);
                command.Parameters.AddWithValue("@recipientId", recipientId);
                command.Parameters.AddWithValue("@classId", classId ?? (object)DBNull.Value);

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException("Nepavyko iðsaugoti þinutës.");
                }
            }
        }

    }
}