const CONFIG = {
    API_BASE_URL: window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1" 
        ? "http://localhost:5172" 
        : "https://api.mediprocure.com"
};

async function fetchWithTimeout(url, options = {}) {
    const timeout = options.timeout || 60000; 
    
    const controller = new AbortController();
    const id = setTimeout(() => controller.abort(), timeout);

    try {
        const response = await fetch(url, { ...options, signal: controller.signal });
        clearTimeout(id);
        return response;
    } catch (error) {
        clearTimeout(id);
        throw error; 
    }
}

function performLogout() {
    sessionStorage.clear();
    window.location.replace("home.html");
}

window.togglePasswordVisibility = function(inputId, button) {
    const input = document.getElementById(inputId);
    if (input && input.type === "password") {
        input.type = "text";
        button.innerText = "🙈"; 
    } else if (input) {
        input.type = "password";
        button.innerText = "👁️"; 
    }
};

document.addEventListener("DOMContentLoaded", () => {
    const token = sessionStorage.getItem("jwt_token");
    const currentPage = window.location.pathname.toLowerCase();
    
    if (currentPage.includes("home.html") || currentPage === "/" || currentPage.endsWith("wwwroot/")) {
        if (token) {
            const actionButtons = document.querySelectorAll('a[href*="login.html"], a[href*="signup.html"]');
            actionButtons.forEach(btn => {
                btn.href = "upload.html"; 
                btn.innerHTML = "Go to Dashboard";
                btn.style.backgroundColor = "#10b981"; 
                btn.style.borderColor = "#10b981";
            });
        }
    }

    if (token && !currentPage.includes("login.html") && !currentPage.includes("signup.html") && !currentPage.includes("home.html")) {
        
        let remainingQuota = sessionStorage.getItem("meditender_cached_quota") || "...";

        const existingWidget = document.getElementById("meditender-global-widget");
        if (existingWidget) {
            existingWidget.remove();
        }

        const widgetContainer = document.createElement("div");
        widgetContainer.id = "meditender-global-widget";
        widgetContainer.dir = "ltr";
        widgetContainer.style.cssText = `position: fixed; bottom: 20px; right: 20px; display: flex; align-items: center; gap: 10px; z-index: 9999; font-family: system-ui, -apple-system, sans-serif;`;
        
        const quotaBadge = document.createElement("div");
        quotaBadge.id = "meditender-quota-badge";
        quotaBadge.innerHTML = `<span>Quota: <b>${remainingQuota}</b> Points</span>`;
        quotaBadge.title = "Your current API quota balance";
        quotaBadge.style.cssText = `background-color: #3b82f6; color: white; padding: 10px 16px; border-radius: 50px; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); display: flex; align-items: center; justify-content: center;`;

        const logoutBtn = document.createElement("button");
        logoutBtn.innerHTML = "Logout";
        logoutBtn.ariaLabel = "Logout";
        logoutBtn.style.cssText = `background-color: #ef4444; color: white; border: none; padding: 10px 18px; border-radius: 50px; cursor: pointer; font-weight: bold; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.2s ease-in-out;`;
        logoutBtn.onclick = performLogout; 
        
        widgetContainer.appendChild(quotaBadge);
        widgetContainer.appendChild(logoutBtn);
        document.body.appendChild(widgetContainer);

        fetch(`${CONFIG.API_BASE_URL}/api/Document/check-quota`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({ vendorCount: 0 })
        })
        .then(res => res.json())
        .then(data => {
            if (data.success || data.remainingQuota !== undefined) {
                sessionStorage.setItem("meditender_cached_quota", data.remainingQuota);
                const badge = document.getElementById("meditender-quota-badge");
                if (badge) {
                    badge.innerHTML = `<span>Quota: <b>${data.remainingQuota}</b> Points</span>`;
                }
            }
        })
        .catch(() => {});
    }
});