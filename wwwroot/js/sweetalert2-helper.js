window.sweetAlert = {

    success: function (title, message) {

        Swal.fire({
            icon: 'success',
            title: title,
            text: message,
            timer: 3000,
            timerProgressBar: true,
            showConfirmButton: false,
            position: 'center'
        });

    },

    error: function (title, message) {

        Swal.fire({
            icon: 'error',
            title: title,
            text: message,
            timer: 3000,
            timerProgressBar: true,
            showConfirmButton: false,
            position: 'center'
        });

    },

    warning: function (title, message) {

        Swal.fire({
            icon: 'warning',
            title: title,
            text: message,
            timer: 3000,
            timerProgressBar: true,
            showConfirmButton: false,
            position: 'center'
        });

    },

    confirmDelete: async function (title, message) {

        const result = await Swal.fire({

            icon: 'warning',

            title: title,

            text: message,

            showCancelButton: true,

            confirmButtonText: '<i class="bi bi-trash"></i> Yes, Delete',

            cancelButtonText: '<i class="bi bi-x-lg"></i> Cancel',

            confirmButtonColor: '#dc3545',

            cancelButtonColor: '#6c757d',

            reverseButtons: true,

            focusCancel: true

        });

        return result.isConfirmed;
    }
};