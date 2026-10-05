$(document).ready(function () {
    let isOpening = false;

    // Admin sidebar
    $("#menu").click(function () {
        if (isOpening) return;
        isOpening = true;
        $(this).toggleClass('open');

        const sidebar = $(".admin-sidebar");

        if (sidebar.hasClass("open")) {
            sidebar.removeClass("open");
            setTimeout(() => {
                sidebar.css("display", "none");
                isOpening = false;
            }, 400);
        } else {
            sidebar.css("display", "flex");
            setTimeout(() => {
                sidebar.addClass("open");
                isOpening = false;
            }, 10);
        }
    });

    $(document).click(function (e) {
        const target = $(e.target);

        if (!target.closest(".user-section").length && !target.closest(".user-dropdown").length) {
            $(".user-dropdown").slideUp(100, function () {
                $(this).removeClass("d-flex");
            });
        }
    });

    // User dropdown
    $(".user-section").click(function (e) {
        e.stopPropagation();

        const dropdown = $(".user-dropdown");

        if (dropdown.hasClass("d-flex")) {
            dropdown.slideUp(100, function () {
                dropdown.removeClass("d-flex");
            });
        } else {
            dropdown.addClass("d-flex").hide().slideDown(100);
        }
    });
});