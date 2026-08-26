window.equipmentDataTable = {

    initialize: function (selector) {

        if (!window.jQuery || !$.fn.DataTable) {
            console.error(
                "jQuery or DataTables is not loaded."
            );
            return;
        }

        var table = $(selector);

        if (!table.length) {
            console.warn(
                "DataTable element not found: " + selector
            );
            return;
        }

        // Safety: destroy existing instance
        if ($.fn.DataTable.isDataTable(selector)) {
            table.DataTable().destroy();
        }

        table.DataTable({

            paging: true,

            searching: true,

            ordering: true,

            info: true,

            lengthChange: true,

            pageLength: 5,

            lengthMenu: [
                [5, 10, 25, 50, 100, -1],
                [5, 10, 25, 50, 100, "All"],
                [5, , 25, 50, 100, "All"]
            ],

            order: [],

            columnDefs: [
                {
                    orderable: false,
                    targets: [0, -1]
                }
            ],

            language: {

                search: "Search:",

                lengthMenu:
                    "Show _MENU_ records",

                info:
                    "Showing _START_ to _END_ of _TOTAL_ records",

                infoEmpty:
                    "Showing 0 to 0 of 0 records",

                zeroRecords:
                    "No matching records found",

                paginate: {

                    first: "First",

                    last: "Last",

                    next: "Next",

                    previous: "Previous"
                }
            }
        });
    },


    destroy: function (selector) {

        if (!window.jQuery ||
            !$.fn.DataTable) {
            return;
        }

        if ($.fn.DataTable.isDataTable(selector)) {

            $(selector)
                .DataTable()
                .destroy();
        }
    }
};

window.componentDataTable = {

    initialize: function (selector) {

        if (!window.jQuery || !jQuery.fn.DataTable) {
            console.warn("jQuery DataTable is not loaded.");
            return;
        }

        if ($.fn.DataTable.isDataTable(selector)) {
            $(selector).DataTable().destroy();
        }

        $(selector).DataTable({
            pageLength: 5,
            lengthMenu: [
                [5, 10, 25, 50, 100, -1],
                [5, 10, 25, 50, 100, "All"]
            ],
            ordering: true,
            searching: true,
            responsive: false,
            autoWidth: false,
            destroy: true
        });
    },

    destroy: function (selector) {

        if (!window.jQuery || !jQuery.fn.DataTable) {
            return;
        }

        if ($.fn.DataTable.isDataTable(selector)) {
            $(selector).DataTable().destroy();
        }
    }
};

window.inspectionTable = {

    initialize: function () {

        const table = document.getElementById("inspectionTable");

        if (!table) {
            console.log("inspectionTable not found");
            return;
        }

        // Existing DataTable இருந்தால் முதலில் destroy
        if ($.fn.DataTable.isDataTable(table)) {
            $(table).DataTable().clear().destroy();
        }

        // புதிய DataTable
        $(table).DataTable({
            responsive: true,
            paging: true,
            searching: true,
            ordering: true,
            info: true,
            pageLength: 10,
            autoWidth: false
        });

        console.log("Inspection DataTable initialized");
    },

    destroy: function () {

        const table = document.getElementById("inspectionTable");

        if (!table) {
            return;
        }

        if ($.fn.DataTable.isDataTable(table)) {
            $(table).DataTable().clear().destroy();
        }
    }
};