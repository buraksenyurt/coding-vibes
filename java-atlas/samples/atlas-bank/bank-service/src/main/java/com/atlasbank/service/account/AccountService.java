package com.atlasbank.service.account;

import com.atlasbank.domain.model.*;
import com.atlasbank.persistence.AccountRepository;
import com.atlasbank.service.audit.AuditLog;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class AccountService {

    private static final Logger LOG = LoggerFactory.getLogger(AccountService.class);

    private final AccountRepository repository;
    private final AuditLog audit;

    public AccountService(AccountRepository repository, AuditLog audit) {
        this.repository = repository;
        this.audit = audit;
    }

    // Account and Transaction come from the wildcard import above.
    public void post(Transaction tx) {
        Account account = repository.findById(tx.accountId()).orElseThrow();
        account.apply(tx);
        repository.save(account);
        audit.record(AccountSummary.of(account));
        LOG.info("Posted {} to {}", tx.amount(), account.id());
    }
}
