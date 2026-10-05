$(document).ready(function () {
    // Variation Selection
    const imageContainer = $('#img-section');
    const variationOptions = $('.variation-option');
    const variationIdInput = $('#productVariationOptionId');
    const addToCartButton = $('#add-to-cart-btn');
    const quantityInput = $('#quantity');

    variationOptions.each(function () {
        const option = $(this);

        // Check if the variation is out of stock
        if (option.hasClass('disabled')) {
            // Disable interaction for out-of-stock items
            option.css({
                'pointer-events': 'none',
                'opacity': '0.7'
            });
        } else {
            let selectedImage = "";

            // Add click event listener for in-stock items
            option.on('click', function () {
                if (option.hasClass('selected')) {
                    // Remove active class from all options
                    variationOptions.removeClass('selected');

                    // Set the hidden variation_id input value
                    variationIdInput.val(0);

                    // Enable the Add to Cart button
                    addToCartButton.prop('disabled', true);

                    // Update max quantity based on stock
                    quantityInput.attr('max', 1);

                    // Get product image
                    selectedImage = $('#productImg').val();
                } else {
                    // Remove active class from all options
                    variationOptions.removeClass('selected');

                    // Add active class to the selected option
                    option.addClass('selected');

                    // Set the hidden variation_id input value
                    variationIdInput.val(option.data('variation-id'));

                    // Enable the Add to Cart button
                    addToCartButton.prop('disabled', false);

                    // Update max quantity based on stock
                    quantityInput.attr('max', option.data('stock-qty'));

                    // Get variation image
                    selectedImage = option.data('variation-image');
                    
                }

                // Change image
                imageContainer.find('img').attr('src', selectedImage);
            });
        }
    });
});
