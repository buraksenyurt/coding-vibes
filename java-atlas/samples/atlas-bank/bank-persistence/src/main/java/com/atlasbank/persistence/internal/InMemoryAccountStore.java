package com.atlasbank.persistence.internal;

import com.atlasbank.domain.model.Account;
import com.atlasbank.persistence.AccountRepository;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

public class InMemoryAccountStore implements AccountRepository {

    private final Map<String, Account> accounts = new ConcurrentHashMap<>();
    // Fully qualified type without an import.
    private final java.util.concurrent.atomic.AtomicLong writes = new java.util.concurrent.atomic.AtomicLong();

    @Override
    public Optional<Account> findById(String id) {
        return Optional.ofNullable(accounts.get(id));
    }

    @Override
    public void save(Account account) {
        accounts.put(account.id(), account);
        writes.incrementAndGet();
    }

    public long writeCount() {
        return writes.get();
    }
}
