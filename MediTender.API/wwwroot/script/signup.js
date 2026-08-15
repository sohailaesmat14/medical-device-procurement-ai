
async function performSignup() {
    const nameEl = document.getElementById('fullname');       
    const emailEl = document.getElementById('email');         
    const passwordEl = document.getElementById('password');
    const confirmEl = document.getElementById('confirmPassword');
    const errorMsg = document.getElementById('errorMsg');
    const signupBtn = document.querySelector('.auth-btn'); 

    if (!nameEl || !emailEl || !passwordEl || !confirmEl) return;

    const name = nameEl.value.trim();
    const email = emailEl.value.trim();
    const password = passwordEl.value;
    const confirm = confirmEl.value;

    if (password !== confirm) {
        errorMsg.innerText = "Passwords do not match.";
        errorMsg.style.display = "block";
        return;
    }

    signupBtn.disabled = true;
    signupBtn.innerText = "Creating Account... ⏳";
    signupBtn.style.opacity = "0.7";
    errorMsg.style.display = "none";

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/signup`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fullName: name, email: email, password: password })
        });

        if (response.ok) {
            sessionStorage.setItem("temp_user_email", email);

            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#d1fae5";
            errorMsg.style.color = "#059669";
            errorMsg.innerText = "Account created successfully! Redirecting to verification...";
            errorMsg.style.display = "block";

            setTimeout(() => {
                window.location.replace("verify-email.html");
            }, 2000);

        } else {
            const errorData = await response.json();

            if (errorData.errors) {
                const firstErrorKey = Object.keys(errorData.errors)[0];
                errorMsg.innerText = errorData.errors[firstErrorKey][0];
            } else if (errorData.message) {
                errorMsg.innerText = errorData.message;
            } else {
                errorMsg.innerText = "Failed to create account. Please check your inputs.";
            }

            errorMsg.className = "error-msg";
            errorMsg.style.backgroundColor = "#fee2e2";
            errorMsg.style.color = "#dc2626";
            errorMsg.style.display = "block";

            signupBtn.disabled = false;
            signupBtn.innerText = "Sign Up & Continue";
            signupBtn.style.opacity = "1";
        }
    } catch (error) {
        errorMsg.className = "error-msg";
        errorMsg.style.backgroundColor = "#fee2e2";
        errorMsg.style.color = "#dc2626";
        errorMsg.innerText = "Server connection error.";
        errorMsg.style.display = "block";

        signupBtn.disabled = false;
        signupBtn.innerText = "Sign Up & Continue";
        signupBtn.style.opacity = "1";
    }
}    
