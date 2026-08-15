
document.addEventListener("DOMContentLoaded", () => {
    const storedEmail = sessionStorage.getItem("temp_user_email");
    if (storedEmail) {
        document.getElementById("emailInput").value = storedEmail;
    }
});

async function verifyEmail() {
    const code = document.getElementById('verificationCode').value.trim();
    const email = document.getElementById('emailInput').value.trim();
    const errorMsg = document.getElementById('errorMsg');
    const btn = document.getElementById('verifyBtn');

    if (!email) {
        errorMsg.innerText = "Please enter your email.";
        errorMsg.style.display = "block";
        return;
    }

    btn.disabled = true;
    btn.innerText = "Verifying...";

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/verify-email`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email, code: code })
        });

        if (response.ok) {
            const data = await response.json();
            sessionStorage.setItem("jwt_token", data.token);
            sessionStorage.setItem("user_email", email); 
            sessionStorage.removeItem("temp_user_email"); 
            window.location.replace("subscription.html");
        } else {
            const errorData = await response.json().catch(() => ({}));
            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#fee2e2";
            errorMsg.style.color = "#dc2626";
            errorMsg.innerText = errorData.message || "Invalid code.";
            errorMsg.style.display = "block";
            btn.disabled = false;
            btn.innerText = "Verify & Continue";
        }
    } catch (error) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Server connection error.";
        errorMsg.style.display = "block";
        btn.disabled = false;
        btn.innerText = "Verify & Continue";
    }
}

async function resendCode() {
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
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/resend-verification`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email })
        });

        if (response.ok) {
            errorMsg.className = "error-msg"; 
            errorMsg.style.backgroundColor = "#d1fae5"; 
            errorMsg.style.color = "#059669";
            errorMsg.innerText = "A new code has been sent to your email!";
            errorMsg.style.display = "block";
        } else {
            const errorData = await response.json().catch(() => ({}));
            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#fee2e2";
            errorMsg.style.color = "#dc2626";
            errorMsg.innerText = errorData.message || "Failed to resend code.";
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
