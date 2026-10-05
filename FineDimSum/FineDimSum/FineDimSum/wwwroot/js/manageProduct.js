let rowCount = parseInt(document.getElementById("row-count").getAttribute("data-count"));

$(document).on("click", "#add-variation", function (e) {
    e.preventDefault();
    const template = $(".variation-template").clone(false);

    // Update row number and names
    template.removeClass("variation-template").show();
    template.find("td:first").text(rowCount + 1);

    template.find("input, select").each(function () {
        const currentName = $(this).attr("name");
        if (currentName) {
            const newName = currentName.replace("#", rowCount);
            $(this).attr("name", newName);
        }
    });

    $("table tbody").append(template);
    rowCount++;
});

// Remove variation row
$(document).on("click", ".remove-variation", function (e) {
    e.preventDefault();
    $(this).closest("tr").remove();

    $("table tbody tr:not(.variation-template)").each(function (index) {
        // Renumber row num
        $(this).find("td:first").text(index + 1);

        // Renumber input, select
        $(this).find("input, select").each(function () {
            const name = $(this).attr("name");
            if (name) {
                $(this).attr("name", name.replace(/\[\d+\]/, `[${index}]`));
            }
        });
    });

    // Renumber input, select
    $(this).find("input, select").each(function () {
        const name = $(this).attr("name");
        if (name) {
            $(this).attr("name", name.replace(/\[\d+\]/, `[${index}]`));
        }
    });

    rowCount--;
});

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

// Enable hidden file validation
$.validator.setDefaults({ ignore: '' });

// Photo preview
$(document).on('change', '.upload input', function (e) {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];

    img.dataset.src ??= img.src;

    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    }
    else {
        img.src = img.dataset.src;
        e.target.value = '';
    }

    // Trigger input validation
    $(e.target).valid();
});