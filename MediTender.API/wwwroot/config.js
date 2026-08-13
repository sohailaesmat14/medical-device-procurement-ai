const CONFIG = {
    API_BASE_URL:
        window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
            ? "http://localhost:5172"
            : window.location.origin,
            
    BILLING: {
        FREE_QUOTA: 200,
        MONTHLY_QUOTA: 2000,
        ANNUAL_QUOTA: 99999,
        PER_VENDOR_COST: 15
    }
};

fetch(`${CONFIG.API_BASE_URL}/api/Auth/billing-config`)
    .then(res => res.json())
    .then(data => {
        CONFIG.BILLING.FREE_QUOTA = data.freeTrialQuota;
        CONFIG.BILLING.MONTHLY_QUOTA = data.monthlyQuota;
        CONFIG.BILLING.ANNUAL_QUOTA = data.annualQuota;
        CONFIG.BILLING.PER_VENDOR_COST = data.perVendorCost;
    })
    .catch(() => {});

const style = document.createElement('style');
style.innerHTML = `
.toast-container { position: fixed; top: 20px; right: 20px; z-index: 9999; display: flex; flex-direction: column; gap: 10px; }
.toast { background-color: #1e293b; color: white; padding: 15px 20px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); font-size: 14px; display: flex; align-items: center; gap: 10px; opacity: 0; transform: translateX(100%); animation: slideIn 0.3s forwards; min-width: 250px; max-width: 350px; font-family: system-ui, -apple-system, sans-serif;}
.toast.success { border-left: 4px solid #10b981; }
.toast.error { border-left: 4px solid #ef4444; }
.toast.info { border-left: 4px solid #3b82f6; }
.toast.warning { border-left: 4px solid #f59e0b; }
.toast-close { margin-left: auto; cursor: pointer; font-weight: bold; color: #94a3b8; }
.toast-close:hover { color: white; }
@keyframes slideIn { to { transform: translateX(0); opacity: 1; } }
@keyframes fadeOut { to { opacity: 0; transform: translateX(10px); } }
`;
document.head.appendChild(style);

window.showToast = function(message, type = 'info') {
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container';
        document.body.appendChild(container);
    }
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    let icon = type === 'success' ? '✅' : type === 'error' ? '❌' : type === 'warning' ? '⚠️' : 'ℹ️';
    toast.innerHTML = `<span>${icon}</span><span style="flex-grow: 1;">${message}</span><span class="toast-close" onclick="this.parentElement.remove()">✕</span>`;
    container.appendChild(toast);
    setTimeout(() => {
        toast.style.animation = 'fadeOut 0.3s forwards';
        setTimeout(() => toast.remove(), 300);
    }, 4000);
};

async function fetchWithTimeout(url, options = {}) {
    const timeout = options.timeout || 60000;
    const controller = new AbortController();
    const id = setTimeout(() => controller.abort(), timeout);
    try {
        const response = await fetch(url, { ...options, signal: controller.signal });
        clearTimeout(id);
        
        if (response.status === 401 && !url.includes('/api/Auth/')) {
            performLogout();
            return Promise.reject(new Error("Session expired. Please log in again."));
        }
        
        return response;
    } catch (error) {
        clearTimeout(id);
        throw error;
    }
}

window.performLogout = function() {
    sessionStorage.clear();
    window.location.replace("login.html");
}

window.togglePasswordVisibility = function (inputId, button) {
    const input = document.getElementById(inputId);
    if (input && input.type === "password") {
        input.type = "text";
        button.innerText = "🙈";
    } else if (input) {
        input.type = "password";
        button.innerText = "👁️";
    }
};

