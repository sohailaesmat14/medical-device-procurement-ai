// FIX: API_BASE_URL was hardcoded to localhost, which would silently break in any
// deployed environment. This now uses localhost only when actually running locally,
// and otherwise falls back to a same-origin relative path so the app keeps working
// if the API is served from the same host/domain as these static files. If your API
// lives on a different domain in production, replace the fallback below with your
// real API URL.
const CONFIG = {
    API_BASE_URL:
        window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
            ? "http://localhost:5172"
            : window.location.origin // TODO: replace with your production API URL if it's on a different host
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
    sessionStorage.removeItem("meditender_cached_quota");

    window.location.replace("home.html");
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

// FIX: This entire initialization block used to be registered TWICE via two nearly
// identical "DOMContentLoaded" listeners. Both ran on every page load: the first
// built a floating quota/logout widget with no id (so it could never be found and
// removed), and the second built its own widget (with caching) after trying — and
// failing — to remove the first one by id. The visible result was a duplicated
// quota badge + logout button stacked on top of each other on every protected page,
// plus a redundant, uncached "/api/Document/check-quota" network call on every load.
// There is now a single listener that does the caching-aware version only.
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
        // Quota caching logic — avoids re-fetching on every single page navigation.
        // Pages that consume quota (e.g. processing.html) explicitly clear this cache
        // key after a successful operation so the badge stays accurate.
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
                    sessionStorage.setItem("meditender_cached_quota", remainingQuota);
                } else {
                    remainingQuota = "...";
                }
            } catch (e) {
                remainingQuota = "...";
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
        // FIX: remainingQuota ultimately comes from the server, but building the
        // badge via createElement/textContent (rather than trusting a raw innerHTML
        // interpolation) keeps this safe even if that ever changes to include
        // free-form text in the future.
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
