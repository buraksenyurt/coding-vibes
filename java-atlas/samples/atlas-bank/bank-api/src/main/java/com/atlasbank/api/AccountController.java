package com.atlasbank.api;

import com.atlasbank.persistence.internal.InMemoryAccountStore;
import com.atlasbank.service.account.AccountService;
import com.atlasbank.service.account.AccountSummary;
import com.atlasbank.service.audit.AuditLog;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class AccountController {

    private static final Logger LOG = LoggerFactory.getLogger(AccountController.class);

    // Shortcut straight into a package bank-persistence does not export.
    private final InMemoryAccountStore store = new InMemoryAccountStore();
    private final AccountService service = new AccountService(store, new AuditLog());
    private final ObjectMapper json = new ObjectMapper();

    public String show(String id) throws Exception {
        LOG.debug("show {}", id);
        return store.findById(id)
                .map(AccountSummary::of)
                .map(this::write)
                .orElse("{}");
    }

    public AccountService service() {
        return service;
    }

    private String write(AccountSummary summary) {
        try {
            return json.writeValueAsString(summary);
        } catch (Exception e) {
            throw new IllegalStateException(e);
        }
    }
}
