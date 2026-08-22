window.RBIValidation = {

    // ==========================================
    // Validate Required Fields
    // ==========================================
    validateRequired: function (element) {

        if (!element)
            return false;

        let value = element.value?.trim();

        if (!value) {

            element.classList.add("is-invalid");

            return false;
        }

        element.classList.remove("is-invalid");
        element.classList.add("is-valid");

        return true;
    },


    // ==========================================
    // Validate Email
    // ==========================================
    validateEmail: function (element) {

        if (!element)
            return false;

        let value = element.value?.trim();

        if (!value) {

            element.classList.remove("is-valid");
            element.classList.remove("is-invalid");

            return true;
        }

        let emailPattern =
            /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

        if (!emailPattern.test(value)) {

            element.classList.add("is-invalid");
            element.classList.remove("is-valid");

            return false;
        }

        element.classList.remove("is-invalid");
        element.classList.add("is-valid");

        return true;
    },


    // ==========================================
    // Validate Number
    // ==========================================
    validateNumber: function (element) {

        if (!element)
            return false;

        let value = element.value?.trim();

        if (!value) {

            element.classList.add("is-invalid");

            return false;
        }

        if (isNaN(value)) {

            element.classList.add("is-invalid");
            element.classList.remove("is-valid");

            return false;
        }

        element.classList.remove("is-invalid");
        element.classList.add("is-valid");

        return true;
    },


    // ==========================================
    // Validate Mobile
    // ==========================================
    validateMobile: function (element) {

        if (!element)
            return false;

        let value = element.value?.trim();

        if (!value) {

            element.classList.add("is-invalid");

            return false;
        }

        let mobilePattern = /^[0-9]{10,15}$/;

        if (!mobilePattern.test(value)) {

            element.classList.add("is-invalid");
            element.classList.remove("is-valid");

            return false;
        }

        element.classList.remove("is-invalid");
        element.classList.add("is-valid");

        return true;
    },


    // ==========================================
    // Validate Form
    // ==========================================
    validateForm: function (formElement) {

        if (!formElement)
            return false;

        let valid = true;

        // Required fields
        formElement
            .querySelectorAll(".validate-required")
            .forEach(element => {

                if (!this.validateRequired(element)) {
                    valid = false;
                }

            });


        // Email fields
        formElement
            .querySelectorAll(".validate-email")
            .forEach(element => {

                if (!this.validateEmail(element)) {
                    valid = false;
                }

            });


        // Number fields
        formElement
            .querySelectorAll(".validate-number")
            .forEach(element => {

                if (!this.validateNumber(element)) {
                    valid = false;
                }

            });


        // Mobile fields
        formElement
            .querySelectorAll(".validate-mobile")
            .forEach(element => {

                if (!this.validateMobile(element)) {
                    valid = false;
                }

            });


        return valid;
    },


    // ==========================================
    // Clear Validation
    // ==========================================
    clear: function (element) {

        if (!element)
            return;

        element.classList.remove("is-valid");
        element.classList.remove("is-invalid");
    },


    // ==========================================
    // Clear Form Validation
    // ==========================================
    clearForm: function (formElement) {

        if (!formElement)
            return;

        formElement
            .querySelectorAll(".is-valid, .is-invalid")
            .forEach(element => {

                element.classList.remove("is-valid");
                element.classList.remove("is-invalid");

            });
    }

};