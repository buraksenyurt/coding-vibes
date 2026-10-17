package com.atlasshop.catalog;

import com.atlasshop.core.Sku;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.List;

public class Catalog {

    private final List<Product> products;
    private final ObjectMapper json = new ObjectMapper();

    public Catalog(List<Product> products) {
        this.products = List.copyOf(products);
    }

    public String find(Sku sku) throws Exception {
        for (Product p : products) {
            if (p.sku().equals(sku)) {
                return json.writeValueAsString(p);
            }
        }
        return "null";
    }
}
