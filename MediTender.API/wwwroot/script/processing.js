
if (!sessionStorage.getItem("jwt_token")) {
    window.location.replace("login.html");
}

const token = sessionStorage.getItem("jwt_token");
if (!token) {
    window.location.replace("login.html");
}

const unloadWarning = function (e) {
    const msg = "Are you sure you want to leave? Your progress will be lost.";
    e.returnValue = msg;
    return msg;
};
window.addEventListener("beforeunload", unloadWarning);

function updateProgress(percent, message) {
    const progressFill = document.getElementById("progressFill");
    const statusText = document.getElementById("processStatus"); 
    
    if (progressFill) {
        progressFill.style.width = `${percent}%`;
        progressFill.innerText = `${percent}%`;
    }
    if (statusText) {
        statusText.innerText = message;
    }
}

let currentStep = sessionStorage.getItem("meditender_current_step") || "extract"; 

document.addEventListener("DOMContentLoaded", () => {
    const userName = sessionStorage.getItem("user_name") || "Committee Member";
    const nameDisplay = document.getElementById("userNameDisplay");
    const avatarDisplay = document.getElementById("userAvatar");
    
    if (nameDisplay) nameDisplay.innerText = userName;
    if (avatarDisplay) {
        avatarDisplay.innerText = userName.split(' ')
                                .map(n => n[0])
                                .join('')
                                .substring(0, 2)
                                .toUpperCase();
    }
    
    async function startProcessing() {
        const standardFile = sessionStorage.getItem("meditender_standard_file");
        const vendorsRaw = sessionStorage.getItem("meditender_vendors");
        const currentTenderId = sessionStorage.getItem("meditender_current_id");
        const token = sessionStorage.getItem("jwt_token"); 

        const statusText = document.getElementById("processStatus");
        const progressWrapper = document.getElementById("progressWrapper");
        const progressFill = document.getElementById("progressFill");
        const retryBtn = document.getElementById("retryBtn");
        const spinnerWrapper = document.getElementById("spinnerWrapper");
        const mainTitle = document.getElementById("mainTitle");
        const errorContainer = document.getElementById("errorContainer");
        const errorMessage = document.getElementById("errorMessage");

        if (retryBtn) {
            retryBtn.disabled = false;
            retryBtn.innerText = "Resume Processing 🔄";
        }

        function showErrorState(message) {
            window.removeEventListener("beforeunload", unloadWarning);
            statusText.innerText = "Processing stopped.";
            statusText.style.color = "#dc2626";
            mainTitle.innerText = "Analysis Failed";
            spinnerWrapper.style.display = "none";
            progressWrapper.style.display = "none";
            
            if(errorContainer && errorMessage) {
                errorContainer.style.display = "block";
                errorMessage.innerText = message;
            }
        }

        if (!standardFile || !vendorsRaw || !currentTenderId) {
            showErrorState("Error: Missing data or Tender ID from upload step.");
            return;
        }

        const vendors = JSON.parse(vendorsRaw);
        
        progressWrapper.style.display = "block";
        spinnerWrapper.style.display = "block";
        if(errorContainer) errorContainer.style.display = "none";
        statusText.style.color = "";
        if (progressFill) progressFill.style.backgroundColor = "#2563eb"; 
        mainTitle.innerText = "Processing Document...";

        try {
            if (currentStep === "extract") {
                updateProgress(10, "Phase 1: Auto-extracting mandatory specifications from Tender Standard...");

                let extractRes = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Document/extract-standard`, {
                    method: 'POST', 
                    headers: { 
                        'Content-Type': 'application/json', 
                        'Authorization': `Bearer ${token}` 
                    },
                    body: JSON.stringify({
                        fileName: standardFile,
                        tenderId: parseInt(currentTenderId)
                    }),
                    timeout: 300000 
                });
                
                if (!extractRes.ok)
                    throw new Error(`API Error: ${await extractRes.text()}`);

                updateProgress(45, "Phase 1 Complete! Standard requirements extracted successfully.");
                
                currentStep = "compare"; 
                sessionStorage.setItem("meditender_current_step", "compare");
                
                await new Promise(r => setTimeout(r, 1000));
            }

            if (currentStep === "compare") {
                sessionStorage.removeItem("meditender_cached_quota");
                updateProgress(55, "Phase 2: AI Agents evaluating Vendor Offers in parallel. This may take a moment...");

                let compareRes = await fetchWithTimeout(`${CONFIG.API_BASE_URL}/api/Document/compare-vendors`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    },
                    body: JSON.stringify({
                        tenderId: parseInt(currentTenderId),
                        vendorNames: vendors
                    }),
                    timeout: 420000
                });

                if (!compareRes.ok)
                    throw new Error(`Comparison API Error: ${await compareRes.text()}`);

                let evaluations = await compareRes.json();

                updateProgress(100, "Analysis Complete! Redirecting to Decision Matrix...");
                
                sessionStorage.removeItem("meditender_current_step");
                sessionStorage.setItem("meditender_evaluations", JSON.stringify(evaluations));

                window.removeEventListener("beforeunload", unloadWarning);

                setTimeout(() => {
                    window.location.replace("dashboard.html");
                }, 1000);
            }

        } catch (err) {
            let errorMsg = err.message;
            
            if (err.name === 'AbortError') {
                errorMsg = "The request timed out. The server or AI is taking too long to respond. Please try again.";
            }

            if (progressFill) progressFill.style.backgroundColor = "#dc3545"; 
            updateProgress(100, "System Error: " + errorMsg); 
            showErrorState("System Error: " + errorMsg);

            if(retryBtn) {
                retryBtn.onclick = () => {
                    retryBtn.disabled = true;
                    retryBtn.innerText = "Resuming... ⏳";
                    window.addEventListener("beforeunload", unloadWarning);
                    if(errorContainer) errorContainer.style.display = "none";
                    startProcessing();
                };
            }
        }
    }

    startProcessing();
});
