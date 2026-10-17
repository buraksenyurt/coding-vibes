package com.atlasbank.service.account;

import static com.atlasbank.common.Money.zero;

import com.atlasbank.common.Money;
import com.atlasbank.domain.model.Transaction;
import com.atlasbank.domain.rules.TransferRule;
import java.time.Instant;

public class TransferService {

    // AccountService lives in the same package: no import needed.
    private final AccountService accounts;
    private final TransferRule rule = TransferRule.noOverdraft();

    public TransferService(AccountService accounts) {
        this.accounts = accounts;
    }

    public void transfer(String fromId, String toId, Money amount) {
        Money negative = zero(amount.currency()).minus(amount);
        accounts.post(new Transaction(fromId, negative, Instant.now()));
        accounts.post(new Transaction(toId, amount, Instant.now()));
    }

    public TransferRule rule() {
        return rule;
    }
}
