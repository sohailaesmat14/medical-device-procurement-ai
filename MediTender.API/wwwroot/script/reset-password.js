
document.addEventListener("DOMContentLoaded", () => {
    const storedEmail = sessionStorage.getItem("reset_email");
    if (storedEmail) {
        document.getElementById("emailInput").value = storedEmail;
    }
});

async function submitNewPassword() {
    const email = document.getElementById('emailInput').value.trim();
    const code = document.getElementById('resetCode').value.trim();
    const newPassword = document.getElementById('password').value;
    const errorMsg = document.getElementById('errorMsg');
    const btn = document.getElementById('submitBtn');

    if (!email) {
        errorMsg.innerText = "Please enter your email.";
        errorMsg.style.display = "block";
        return;
    }

    btn.disabled = true;
    btn.innerText = "Updating...";

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/reset-password`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email, code: code, newPassword: newPassword })
        });

        if (response.ok) {
            showToast("Password updated successfully! Please login with your new password.", "success");
            sessionStorage.removeItem("reset_email");
            setTimeout(() => window.location.replace("login.html"), 1500);
        } else {
            const errorData = await response.json().catch(() => ({}));
            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#fee2e2";
            errorMsg.style.color = "#dc2626";
            errorMsg.innerText = errorData.message || "Invalid or expired code.";
            errorMsg.style.display = "block";
            btn.disabled = false;
            btn.innerText = "Update Password";
        }
    } catch (error) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Server connection error.";
        errorMsg.style.display = "block";
        btn.disabled = false;
        btn.innerText = "Update Password";
    }
}

async function resendResetCode() {
    const email = document.getElementById('emailInput').value.trim();
    const resendBtn = document.getElementById('resendBtn');
    const errorMsg = document.getElementById('errorMsg');

    if (!email) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Please enter your email first to resend the code.";
        errorMsg.style.display = "block";
        return;
    }

    resendBtn.disabled = true;
    let timeLeft = 30; 
    
    const countdownInterval = setInterval(() => {
        resendBtn.innerText = `Wait ${timeLeft}s`;
        timeLeft--;
        if (timeLeft <= 0) {
            clearInterval(countdownInterval);
            resendBtn.innerText = "Resend Code";
            resendBtn.disabled = false;
        }
    }, 1000);

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/forgot-password`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email })
        });

        if (response.ok) {
            errorMsg.className = "error-msg"; 
            errorMsg.style.backgroundColor = "#d1fae5"; 
            errorMsg.style.color = "#059669";
            errorMsg.innerText = "A new reset code has been sent!";
            errorMsg.style.display = "block";
        } else {
            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#fee2e2";
            errorMsg.style.color = "#dc2626";
            errorMsg.innerText = "Failed to resend code.";
            errorMsg.style.display = "block";
        }
    } catch (error) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Server connection error.";
        errorMsg.style.display = "block";
    }
}    
