package com.atlasshop.core;

import com.google.common.base.Preconditions;

public record Sku(String value) {

    public Sku {
        Preconditions.checkArgument(!value.isBlank(), "SKU must not be blank");
    }
}
