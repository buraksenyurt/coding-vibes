package com.atlasbank.persistence;

import com.atlasbank.persistence.internal.InMemoryAccountStore;

public final class Repositories {

    private Repositories() {
    }

    public static AccountRepository inMemory() {
        return new InMemoryAccountStore();
    }
}
