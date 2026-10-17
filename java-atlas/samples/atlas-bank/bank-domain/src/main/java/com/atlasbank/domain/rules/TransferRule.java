package com.atlasbank.domain.rules;

import com.atlasbank.common.Money;
import com.atlasbank.domain.model.Account;

public interface TransferRule {

    boolean allows(Account from, Money amount);

    static TransferRule noOverdraft() {
        return (from, amount) -> !from.balance().minus(amount).isNegative();
    }
}
