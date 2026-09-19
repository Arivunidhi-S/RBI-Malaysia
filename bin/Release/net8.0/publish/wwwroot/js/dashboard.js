window.rbiDashboard = {

    initialize: function () {

        // ===============================
        // NUMBER COUNTER
        // ===============================

        const counters =
            document.querySelectorAll(".counter");

        counters.forEach(function (counter) {

            const target =
                parseInt(counter.getAttribute("data-target"));

            let current = 0;

            const duration = 1000;

            const steps = 40;

            const increment =
                target / steps;

            const intervalTime =
                duration / steps;


            const timer = setInterval(function () {

                current += increment;

                if (current >= target) {

                    current = target;

                    clearInterval(timer);
                }

                counter.innerText =
                    Math.floor(current).toLocaleString();

            }, intervalTime);

        });


        // ===============================
        // DASHBOARD CARD ANIMATION
        // ===============================

        const cards =
            document.querySelectorAll(".stat-card");

        cards.forEach(function (card, index) {

            card.style.opacity = "0";

            card.style.transform =
                "translateY(15px)";

            setTimeout(function () {

                card.style.transition =
                    "all 0.5s ease";

                card.style.opacity = "1";

                card.style.transform =
                    "translateY(0)";

            }, index * 100);

        });

    }

};