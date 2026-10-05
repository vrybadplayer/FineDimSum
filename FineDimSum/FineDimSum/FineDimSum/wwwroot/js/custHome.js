function submitCategoryForm(category) {
    const hiddenInput = document.getElementById('selectedCategoryInput');
    hiddenInput.value = category;

    // Submit the form
    document.getElementById('categoryForm').submit();
}

function selectCategory(element) {
    // Remove 'active' class from all items
    document.querySelectorAll('.category-item').forEach((item) => {
        item.classList.remove('active');
    });

    // Add 'active' class to the selected item
    element.classList.add('active');
}

function scrollSidebar(direction) {
    const listContainer = document.querySelector('.list-container');
    const itemHeight = 40; // Adjust based on the height of .category-item

    if (direction === 'up') {
        listContainer.scrollTop = Math.max(0, listContainer.scrollTop - itemHeight); // Scroll up
    } else if (direction === 'down') {
        listContainer.scrollTop = Math.min(
            listContainer.scrollHeight - listContainer.clientHeight,
            listContainer.scrollTop + itemHeight
        ); // Scroll down
    }
}

// Filter dropdown
$(".filter-btn").click(function (e) {
    e.stopPropagation();

    const dropdown = $(".filter-dropdown");

    if (dropdown.hasClass("d-flex")) {
        dropdown.slideUp(100, function () {
            dropdown.removeClass("d-flex");
        });
    } else {
        dropdown.addClass("d-flex").hide().slideDown(100);
    }
});