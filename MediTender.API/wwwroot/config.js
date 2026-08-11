const CONFIG = {
    API_BASE_URL: "http://localhost:5172" 
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
    sessionStorage.removeItem("jwt_token");
    sessionStorage.removeItem("user_name");
    sessionStorage.removeItem("user_email");
    sessionStorage.removeItem("meditender_plan");
    sessionStorage.removeItem("meditender_quota");
    
    window.location.replace("home.html");
}

document.addEventListener("DOMContentLoaded", async () => {
    const token = sessionStorage.getItem("jwt_token");
    const currentPage = window.location.pathname.toLowerCase();
    
    if (currentPage.includes("home.html") || currentPage === "/" || currentPage.endsWith("wwwroot/")) {
        if (token) {
            const actionButtons = document.querySelectorAll('a[href*="login.html"], a[href*="signup.html"]');
            actionButtons.forEach(btn => {
                btn.href = "upload.html"; 
                btn.innerHTML = "Go to Dashboard";
                btn.style.backgroundColor = "#10b981"; 
                btn.style.color = "white";
                btn.style.borderColor = "#10b981";
            });
        }
    }

    if (token && !currentPage.includes("login.html") && !currentPage.includes("signup.html") && !currentPage.includes("home.html")) {
        
        let remainingQuota = "...";
        try {
            const quotaRes = await fetch(`${CONFIG.API_BASE_URL}/api/Document/check-quota`, {
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
            }
        } catch (e) {
            console.error("Failed to fetch quota");
        }

        const widgetContainer = document.createElement("div");
        widgetContainer.style.cssText = `
            position: fixed; 
            bottom: 20px; 
            right: 20px; 
            display: flex;
            align-items: center;
            gap: 10px;
            z-index: 9999; 
            font-family: system-ui, -apple-system, sans-serif;
        `;

        const quotaBadge = document.createElement("div");
        quotaBadge.innerHTML = `🪙 <b>${remainingQuota}</b> Points`;
        quotaBadge.title = "Your current API quota balance";
        quotaBadge.style.cssText = `
            background-color: #3b82f6; 
            color: white; 
            padding: 10px 16px; 
            border-radius: 50px; 
            font-size: 14px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1); 
            display: flex;
            align-items: center;
            justify-content: center;
        `;

        const logoutBtn = document.createElement("button");
        logoutBtn.innerHTML = "🚪 Logout";
        logoutBtn.style.cssText = `
            background-color: #ef4444; 
            color: white; 
            border: none; 
            padding: 10px 18px; 
            border-radius: 50px; 
            cursor: pointer; 
            font-weight: bold; 
            font-size: 14px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1); 
            transition: all 0.2s ease-in-out;
        `;
        
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

window.toggleDirection = function() {
    const htmlDoc = document.documentElement;
    const currentDir = htmlDoc.getAttribute("dir");

    if (currentDir === "rtl") {
        htmlDoc.setAttribute("dir", "ltr");
        htmlDoc.setAttribute("lang", "en");
    } else {
        htmlDoc.setAttribute("dir", "rtl");
        htmlDoc.setAttribute("lang", "ar");
    }
};

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

document.addEventListener("DOMContentLoaded", async () => {
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
        
        // 1. Quota Caching Logic
        let remainingQuota = sessionStorage.getItem("meditender_cached_quota");

        if (!remainingQuota) {
            try {
                const quotaRes = await fetch(`${CONFIG.API_BASE_URL}/api/Document/check-quota`, {
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
                    sessionStorage.setItem("meditender_cached_quota", remainingQuota); // Save to cache
                }
            } catch (e) {
                remainingQuota = "...";
            }
        }

        const widgetContainer = document.createElement("div");
        widgetContainer.style.cssText = `position: fixed; bottom: 20px; right: 20px; display: flex; align-items: center; gap: 10px; z-index: 9999; font-family: system-ui, -apple-system, sans-serif;`;

        const quotaBadge = document.createElement("div");
        quotaBadge.innerHTML = `🪙 <b>${remainingQuota}</b> Points`;
        quotaBadge.title = "Your current API quota balance";
        quotaBadge.style.cssText = `background-color: #3b82f6; color: white; padding: 10px 16px; border-radius: 50px; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); display: flex; align-items: center; justify-content: center;`;

        // 2. Global AR/EN Translation Button
        const langBtn = document.createElement("button");
        langBtn.innerHTML = "🌐 AR / EN";
        langBtn.ariaLabel = "Toggle Language Direction";
        langBtn.style.cssText = `background-color: #f59e0b; color: white; border: none; padding: 10px 18px; border-radius: 50px; cursor: pointer; font-weight: bold; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.2s ease-in-out;`;
        langBtn.onclick = window.toggleDirection;

        const logoutBtn = document.createElement("button");
        logoutBtn.innerHTML = "🚪 Logout";
        logoutBtn.ariaLabel = "Logout";
        logoutBtn.style.cssText = `background-color: #ef4444; color: white; border: none; padding: 10px 18px; border-radius: 50px; cursor: pointer; font-weight: bold; font-size: 14px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.2s ease-in-out;`;
        logoutBtn.onclick = performLogout; 
        
        widgetContainer.appendChild(quotaBadge);
        widgetContainer.appendChild(langBtn);
        widgetContainer.appendChild(logoutBtn);
        document.body.appendChild(widgetContainer);
    }
});