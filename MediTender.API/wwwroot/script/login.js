async function performLogin() {
    const user = document.getElementById('username').value;
    const pass = document.getElementById('password').value;
    const errorMsg = document.getElementById('errorMsg');
    const loginBtn = document.querySelector('.login-btn');

    loginBtn.disabled = true;
    loginBtn.innerText = "Signing in...";

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username: user, password: pass })
        });

        if (response.ok) {
            const data = await response.json();
            sessionStorage.setItem("jwt_token", data.token);
            sessionStorage.setItem("user_name", data.fullName || user);
            sessionStorage.setItem("user_email", user);
            
            if (data.plan && data.plan.trim() !== "") {
                sessionStorage.setItem("meditender_plan", data.plan);
                window.location.replace("upload.html");
            } else {
                window.location.replace("subscription.html");
            }
        } else {
            const errorData = await response.json().catch(() => ({}));
            
            if (response.status === 403 && errorData.requiresVerification) {
                sessionStorage.setItem("temp_user_email", user);
                errorMsg.className = "error-msg";
                errorMsg.style.backgroundColor = "#fef3c7";
                errorMsg.style.color = "#d97706";
                errorMsg.innerText = "Account not verified. Redirecting to verification...";
                errorMsg.style.display = "block";
                setTimeout(() => window.location.replace("verify-email.html"), 2000);
            } else {
                errorMsg.className = "error-msg";
                errorMsg.style.backgroundColor = "#fee2e2";
                errorMsg.style.color = "#dc2626";
                errorMsg.innerText = errorData.message || "Invalid credentials. Try again.";
                errorMsg.style.display = "block";
                loginBtn.disabled = false;
                loginBtn.innerText = "Sign In";
            }
        }
    } catch (error) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Server connection error.";
        errorMsg.style.display = "block";
        loginBtn.disabled = false;
        loginBtn.innerText = "Sign In";
    }
}    
