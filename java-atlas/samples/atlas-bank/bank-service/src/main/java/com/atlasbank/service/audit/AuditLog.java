package com.atlasbank.service.audit;

import com.atlasbank.service.account.AccountSummary;
import java.util.ArrayList;
import java.util.List;

/** Closes the account <-> audit cycle on purpose. */
public class AuditLog {

    private final List<AuditEntry> entries = new ArrayList<>();

    public void record(AccountSummary summary) {
        entries.add(new AuditEntry(summary.id(), summary.status().name()));
    }

    public List<AuditEntry> entries() {
        return List.copyOf(entries);
    }
}
