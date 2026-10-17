package com.atlasbank.common;

import java.util.UUID;

public final class Ids {

    private Ids() {
    }

    public static String newId(String prefix) {
        return prefix + "-" + UUID.randomUUID();
    }
}
