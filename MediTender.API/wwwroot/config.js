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

document.addEventListener("DOMContentLoaded", () => {
    const token = sessionStorage.getItem("jwt_token");
    const currentPage = window.location.pathname.toLowerCase();
    
    if (token && !currentPage.includes("login.html") && !currentPage.includes("signup.html")) {
        const logoutBtn = document.createElement("button");
        logoutBtn.innerHTML = "🚪 Logout";
        
        logoutBtn.style.cssText = `
            position: fixed; 
            bottom: 20px; 
            right: 20px; 
            background-color: #ef4444; 
            color: white; 
            border: none; 
            padding: 10px 18px; 
            border-radius: 50px; 
            cursor: pointer; 
            font-weight: bold; 
            box-shadow: 0 4px 6px rgba(0,0,0,0.1); 
            z-index: 9999; 
            transition: all 0.2s ease-in-out;
            display: flex;
            align-items: center;
            gap: 5px;
        `;
        
        logoutBtn.onmouseover = () => logoutBtn.style.backgroundColor = "#dc2626";
        logoutBtn.onmouseout = () => logoutBtn.style.backgroundColor = "#ef4444";
        logoutBtn.onmousedown = () => logoutBtn.style.transform = "scale(0.95)";
        logoutBtn.onmouseup = () => logoutBtn.style.transform = "scale(1)";
        
        logoutBtn.onclick = performLogout; 
        
        document.body.appendChild(logoutBtn);
    }
});