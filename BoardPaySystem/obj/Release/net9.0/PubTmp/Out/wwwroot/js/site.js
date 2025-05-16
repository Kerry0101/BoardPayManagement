// wwwroot/js/site.js

document.addEventListener('DOMContentLoaded', () => {
    // --- Mobile Menu Functionality ---
    window.toggleMobileMenu = () => {
        const sidebar = document.querySelector('.sidebar');
        const overlay = document.querySelector('.mobile-menu-overlay');
        const body = document.body;

        if (sidebar && overlay) {
            sidebar.classList.toggle('active');
            overlay.style.display = sidebar.classList.contains('active') ? 'block' : 'none';
            body.classList.toggle('menu-open');
        }
    };

    // Close mobile menu when clicking outside
    const overlay = document.querySelector('.mobile-menu-overlay');
    if (overlay) {
        overlay.addEventListener('click', () => {
            const sidebar = document.querySelector('.sidebar');
            if (sidebar) {
                sidebar.classList.remove('active');
                overlay.style.display = 'none';
                document.body.classList.remove('menu-open');
            }
        });
    }

    // --- Element References ---
    const loginForm = document.getElementById('login-form');
    const loginError = document.getElementById('login-error');
    // Logout buttons are handled by form submission now, JS listener removed.

    // --- Form/Modal/Message References (Check if they exist) ---
    const addBuildingFormContainer = document.getElementById('add-building-form-container');
    const buildingForm = document.getElementById('building-form');
    const buildingFormMessage = document.getElementById('building-form-message'); // Keep for potential server-side feedback display
    const addTenantForm = document.getElementById('add-tenant-form');
    const addTenantMessage = document.getElementById('add-tenant-message'); // Keep for potential server-side feedback display
    const meterReadingForm = document.getElementById('meter-reading-form');
    const addReadingMessage = document.getElementById('add-reading-message'); // Keep for potential server-side feedback display
    const updateFeesModal = document.getElementById('update-fees-modal');
    const updateFeesForm = document.getElementById('update-fees-form');
    const updateFeesMessage = document.getElementById('update-fees-message'); // Keep for potential server-side feedback display
    const gcashBtn = document.querySelector('#tenant-dashboard #t-payment button.btn-success'); // Assumes Tenant dashboard view exists
    const gcashMessage = document.getElementById('gcash-message');

    // --- Helper Functions ---
    function showMessage(element, text, type = 'success', duration = 3000) {
        if (!element) return;
        element.textContent = text;
        element.className = `alert alert-${type}`;
        element.style.display = 'block';
        if (duration > 0) {
            if (element.timeoutId) clearTimeout(element.timeoutId);
            element.timeoutId = setTimeout(() => {
                element.style.display = 'none';
                element.timeoutId = null;
            }, duration);
        }
    }
    window.showMessage = showMessage;

    // --- Form Validations ---
    function validateForm(form) {
        let isValid = true;
        const requiredFields = form.querySelectorAll('[required]');
        
        requiredFields.forEach(field => {
            if (!field.value.trim()) {
                isValid = false;
                field.classList.add('is-invalid');
            } else {
                field.classList.remove('is-invalid');
            }
        });
        
        return isValid;
    }

    // --- Login Form Handling ---
    if (loginForm) {
        loginForm.addEventListener('submit', (e) => {
            if (loginError) loginError.style.display = 'none';
            
            if (!validateForm(loginForm)) {
                e.preventDefault();
                showMessage(loginError, "Please fill in all required fields.", 'danger', 0);
            }
        });
    }

    // --- Logout ---
    // REMOVED: logoutBtns.forEach(...) listener.
    // Logout should be handled by submitting a form containing the logout button
    // directly to the server's Logout action. Ensure your logout button is
    // inside a <form method="post" asp-action="Logout" asp-controller="Account">
    // with @Html.AntiForgeryToken().


    // --- Landlord Page Specific Logic ---

    // Manage Buildings Form Toggle (UI Logic - Keep as is)
    window.showAddBuildingForm = () => {
        if (buildingForm) buildingForm.reset();
        const buildingIdInput = document.getElementById('building-id');
        if (buildingIdInput) buildingIdInput.value = ''; // Reset hidden field if editing
        if (addBuildingFormContainer) addBuildingFormContainer.style.display = 'block';
        if (buildingFormMessage) buildingFormMessage.style.display = 'none'; // Hide old messages
        addBuildingFormContainer?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    };
    window.hideAddBuildingForm = () => {
        if (addBuildingFormContainer) addBuildingFormContainer.style.display = 'none';
    };

    // Building Form Submission
    if (buildingForm) {
        buildingForm.addEventListener('submit', (e) => {
            if (!validateForm(buildingForm)) {
                e.preventDefault();
                showMessage(buildingFormMessage, "Please fill in all required fields.", 'danger', 0);
            }
        });
    }

    // Add Tenant Form Submission
    if (addTenantForm) {
        addTenantForm.addEventListener('submit', (e) => {
            if (!validateForm(addTenantForm)) {
                e.preventDefault();
                showMessage(addTenantMessage, "Please fill in all required fields.", 'danger', 0);
            }
        });
    }

    // Meter Reading Form Submission
    if (meterReadingForm) {
        meterReadingForm.addEventListener('submit', (e) => {
            if (!validateForm(meterReadingForm)) {
                e.preventDefault();
                showMessage(addReadingMessage, "Please fill in all required fields.", 'danger', 0);
            }
        });
    }

    // --- Tenant Detail & Fee Update Modal Logic ---

    // Show Modal (UI Logic - Keep as is, might need AJAX later for dynamic data)
    window.showTenantDetails = (tenantId) => {
        console.log("Opening update fees modal for tenant ID:", tenantId);
        // In a real app, you might fetch CURRENT fee overrides via AJAX here
        // to pre-populate the modal form if needed. For now, it just opens.

        let tenantName = `Tenant ${tenantId}`; // Placeholder, get real name from element or data attribute
        // Example: Find the name in the table row corresponding to tenantId
        const tenantRow = document.querySelector(`tr[data-tenant-id="${tenantId}"]`);
        if (tenantRow) {
            const nameCell = tenantRow.querySelector('td:first-child'); // Assuming name is first cell
            if (nameCell) tenantName = nameCell.textContent;
        }


        const updateTenantIdInput = document.getElementById('update-fees-tenant-id');
        const updateTenantNameHeader = document.getElementById('update-fees-tenant-name');

        if (updateTenantIdInput) updateTenantIdInput.value = tenantId; // Set the ID for the form
        if (updateTenantNameHeader) updateTenantNameHeader.textContent = `Update Fees for ${tenantName}`;

        if (updateFeesModal) {
            updateFeesForm?.reset(); // Clear previous entries
            const msgElem = document.getElementById('update-fees-message');
            if (msgElem) msgElem.style.display = 'none'; // Hide old messages
            updateFeesModal.style.display = 'block';
        } else {
            console.error("Update Fees Modal not found on this page.");
        }
    };

    // Show/Close Modal Functions (UI Logic - Keep as is)
    window.showUpdateFeesModal = () => { // Can be called directly if needed
        if (updateFeesModal) {
            updateFeesForm?.reset();
            const msgElem = document.getElementById('update-fees-message');
            if (msgElem) msgElem.style.display = 'none';
            updateFeesModal.style.display = 'block';
        }
    };
    window.closeUpdateFeesModal = () => {
        if (updateFeesModal) updateFeesModal.style.display = 'none';
    };

    // Fee Update Form Submission (Inside Modal)
    if (updateFeesForm) {
        updateFeesForm.addEventListener('submit', (e) => {
            if (!validateForm(updateFeesForm)) {
                e.preventDefault();
                showMessage(updateFeesMessage, "Please fill in all required fields.", 'danger', 0);
            }
        });
    }

    // --- Tenant Page Specific Logic ---
    // GCash Button click (assuming this INITIATES something, might need AJAX or form post later)
    if (gcashBtn) {
        gcashBtn.addEventListener('click', () => {
            showMessage(gcashMessage, 'Processing GCash payment...', 'info');
        });
    }

    // --- General ---
    // Close modal clicking outside (UI Logic - Keep as is)
    window.onclick = function (event) {
        if (updateFeesModal && event.target == updateFeesModal) {
            closeUpdateFeesModal();
        }
    }

}); // End DOMContentLoaded