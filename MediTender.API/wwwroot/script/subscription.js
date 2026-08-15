
document.addEventListener("DOMContentLoaded", () => {
    const currentPlan = sessionStorage.getItem("meditender_plan");
    
    if (currentPlan) {
        const allBtns = document.querySelectorAll('.subscribe-btn');
        allBtns.forEach(btn => {
            if (btn.getAttribute('onclick') && btn.getAttribute('onclick').includes(`'${currentPlan}'`)) {
                btn.innerText = "Current Plan";
                btn.disabled = true;
                btn.style.backgroundColor = "#94a3b8";
                btn.style.cursor = "not-allowed";
            }
        });
    }
});

async function selectPlan(planType, btnElement) {
    const email = sessionStorage.getItem("user_email");
    const token = sessionStorage.getItem("jwt_token");

    if (!email || !token) {
        showToast("Please log in first.", "warning");
        setTimeout(() => window.location.replace("login.html"), 1500);
        return;
    }

    const allBtns = document.querySelectorAll('.subscribe-btn');
    allBtns.forEach(btn => btn.disabled = true);
    
    const originalText = btnElement.innerText;
    btnElement.innerText = "Processing... ⏳";

    const resetButtons = () => {
        allBtns.forEach(btn => {
            const savedPlan = sessionStorage.getItem("meditender_plan");
            if (savedPlan && btn.getAttribute('onclick') && btn.getAttribute('onclick').includes(`'${savedPlan}'`)) {
                return;
            }
            btn.disabled = false;
        });
        btnElement.innerText = originalText;
    };

    try {
        if (planType === 'free') {
            const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Auth/update-plan`, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ email: email, plan: planType })
            });

            if (response.ok) {
                const data = await response.json().catch(() => ({}));
                showToast(data.message || "Redirecting...", "success");
                sessionStorage.removeItem("meditender_cached_quota");
                sessionStorage.setItem("meditender_plan", "free");
                setTimeout(() => window.location.replace("upload.html"), 1500);
            } else {
                const errorData = await response.json().catch(() => ({}));
                showToast(errorData.message || "Failed to update subscription plan.", "error");
                resetButtons();
            }
            
        } else {
            const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Payment/initiate`, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ planType: planType })
            });

            if (response.ok) {
                const data = await response.json();
                window.location.replace(data.checkoutUrl); 
            } else {
                const errorData = await response.json().catch(() => ({}));
                showToast(errorData.message || "Failed to initiate payment.", "error");
                resetButtons();
            }
        }
    } catch (error) {
        showToast(error.message || "Server connection error.", "error");
        resetButtons();
    }
}
