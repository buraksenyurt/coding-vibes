package com.atlasshop.catalog;

import com.atlasshop.core.Sku;

public record Product(Sku sku, String title, long priceCents) {
}
