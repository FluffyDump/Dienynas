function show2fa() {
    // Show the 2FA overlay when "Prisijungti" button is clicked
    document.getElementById("2faOverlay").style.display = "block";
}

function validate2fa(redirectUrl) {
    // Check if there's any value entered in the 2FA code field
    if (document.getElementById("2faCode").value.trim()) {
        window.location.href = redirectUrl;
    }
}

function showLogout() {
    document.getElementById("logoutOverlay").style.display = "block";
}

function hideLogout() {
    document.getElementById("logoutOverlay").style.display = "none";
}

function confirmLogout() {
    window.location.href = '/Index';
}

function saveAndRedirect() {
    // Perform any save actions here (if needed, in future logic)

    // Redirect to profile view page
    window.location.href = '/Profile';
}