package com.atlasbank.service.account;

import com.atlasbank.common.Money;
import com.atlasbank.domain.model.Account;
import com.atlasbank.domain.model.Account.Status;

public record AccountSummary(String id, Money balance, Status status) {

    public static AccountSummary of(Account account) {
        return new AccountSummary(account.id(), account.balance(), account.status());
    }
}
