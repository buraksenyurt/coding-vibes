package com.atlasshop.web;

import com.atlasshop.catalog.Catalog;
import com.atlasshop.core.Sku;
import io.javalin.Javalin;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class ShopServer {

    private static final Logger LOG = LoggerFactory.getLogger(ShopServer.class);

    public static void main(String[] args) {
        Catalog catalog = new Catalog(List.of());
        Javalin.create()
                .get("/products/{sku}", ctx -> ctx.result(catalog.find(new Sku(ctx.pathParam("sku")))))
                .start(7070);
        LOG.info("atlas-shop listening on 7070");
    }
}
