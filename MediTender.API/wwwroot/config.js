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