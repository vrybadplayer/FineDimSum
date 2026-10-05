$(document).on("click", "[data-post-delete]", function (e) {
    e.preventDefault();

    const id = $(this).data("id");
    const url = $(this).data("post-delete");

    if (confirm("Are you sure to delete this product? (Product Id: " + id + ")\n(Warning: All OrderItem (Preparing) in Active Order will be deleted.)")) {
        const form = $('<form>', {
            method: 'post',
            action: url
        }).appendTo(document.body);

        form.submit();
    }
});