document.addEventListener("DOMContentLoaded", async () => {
    const token = sessionStorage.getItem("jwt_token");
    const currentPage = window.location.pathname.toLowerCase();
    
    const isHomePage = currentPage.includes("home.html") || currentPage.includes("index.html") || currentPage === "/" || currentPage.endsWith("wwwroot/");

    // --- HOME PAGE LOGIC ---
    if (isHomePage && token) {
        const userName = sessionStorage.getItem("user_name") || "Committee Member";
        const initials = userName.trim().split(/\s+/).map(n => n[0]).join('').substring(0, 2).toUpperCase();

        // 1. Replace Top-Right Buttons (Sign In / Get Started) safely
        const loginLink = Array.from(document.querySelectorAll('a')).find(a => 
            a.innerText.trim() === 'Sign In' || (a.href && a.href.includes('login.html'))
        );
        const signupLink = Array.from(document.querySelectorAll('a')).find(a => 
            a.innerText.trim() === 'Get Started' || (a.href && a.href.includes('signup.html'))
        );

        if (loginLink) {
            const profileDiv = document.createElement('div');
            profileDiv.style.cssText = 'display: flex; align-items: center; gap: 15px;';
            profileDiv.innerHTML = `
                <div style="display: flex; align-items: center; gap: 8px; background: rgba(255,255,255,0.1); padding: 5px 15px 5px 5px; border-radius: 30px; border: 1px solid rgba(255,255,255,0.2);">
                    <div style="background-color: #10b981; color: white; width: 32px; height: 32px; border-radius: 50%; display: flex; justify-content: center; align-items: center; font-weight: bold; font-size: 13px;">${initials}</div>
                    <span style="font-weight: bold; font-size: 14px; color: white;">${userName}</span>
                </div>
                <a href="upload.html" style="background-color: #2563eb; color: white; padding: 10px 20px; border-radius: 8px; text-decoration: none; font-weight: bold; box-shadow: 0 4px 6px rgba(37, 99, 235, 0.2); transition: 0.3s;">Dashboard 🚀</a>
                <button onclick="performLogout()" style="background: none; border: none; color: #ef4444; cursor: pointer; font-size: 20px; padding: 0; margin-left: 5px;" title="Logout">🚪</button>
            `;

            if (signupLink && signupLink.parentElement === loginLink.parentElement) {
                signupLink.replaceWith(profileDiv);
                loginLink.remove();
            } else {
                loginLink.replaceWith(profileDiv);
                if(signupLink) signupLink.remove();
            }
        }

        // 2. Update Hero Buttons ("Start Free Trial" -> "Go to Dashboard")
        document.querySelectorAll('a').forEach(a => {
            const text = a.innerText.trim().toLowerCase();
            if (text === 'start free trial' || text === 'get started') {
                a.href = "upload.html";
                a.innerText = "Go to Dashboard 🚀";
                a.style.backgroundColor = "#10b981"; // Optional: make it green to stand out
                a.style.borderColor = "#10b981";
            }
        });
    }

    // --- OTHER PAGES WIDGET LOGIC ---
    if (token && !currentPage.includes("login.html") && !currentPage.includes("signup.html") && !isHomePage) {
        let remainingQuota = sessionStorage.getItem("meditender_cached_quota");

        if (!remainingQuota) {
            try {
                const quotaRes = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Document/check-quota`, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Authorization": `Bearer ${token}`
                    },
                    body: JSON.stringify({ vendorCount: 0 })
                });

                if (quotaRes.ok) {
                    const quotaData = await quotaRes.json();
                    remainingQuota = quotaData.remainingQuota;
                    sessionStorage.setItem("meditender_cached_quota", remainingQuota);
                } else {
                    remainingQuota = "N/A";
                }
            } catch (e) {
                remainingQuota = "N/A";
            }
        }

        const existingWidget = document.getElementById("meditender-global-widget");
        if (existingWidget) {
            existingWidget.remove();
        }

        const widgetContainer = document.createElement("div");
        widgetContainer.id = "meditender-global-widget";
        widgetContainer.dir = "ltr";
        widgetContainer.style.cssText = `position: fixed; bottom: 20px; right: 20px; display: flex; align-items: center; gap: 10px; z-index: 9999; font-family: system-ui, -apple-system, sans-serif;`;

        const quotaBadge = document.createElement("div");
        const coinSpan = document.createElement("span");
        coinSpan.textContent = "🪙 ";
        const quotaValue = document.createElement("b");
        quotaValue.textContent = String(remainingQuota);
        const pointsSpan = document.createElement("span");
        pointsSpan.textContent = " Points";
        quotaBadge.appendChild(coinSpan);
        quotaBadge.appendChild(quotaValue);
        quotaBadge.appendChild(pointsSpan);
        quotaBadge.title = "Your current API quota balance";
        quotaBadge.style.cssText = `background-color: #3b82f6; color: white; padding: 10px 16px; border-radius: 50px; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); display: flex; align-items: center; justify-content: center;`;

        const logoutBtn = document.createElement("button");
        logoutBtn.innerHTML = "🚪 Logout";
        logoutBtn.ariaLabel = "Logout";
        logoutBtn.style.cssText = `background-color: #ef4444; color: white; border: none; padding: 10px 18px; border-radius: 50px; cursor: pointer; font-weight: bold; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.2s ease-in-out;`;
        logoutBtn.onmouseover = () => logoutBtn.style.backgroundColor = "#dc2626";
        logoutBtn.onmouseout = () => logoutBtn.style.backgroundColor = "#ef4444";
        logoutBtn.onmousedown = () => logoutBtn.style.transform = "scale(0.95)";
        logoutBtn.onmouseup = () => logoutBtn.style.transform = "scale(1)";
        logoutBtn.onclick = performLogout;

        widgetContainer.appendChild(quotaBadge);
        widgetContainer.appendChild(logoutBtn);
        document.body.appendChild(widgetContainer);
    }
});