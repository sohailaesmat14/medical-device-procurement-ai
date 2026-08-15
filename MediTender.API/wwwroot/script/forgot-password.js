async function requestReset() {
    const email = document.getElementById('email').value.trim();
    const msgBox = document.getElementById('msgBox');
    const btn = document.getElementById('submitBtn');

    btn.disabled = true;
    btn.innerText = "Sending...";

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/forgot-password`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({ email: email }),
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || "A server error occurred. Please try again later.");
            }

            sessionStorage.setItem("reset_email", email);
            window.location.replace("reset-password.html");
        
    } catch (error) {
        msgBox.className = "error-msg";
        msgBox.innerText = error.message || "Server connection error.";
        msgBox.style.display = "block";
        btn.disabled = false;
        btn.innerText = "Send Reset Code";
    }
}    
