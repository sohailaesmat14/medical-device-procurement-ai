
function escapeHTML(str) {
    if (!str) return "";
    return str
        .toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

document.addEventListener("DOMContentLoaded", async () => {
    const token = sessionStorage.getItem("jwt_token");
    if (!token) {
        window.location.replace("login.html");
        return;
    }

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Document/my-tenders`, {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (response.ok) {
            const tenders = await response.json();
            document.getElementById("loadingState").style.display = "none";

            if (tenders.length === 0) {
                document.getElementById("emptyState").style.display = "block";
            } else {
                const tbody = document.getElementById("tendersBody");
                tenders.forEach(tender => {
                    const date = new Date(tender.createdAt).toLocaleDateString() + " " + new Date(tender.createdAt).toLocaleTimeString();
                    const safeTitle = escapeHTML(tender.title);
                    
                    const tr = document.createElement("tr");
                    tr.innerHTML = `
                        
                        <td>${safeTitle}</td>
                        <td>${date}</td>
                        <td>
                            <button class="btn-view" onclick="loadTenderDashboard(${tender.id}, event)">📊 View Matrix</button>
                        </td>
                    `;
                    tbody.appendChild(tr);
                });
                document.getElementById("tendersTable").style.display = "table";
            }
        } else {
            throw new Error("Failed to fetch tenders.");
        }
    } catch (error) {
        document.getElementById("loadingState").innerText = error.message || "Error loading data. Please try again.";
        document.getElementById("loadingState").style.color = "#dc2626";
    }
});

async function loadTenderDashboard(tenderId, event) {
    const token = sessionStorage.getItem("jwt_token");
    const btn = event.currentTarget;
    const originalText = btn.innerText;
    btn.innerText = "Loading...";
    btn.disabled = true;

    try {
        const response = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Document/evaluations/${tenderId}`, {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (response.ok) {
            const evaluations = await response.json();
            if(evaluations && evaluations.length > 0) {
                sessionStorage.setItem("meditender_evaluations", JSON.stringify(evaluations));
                sessionStorage.setItem("meditender_current_id", tenderId);
                window.location.href = "dashboard.html";
            } else {
                showToast("No evaluations found for this tender.", "warning");
                btn.innerText = originalText;
                btn.disabled = false;
            }
        } else {
            throw new Error("Failed to fetch evaluations.");
        }
    } catch (error) {
        showToast(error.message || "Error loading dashboard data.", "error");
        btn.innerText = originalText;
        btn.disabled = false;
    }
}
